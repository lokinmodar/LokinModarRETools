using ReValidation.Common.Abstractions;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;

namespace ReValidation.Common.UI;

public sealed class ValidationWindowController : IDisposable
{
    private readonly ValidationScenarioRegistry registry;
    private readonly IValidationScenarioRunner runner;
    private readonly IScenarioExecutionContextFactory contextFactory;
    private readonly CancellationTokenSource lifetimeCancellationSource = new();
    private readonly SemaphoreSlim runGate = new(1, 1);
    private int disposed;

    public ValidationWindowController(
        ValidationWindowState state,
        ValidationScenarioRegistry registry,
        IValidationScenarioRunner runner,
        IScenarioExecutionContextFactory? contextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(runner);

        State = state;
        this.registry = registry;
        this.runner = runner;
        this.contextFactory = contextFactory ?? new ValidationScenarioContextFactory(Path.GetTempPath());
    }

    public ValidationWindowState State { get; }
    public IReadOnlyCollection<IValidationScenario> Scenarios => registry.Scenarios;

    public async Task RunSelectedScenarioAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (!runGate.Wait(0))
            return;

        State.SetRunning();
        try
        {
            using var runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetimeCancellationSource.Token);
            var scenario = registry.GetRequired(State.SelectedScenarioId!);
            var context = contextFactory.Create(State.SelectedRoute, State.SelectedMode);
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
            lifetimeCancellationSource.Cancel();
    }
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
