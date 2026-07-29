using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Discovery;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
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
        Assert.Equal("item tooltip captured", controller.State.StatusDetailText);
        Assert.Equal(2, controller.State.ArtifactPaths.Count);
        Assert.Contains(controller.State.ArtifactPaths, path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunTooltipBranchValidationAsync_PublishesStatusAndEvidence_WhenWorkflowConfigured()
    {
        var discoveryOptions = new ClientStructsDiscoveryOptions(@"C:\ffxivclientstructs", DiscoveryMode.Diff, [TargetFamily.ItemTooltip]);
        var catalog = new DiscoveredTargetCatalog(
            discoveryOptions,
            [
                new DiscoveredTarget(
                    "AddonItemDetail.GenerateTooltip",
                    "AddonItemDetail",
                    "GenerateTooltip",
                    BindingKind.MemberFunction,
                    "48 89",
                    "AddonItemDetail.cs",
                    true,
                    TargetFamily.ItemTooltip,
                    CueFamily.TooltipItemDetail,
                    ProofProfile.DetourFunction,
                    4),
            ],
            []);
        var branchRunner = new StubBranchRunner();
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(new StubScenario("journal.completed-entries")),
            runner: new StubRunner(),
            branchWorkflow: new BranchValidationWorkflow(
                new StubDiscoveryService(catalog),
                new ProofPlanBuilder(),
                branchRunner,
                new StubBranchRouteAdapter(ValidationRoute.LocalClientStructs),
                discoveryOptions,
                @"C:\evidence"));

        Assert.True(controller.CanRunBranchValidation);

        await controller.RunTooltipBranchValidationAsync(CancellationToken.None);

        Assert.Equal("Passed", controller.State.StatusText);
        Assert.Equal("Tooltip branch validation passed.", controller.State.StatusDetailText);
        Assert.Equal(2, controller.State.ArtifactPaths.Count);
        Assert.Equal(4, branchRunner.ObservedRequiredProofLevel);
        Assert.Equal(ValidationRoute.LocalClientStructs, branchRunner.ObservedRoute);
    }

    [Fact]
    public async Task RunSelectedScenario_RunnerThrows_PublishesFailedStatus()
    {
        var controller = CreateController(new ThrowingRunner());

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Failed", controller.State.StatusText);
        Assert.Empty(controller.State.StatusDetailText);
        Assert.Empty(controller.State.ArtifactPaths);
    }

    [Fact]
    public async Task RunSelectedScenario_RunnerCancels_PublishesCancelledStatus()
    {
        var controller = CreateController(new CancellingRunner());

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Cancelled", controller.State.StatusText);
        Assert.Empty(controller.State.StatusDetailText);
        Assert.Empty(controller.State.ArtifactPaths);
    }

    [Fact]
    public async Task RunSelectedScenario_BlockedPrecondition_PublishesBlockedStatusAndReason()
    {
        var controller = CreateController(new BlockingReportRunner("Tooltip comparison reference source is required."));

        await controller.RunSelectedScenarioAsync(CancellationToken.None);

        Assert.Equal("Blocked", controller.State.StatusText);
        Assert.Equal("Tooltip comparison reference source is required.", controller.State.StatusDetailText);
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
        Assert.Empty(controller.State.StatusDetailText);
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

        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.True(controller.State.IsArmed);
        Assert.Equal("Armed", controller.State.StatusText);
        Assert.Equal("Hover the tooltip in game.", controller.State.StatusDetailText);

        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.True(controller.State.IsArmed);
        Assert.Equal("Waiting for tooltip cue.", controller.State.StatusText);
        Assert.Equal("Hover the tooltip in game.", controller.State.StatusDetailText);
        Assert.Equal(0, runner.RunCount);

        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.Equal("Passed", controller.State.StatusText);
        Assert.Equal("item tooltip captured", controller.State.StatusDetailText);
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
        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await controller.PulseArmedScenarioAsync(CancellationToken.None);
        Assert.True(controller.State.IsArmed);

        timeProvider.Advance(TimeSpan.FromSeconds(6));
        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.Equal("Timed out", controller.State.StatusText);
        Assert.Equal("Tooltip cue did not become ready within the arm window.", controller.State.StatusDetailText);
        Assert.Empty(controller.State.ArtifactPaths);
        Assert.Equal(0, runner.RunCount);
        Assert.Equal(1, scenario.DisarmCount);
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
        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        controller.DisarmSelectedScenario();
        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.False(controller.State.IsRunning);
        Assert.Equal("Idle", controller.State.StatusText);
        Assert.Empty(controller.State.StatusDetailText);
        Assert.Equal(0, runner.RunCount);
        Assert.Equal(0, scenario.PollCount);
        Assert.Equal(1, scenario.DisarmCount);
    }

    [Fact]
    public async Task PulseArmedScenario_Cancellation_DisarmsPendingScenario()
    {
        var scenario = new ArmableStubScenario("tooltip.item-detail", new ScenarioArmState(false, "Waiting for tooltip cue."));
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: new StubRunner(),
            timeProvider: new FakeTimeProvider());
        controller.State.SelectScenario("tooltip.item-detail");
        using var cancellation = new CancellationTokenSource();

        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        cancellation.Cancel();
        await controller.PulseArmedScenarioAsync(cancellation.Token);

        Assert.Equal("Cancelled", controller.State.StatusText);
        Assert.Equal(1, scenario.DisarmCount);
    }

    [Fact]
    public async Task Dispose_DisarmsPendingScenario()
    {
        var scenario = new ArmableStubScenario("tooltip.item-detail", new ScenarioArmState(false, "Waiting for tooltip cue."));
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: new StubRunner(),
            timeProvider: new FakeTimeProvider());
        controller.State.SelectScenario("tooltip.item-detail");

        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        controller.Dispose();

        Assert.Equal(1, scenario.DisarmCount);
    }

    [Fact]
    public async Task ArmSelectedScenario_BlockedPrecondition_PublishesBlockedStatusBeforePolling()
    {
        var scenario = new BlockingArmableScenario("tooltip.item-detail", "Tooltip comparison reference source is required.");
        var runner = new StubRunner();
        var controller = new ValidationWindowController(
            state: new ValidationWindowState(),
            registry: ValidationScenarioRegistry.ForTests(scenario),
            runner: runner,
            timeProvider: new FakeTimeProvider());

        controller.State.SelectScenario("tooltip.item-detail");

        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.False(controller.State.IsArmed);
        Assert.Equal("Blocked", controller.State.StatusText);
        Assert.Equal("Tooltip comparison reference source is required.", controller.State.StatusDetailText);
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

    private sealed class StubDiscoveryService(DiscoveredTargetCatalog catalog) : IClientStructsDiscoveryService
    {
        public ValueTask<DiscoveredTargetCatalog> DiscoverAsync(ClientStructsDiscoveryOptions options, CancellationToken cancellationToken) =>
            ValueTask.FromResult(catalog);
    }

    private sealed class StubRunner : IValidationScenarioRunner
    {
        public int RunCount { get; private set; }

        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(CreateReport(scenario, context));

        private ScenarioRunReport CreateReport(IValidationScenario scenario, ScenarioExecutionContext context)
        {
            RunCount++;
            var report = ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode) with
            {
                Capture = new ScenarioCapture("item tooltip captured", new JsonObject()),
                IsSuccess = true,
            };

            return report
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

    private sealed class BlockingReportRunner(string reason) : IValidationScenarioRunner
    {
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(
                ScenarioRunReport.Started(scenario.Definition, context.Route, context.Mode)
                    .WithPrecondition(new ScenarioPreconditionResult(false, reason))
                    .MarkBlocked());
    }

    private sealed class StubBranchRunner : IBranchValidationRunner
    {
        public ValidationRoute ObservedRoute { get; private set; }
        public int ObservedRequiredProofLevel { get; private set; }

        public ValueTask<BranchValidationRunReport> RunAsync(
            BranchValidationPlan plan,
            IBranchValidationRouteAdapter routeAdapter,
            string evidenceRoot,
            CancellationToken cancellationToken)
        {
            ObservedRoute = routeAdapter.Route;
            ObservedRequiredProofLevel = plan.RequiredProofLevel;

            var groups = plan.Groups
                .Select(group => new ProofGroupRunReport(
                    group.GroupId,
                    group.Targets.Select(target => new TargetProofRecord(target.TargetId, "passed", 1, 0x1234, 1, true, true, true, null)).ToArray(),
                    "ok"))
                .ToArray();

            return ValueTask.FromResult(
                new BranchValidationRunReport(routeAdapter.Route, plan.RequiredProofLevel, groups)
                {
                    Evidence =
                    [
                        new EvidenceWriteResult("json", Path.Combine(evidenceRoot, "branch.json")),
                        new EvidenceWriteResult("markdown", Path.Combine(evidenceRoot, "branch.md")),
                    ],
                });
        }
    }

    private sealed class StubBranchRouteAdapter(ValidationRoute route) : IBranchValidationRouteAdapter
    {
        public ValidationRoute Route { get; } = route;

        public ValueTask<ProofGroupRunReport> RunProofGroupAsync(
            ProofGroupDefinition group,
            int requiredProofLevel,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubScenario(string id) : IValidationScenario
    {
        public ValidationScenarioDefinition Definition { get; } = new(id, id);

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(true, null));
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
        public int ArmCount { get; private set; }
        public int DisarmCount { get; private set; }

        public ValueTask ArmAsync(CancellationToken cancellationToken)
        {
            ArmCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisarmAsync(CancellationToken cancellationToken)
        {
            DisarmCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PollCount++;
            if (cueStates.Count == 0)
                return ValueTask.FromResult(new ScenarioArmState(false, "Waiting for tooltip cue."));

            return ValueTask.FromResult(cueStates.Dequeue());
        }

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(true, null));
        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class BlockingArmableScenario(string id, string reason) : IValidationScenario, IArmableValidationScenario
    {
        public ValidationScenarioDefinition Definition { get; } = new(id, id);
        public string ArmPrompt => "Hover the tooltip in game.";
        public int PollCount { get; private set; }

        public ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken)
        {
            PollCount++;
            return ValueTask.FromResult(new ScenarioArmState(true, "Tooltip cue ready."));
        }

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(false, reason));

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
