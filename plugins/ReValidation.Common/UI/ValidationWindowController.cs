using ReValidation.Common.Abstractions;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;

namespace ReValidation.Common.UI;

public sealed class ValidationWindowController : IDisposable
{
    private readonly ValidationScenarioRegistry registry;
    private readonly IValidationScenarioRunner runner;
    private readonly IScenarioExecutionContextFactory contextFactory;
    private readonly TimeProvider timeProvider;
    private readonly CancellationTokenSource lifetimeCancellationSource = new();
    private readonly SemaphoreSlim runGate = new(1, 1);
    private ArmedScenarioRequest? armedScenarioRequest;
    private int disposed;
    private int pulseActive;

    public ValidationWindowController(
        ValidationWindowState state,
        ValidationScenarioRegistry registry,
        IValidationScenarioRunner runner,
        IScenarioExecutionContextFactory? contextFactory = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(runner);

        State = state;
        this.registry = registry;
        this.runner = runner;
        this.contextFactory = contextFactory ?? new ValidationScenarioContextFactory(Path.GetTempPath());
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ValidationWindowState State { get; }
    public IReadOnlyCollection<IValidationScenario> Scenarios => registry.Scenarios;
    public bool CanArmSelectedScenario() => TryGetSelectedArmableScenario(out _);

    public async Task RunSelectedScenarioAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        armedScenarioRequest = null;
        await RunScenarioAsync(CreateSelectionSnapshot(), cancellationToken);
    }

    public void ArmSelectedScenario(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (State.IsBusy)
            return;
        if (!TryGetSelectedArmableScenario(out _))
            throw new InvalidOperationException("The selected scenario does not support arming.");

        armedScenarioRequest = new ArmedScenarioRequest(
            CreateSelectionSnapshot(),
            timeProvider.GetUtcNow().Add(timeout));
        State.SetArmed("Armed");
    }

    public void DisarmSelectedScenario()
    {
        armedScenarioRequest = null;
        if (State.IsArmed)
            State.Reset();
    }

    public async Task PulseArmedScenarioAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (armedScenarioRequest is null)
            return;
        if (Interlocked.Exchange(ref pulseActive, 1) != 0)
            return;

        try
        {
            using var pulseCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellationSource.Token);
            var request = armedScenarioRequest;
            if (request is null)
                return;
            if (timeProvider.GetUtcNow() >= request.DeadlineUtc)
            {
                armedScenarioRequest = null;
                State.SetCompleted("Timed out", Array.Empty<string>());
                return;
            }

            var scenario = registry.GetRequired(request.Selection.ScenarioId);
            if (scenario is not IArmableValidationScenario armableScenario)
            {
                armedScenarioRequest = null;
                State.SetCompleted("Failed", Array.Empty<string>());
                return;
            }

            var cue = await armableScenario.PollArmCueAsync(pulseCancellationSource.Token);
            if (!cue.IsReady)
            {
                State.UpdateArmedStatus(cue.StatusText);
                return;
            }

            armedScenarioRequest = null;
            await RunScenarioAsync(request.Selection, pulseCancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            armedScenarioRequest = null;
            State.SetCompleted("Cancelled", Array.Empty<string>());
        }
        catch (Exception)
        {
            armedScenarioRequest = null;
            State.SetCompleted("Failed", Array.Empty<string>());
        }
        finally
        {
            Volatile.Write(ref pulseActive, 0);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            armedScenarioRequest = null;
            lifetimeCancellationSource.Cancel();
        }
    }

    private async Task RunScenarioAsync(ScenarioSelection selection, CancellationToken cancellationToken)
    {
        if (!runGate.Wait(0))
            return;

        State.SetRunning();
        try
        {
            using var runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellationSource.Token);
            var scenario = registry.GetRequired(selection.ScenarioId);
            var context = contextFactory.Create(selection.Route, selection.Mode);
            var report = await runner.RunAsync(scenario, context, runCancellationSource.Token);
            State.SetCompleted(report.IsSuccess ? "Passed" : "Failed", report.ArtifactPaths);
        }
        catch (OperationCanceledException)
        {
            State.SetCompleted("Cancelled", Array.Empty<string>());
        }
        catch (Exception)
        {
            State.SetCompleted("Failed", Array.Empty<string>());
        }
        finally
        {
            runGate.Release();
        }
    }

    private ScenarioSelection CreateSelectionSnapshot() =>
        State.SelectedScenarioId is { Length: > 0 } scenarioId
            ? new ScenarioSelection(scenarioId, State.SelectedRoute, State.SelectedMode)
            : throw new InvalidOperationException("A validation scenario must be selected before running.");

    private bool TryGetSelectedArmableScenario(out IArmableValidationScenario? scenario)
    {
        if (State.SelectedScenarioId is null
            || !registry.TryGet(State.SelectedScenarioId, out var selectedScenario)
            || selectedScenario is not IArmableValidationScenario armableScenario)
        {
            scenario = null;
            return false;
        }

        scenario = armableScenario;
        return true;
    }

    private sealed record ScenarioSelection(string ScenarioId, ValidationRoute Route, ValidationMode Mode);
    private sealed record ArmedScenarioRequest(ScenarioSelection Selection, DateTimeOffset DeadlineUtc);
}

public interface IScenarioExecutionContextFactory
{
    ScenarioExecutionContext Create(ValidationRoute route, ValidationMode mode);
}

public sealed class ValidationScenarioContextFactory(string evidenceRoot) : IScenarioExecutionContextFactory
{
    private readonly string evidenceRoot = Path.GetFullPath(evidenceRoot);

    public ScenarioExecutionContext Create(ValidationRoute route, ValidationMode mode) =>
        new(route, mode, new Dictionary<string, string?>(), evidenceRoot);
}
