using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.UI;
using Xunit;

public sealed class ValidationWindowControllerTests
{
    [Fact]
    public async Task RunSelectedScenario_PublishesStatusAndArtifactPaths()
    {
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(new StubScenario("journal.completed-entries")),
            runner: new StubRunner());

        controller.State.SelectScenario("journal.completed-entries");
        controller.State.SelectRoute(ValidationRoute.OwnerSignatures);
        controller.State.SelectMode(ValidationMode.FullProof);

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Passed", controller.State.StatusText);
        Assert.Equal(2, controller.State.ArtifactPaths.Count);
        Assert.Contains(controller.State.ArtifactPaths, path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunSelectedScenario_RunnerThrows_PublishesFailedStatus()
    {
        var controller = CreateController(new ThrowingRunner());

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Failed", controller.State.StatusText);
        Assert.Empty(controller.State.ArtifactPaths);
    }

    [Fact]
    public async Task RunSelectedScenario_RunnerCancels_PublishesCancelledStatus()
    {
        var controller = CreateController(new CancellingRunner());

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Cancelled", controller.State.StatusText);
        Assert.Empty(controller.State.ArtifactPaths);
    }

    [Fact]
    public async Task RunSelectedScenario_AllowsOnlyOneActiveRun()
    {
        var runner = new BlockingRunner();
        using var controller = CreateController(runner);

        var firstRun = controller.RunSelectedScenarioAsync(CancellationToken.None);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal(1, runner.RunCount);

        runner.Complete();
        await firstRun;
    }

    [Fact]
    public async Task Dispose_CancelsActiveRun()
    {
        var runner = new BlockingRunner();
        var controller = CreateController(runner);
        var run = controller.RunSelectedScenarioAsync(CancellationToken.None);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        controller.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(runner.ObservedCancellation);
        Assert.Equal("Cancelled", controller.State.StatusText);
    }

    [Fact]
    public async Task ArmSelectedScenario_ReadyCue_RunsScenarioAndPublishesArtifacts()
    {
        var scenario = new ArmableStubScenario(
            "tooltip.item-detail",
            new ScenarioArmState(false, "Waiting for tooltip cue."),
            new ScenarioArmState(true, "Tooltip cue ready."));
        var runner = new StubRunner();
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: runner,
            timeProvider: new FakeTimeProvider());

        controller.State.SelectScenario("tooltip.item-detail");

        Assert.True(controller.CanArmSelectedScenario());

        controller.ArmSelectedScenario(TimeSpan.FromSeconds(10));
        Assert.True(controller.State.IsArmed);
        Assert.Equal("Armed", controller.State.StatusText);

        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.True(controller.State.IsArmed);
        Assert.Equal("Waiting for tooltip cue.", controller.State.StatusText);
        Assert.Equal(0, runner.RunCount);

        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.Equal("Passed", controller.State.StatusText);
        Assert.Equal(2, controller.State.ArtifactPaths.Count);
        Assert.Equal(2, scenario.PollCount);
        Assert.Equal(1, runner.RunCount);
    }

    [Fact]
    public async Task ArmSelectedScenario_TimesOut_WhenCueNeverBecomesReady()
    {
        var scenario = new ArmableStubScenario(
            "tooltip.item-detail",
            new ScenarioArmState(false, "Waiting for tooltip cue."));
        var runner = new StubRunner();
        var timeProvider = new FakeTimeProvider();
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: runner,
            timeProvider: timeProvider);

        controller.State.SelectScenario("tooltip.item-detail");
        controller.ArmSelectedScenario(TimeSpan.FromSeconds(5));

        await controller.PulseArmedScenarioAsync(CancellationToken.None);
        Assert.True(controller.State.IsArmed);

        timeProvider.Advance(TimeSpan.FromSeconds(6));
        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.Equal("Timed out", controller.State.StatusText);
        Assert.Empty(controller.State.ArtifactPaths);
        Assert.Equal(0, runner.RunCount);
    }

    [Fact]
    public async Task DisarmSelectedScenario_StopsPendingArmWithoutRunning()
    {
        var scenario = new ArmableStubScenario(
            "tooltip.item-detail",
            new ScenarioArmState(true, "Tooltip cue ready."));
        var runner = new StubRunner();
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: runner,
            timeProvider: new FakeTimeProvider());

        controller.State.SelectScenario("tooltip.item-detail");
        controller.ArmSelectedScenario(TimeSpan.FromSeconds(10));

        controller.DisarmSelectedScenario();
        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.False(controller.State.IsRunning);
        Assert.Equal("Idle", controller.State.StatusText);
        Assert.Equal(0, runner.RunCount);
        Assert.Equal(0, scenario.PollCount);
    }

    [Fact]
    public void CanArmSelectedScenario_IsFalse_ForNonArmableScenario()
    {
        var controller = CreateController(new StubRunner());

        Assert.False(controller.CanArmSelectedScenario());
    }

    private static ValidationWindowController CreateController(IValidationScenarioRunner runner)
    {
        var controller = new ValidationWindowController(
            new ValidationWindowState(),
            ValidationScenarioRegistry.ForTests(new StubScenario("journal.completed-entries")),
            runner);
        controller.State.SelectScenario("journal.completed-entries");
        return controller;
    }

    private sealed class StubRunner : IValidationScenarioRunner
    {
        public int RunCount { get; private set; }

        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(CreateReport(scenario, context));

        private ScenarioRunReport CreateReport(IValidationScenario scenario, ScenarioExecutionContext context)
        {
            RunCount++;
            return ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode)
                .WithEvidence(new EvidenceWriteResult("json", "evidence.json"))
                .WithEvidence(new EvidenceWriteResult("markdown", "evidence.md"));
        }
    }

    private sealed class BlockingRunner : IValidationScenarioRunner
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunCount { get; private set; }
        public bool ObservedCancellation { get; private set; }

        public async Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            RunCount++;
            Started.TrySetResult();
            try
            {
                await completion.Task.WaitAsync(cancellationToken);
                return ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode);
            }
            catch (OperationCanceledException)
            {
                ObservedCancellation = true;
                throw;
            }
        }

        public void Complete() => completion.TrySetResult();
    }

    private sealed class ThrowingRunner : IValidationScenarioRunner
    {
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromException<ScenarioRunReport>(new InvalidOperationException("runner failed"));
    }

    private sealed class CancellingRunner : IValidationScenarioRunner
    {
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromCanceled<ScenarioRunReport>(new CancellationToken(canceled: true));
    }

    private sealed class StubScenario(string id) : IValidationScenario
    {
        public ValidationScenarioDefinition Definition { get; } = new(id, id);

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ArmableStubScenario(string id, params ScenarioArmState[] cueStates) : IValidationScenario, IArmableValidationScenario
    {
        private readonly Queue<ScenarioArmState> cueStates = new(cueStates);

        public ValidationScenarioDefinition Definition { get; } = new(id, id);
        public string ArmPrompt => "Hover the tooltip in game.";
        public int PollCount { get; private set; }

        public ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken)
        {
            PollCount++;
            if (cueStates.Count == 0)
                return ValueTask.FromResult(new ScenarioArmState(false, "Waiting for tooltip cue."));

            return ValueTask.FromResult(cueStates.Dequeue());
        }

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 07, 27, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }
}
