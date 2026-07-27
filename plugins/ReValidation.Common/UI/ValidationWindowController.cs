using ReValidation.Common.Abstractions;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;

namespace ReValidation.Common.UI;

public sealed class ValidationWindowController : IDisposable
{
    private readonly IValidationDiagnosticsSink diagnosticsSink;
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
        IValidationDiagnosticsSink? diagnosticsSink = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(runner);

        State = state;
        this.diagnosticsSink = diagnosticsSink ?? NullValidationDiagnosticsSink.Instance;
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

    public async Task ArmSelectedScenarioAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (State.IsBusy)
            return;
        if (!TryGetSelectedArmableScenario(out var armableScenario))
            throw new InvalidOperationException("The selected scenario does not support arming.");

        var selection = CreateSelectionSnapshot();
        diagnosticsSink.Debug($"arm.requested scenario={selection.ScenarioId} route={selection.Route} mode={selection.Mode} timeoutSeconds={(int)timeout.TotalSeconds}");
        var scenario = registry.GetRequired(selection.ScenarioId);
        var context = contextFactory.Create(selection.Route, selection.Mode);
        var precondition = await scenario.ValidateAsync(context, cancellationToken);
        if (!precondition.CanRun)
        {
            diagnosticsSink.Debug($"arm.blocked scenario={selection.ScenarioId} phase=validate reason=\"{Escape(precondition.BlockingReason)}\"");
            State.SetCompleted("Blocked", precondition.BlockingReason, Array.Empty<string>());
            return;
        }

        armedScenarioRequest = new ArmedScenarioRequest(
            selection,
            timeProvider.GetUtcNow().Add(timeout),
            LastObservedStatus: null);
        diagnosticsSink.Debug($"arm.armed scenario={selection.ScenarioId} prompt=\"{Escape(armableScenario!.ArmPrompt)}\"");
        State.SetArmed("Armed", armableScenario!.ArmPrompt);
    }

    public void DisarmSelectedScenario()
    {
        if (armedScenarioRequest is not null)
            diagnosticsSink.Debug($"arm.disarmed scenario={armedScenarioRequest.Selection.ScenarioId}");
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
                diagnosticsSink.Debug($"arm.timeout scenario={request.Selection.ScenarioId}");
                State.SetCompleted("Timed out", "Tooltip cue did not become ready within the arm window.", Array.Empty<string>());
                return;
            }

            var scenario = registry.GetRequired(request.Selection.ScenarioId);
            if (scenario is not IArmableValidationScenario armableScenario)
            {
                armedScenarioRequest = null;
                diagnosticsSink.Debug($"arm.failed scenario={request.Selection.ScenarioId} reason=\"Selected scenario no longer supports arming.\"");
                State.SetCompleted("Failed", string.Empty, Array.Empty<string>());
                return;
            }

            var cue = await armableScenario.PollArmCueAsync(pulseCancellationSource.Token);
            if (!cue.IsReady)
            {
                if (!string.Equals(request.LastObservedStatus, cue.StatusText, StringComparison.Ordinal))
                    diagnosticsSink.Debug($"arm.waiting scenario={request.Selection.ScenarioId} status=\"{Escape(cue.StatusText)}\"");

                armedScenarioRequest = request with { LastObservedStatus = cue.StatusText };
                State.UpdateArmedStatus(cue.StatusText);
                return;
            }

            armedScenarioRequest = null;
            diagnosticsSink.Debug($"arm.ready scenario={request.Selection.ScenarioId} status=\"{Escape(cue.StatusText)}\"");
            await RunScenarioAsync(request.Selection, pulseCancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            armedScenarioRequest = null;
            diagnosticsSink.Debug("arm.cancelled");
            State.SetCompleted("Cancelled", string.Empty, Array.Empty<string>());
        }
        catch (Exception)
        {
            armedScenarioRequest = null;
            diagnosticsSink.Debug("arm.failed");
            State.SetCompleted("Failed", string.Empty, Array.Empty<string>());
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
            State.SetCompleted(GetStatusText(report), report.Summary, report.ArtifactPaths);
        }
        catch (OperationCanceledException)
        {
            State.SetCompleted("Cancelled", string.Empty, Array.Empty<string>());
        }
        catch (Exception)
        {
            State.SetCompleted("Failed", string.Empty, Array.Empty<string>());
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

    private static string GetStatusText(ScenarioRunReport report)
    {
        if (report.IsSuccess)
            return "Passed";

        return report.Precondition is { CanRun: false } && string.Equals(report.FailedPhase, "validate", StringComparison.Ordinal)
            ? "Blocked"
            : "Failed";
    }

    private sealed record ScenarioSelection(string ScenarioId, ValidationRoute Route, ValidationMode Mode);
    private sealed record ArmedScenarioRequest(ScenarioSelection Selection, DateTimeOffset DeadlineUtc, string? LastObservedStatus);

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
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
