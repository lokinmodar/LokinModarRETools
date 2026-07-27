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
        public Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            Task.FromResult(
                ScenarioRunReport.CreateForTests(scenario.Definition.Id, context.Route, context.Mode)
                    .WithEvidence(new EvidenceWriteResult("json", "evidence.json"))
                    .WithEvidence(new EvidenceWriteResult("markdown", "evidence.md")));
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
}
