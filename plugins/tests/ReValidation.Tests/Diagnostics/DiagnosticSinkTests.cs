using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.UI;
using Xunit;

namespace ReValidation.Tests.Diagnostics;

public sealed class DiagnosticSinkTests
{
    [Fact]
    public async Task Runner_EmitsBlockedAndSuccessDiagnostics()
    {
        var sink = new RecordingDiagnosticsSink();
        var blockedRunner = new ValidationScenarioRunner(new NullRouteMetadataProvider(ValidationRoute.LocalClientStructs), sink, new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.Compare);

        await blockedRunner.RunAsync(new BlockedScenario("Journal comparison reference source is required."), context, CancellationToken.None);

        Assert.Contains(sink.Messages, message => message.Contains("run.start scenario=journal.completed-entries route=LocalClientStructs mode=Compare", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("run.blocked scenario=journal.completed-entries phase=validate reason=\"Journal comparison reference source is required.\"", StringComparison.Ordinal));

        sink.Messages.Clear();

        var successRunner = new ValidationScenarioRunner(new NullRouteMetadataProvider(ValidationRoute.OwnerSignatures), sink, new NullEvidenceWriter());
        await successRunner.RunAsync(new CaptureOnlyScenario(), ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly), CancellationToken.None);

        Assert.Contains(sink.Messages, message => message.Contains("capture.summary scenario=tooltip.item-detail summary=\"item tooltip captured\"", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("run.completed scenario=tooltip.item-detail status=success", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Controller_EmitsArmLifecycleDiagnostics()
    {
        var sink = new RecordingDiagnosticsSink();
        var scenario = new ArmableScenario(
            new ScenarioArmState(false, "ActionDetail addon is not visible."),
            new ScenarioArmState(true, "Tooltip cue ready."));
        var controller = new ValidationWindowController(
            new ValidationWindowState(),
            ValidationScenarioRegistry.ForTests(scenario),
            new StubRunner(),
            diagnosticsSink: sink,
            timeProvider: new FakeTimeProvider());
        controller.State.SelectScenario("tooltip.action-detail");
        controller.State.SelectRoute(ValidationRoute.OwnerSignatures);

        await controller.ArmSelectedScenarioAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
        await controller.PulseArmedScenarioAsync(CancellationToken.None);
        await controller.PulseArmedScenarioAsync(CancellationToken.None);

        Assert.Contains(sink.Messages, message => message.Contains("arm.requested scenario=tooltip.action-detail route=OwnerSignatures mode=CaptureOnly timeoutSeconds=10", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("arm.armed scenario=tooltip.action-detail", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("arm.waiting scenario=tooltip.action-detail status=\"ActionDetail addon is not visible.\"", StringComparison.Ordinal));
        Assert.Contains(sink.Messages, message => message.Contains("arm.ready scenario=tooltip.action-detail status=\"Tooltip cue ready.\"", StringComparison.Ordinal));
    }

    private sealed class RecordingDiagnosticsSink : IValidationDiagnosticsSink
    {
        public List<string> Messages { get; } = [];
        public void Debug(string message) => Messages.Add(message);
    }

    private sealed class NullRouteMetadataProvider(ValidationRoute route) : IRouteMetadataProvider
    {
        public ValidationRoute Route => route;

        public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }

    private sealed class NullEvidenceWriter : IEvidenceWriter
    {
        public string Kind => "null";

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EvidenceWriteResult("null", string.Empty));
    }

    private sealed class BlockedScenario(string reason) : IValidationScenario
    {
        public ValidationScenarioDefinition Definition { get; } = new("journal.completed-entries", "Journal Completed Entries");

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(false, reason));

        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CaptureOnlyScenario : IValidationScenario
    {
        public ValidationScenarioDefinition Definition { get; } = new("tooltip.item-detail", "Tooltip Item Detail");

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(true, null));

        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioCapture("item tooltip captured", new System.Text.Json.Nodes.JsonObject()));

        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ArmableScenario(params ScenarioArmState[] cueStates) : IValidationScenario, IArmableValidationScenario
    {
        private readonly Queue<ScenarioArmState> cueStates = new(cueStates);

        public ValidationScenarioDefinition Definition { get; } = new("tooltip.action-detail", "Tooltip Action Detail");
        public string ArmPrompt => "Hover the tooltip in game.";

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioPreconditionResult(true, null));

        public ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(cueStates.Dequeue());

        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubRunner : IValidationScenarioRunner
    {
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode) with
            {
                Capture = new ScenarioCapture("action tooltip captured", new System.Text.Json.Nodes.JsonObject()),
                IsSuccess = true,
            });
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 07, 27, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
