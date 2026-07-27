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

    private sealed class StubRunner : IValidationScenarioRunner
    {
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(
                ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode)
                    .WithEvidence(new EvidenceWriteResult("json", "evidence.json"))
                    .WithEvidence(new EvidenceWriteResult("markdown", "evidence.md")));
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
}
