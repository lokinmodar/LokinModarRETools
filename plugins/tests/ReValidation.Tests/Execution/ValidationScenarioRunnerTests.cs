using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using Xunit;

public sealed class ValidationScenarioRunnerTests
{
    [Fact]
    public async Task CaptureOnly_StopsAfterCapture()
    {
        var scenario = new RecordingScenario();
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.Equal(new[] { "validate", "capture", "export" }, scenario.Calls);
        Assert.Null(report.CompareResult);
        Assert.Null(report.OverrideResult);
        Assert.Null(report.AssertResult);
        Assert.Null(report.RestoreResult);
    }

    [Fact]
    public async Task FullProof_RunsRestore_WhenAssertFails()
    {
        var scenario = new RecordingScenario(assertThrows: true);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.Equal(new[] { "validate", "capture", "compare", "override", "assert", "restore", "export" }, scenario.Calls);
        Assert.Equal("assert", report.FailedPhase);
        Assert.NotNull(report.RestoreResult);
        Assert.False(report.IsSuccess);
    }

    private sealed class RecordingScenario : IValidationScenario
    {
        private readonly bool assertThrows;

        public static RecordingScenario? Current { get; private set; }

        public RecordingScenario(bool assertThrows = false)
        {
            this.assertThrows = assertThrows;
        }

        public List<string> Calls { get; } = [];
        public ValidationScenarioDefinition Definition { get; } = new("recording", "Recording scenario");

        public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            Current = this;
            Calls.Add("validate");
            return ValueTask.FromResult(new ScenarioPreconditionResult(true, null));
        }

        public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            Calls.Add("capture");
            return ValueTask.FromResult(new ScenarioCapture("capture", new JsonObject()));
        }

        public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
        {
            Calls.Add("compare");
            return ValueTask.FromResult<ScenarioCompareResult?>(new ScenarioCompareResult(true, "compare", []));
        }

        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
        {
            Calls.Add("override");
            return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket("override", new JsonObject()));
        }

        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
        {
            Calls.Add("assert");
            if (assertThrows)
                throw new InvalidOperationException("assert failed");

            return ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(true, "assert", []));
        }

        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
        {
            Calls.Add("restore");
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "restore", []));
        }
    }

    private sealed class NullRouteMetadataProvider : IRouteMetadataProvider
    {
        public ValidationRoute Route => ValidationRoute.OwnerSignatures;

        public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }

    private sealed class NullEvidenceWriter : IEvidenceWriter
    {
        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            RecordingScenario.Current!.Calls.Add("export");
            return ValueTask.FromResult(new EvidenceWriteResult("null", string.Empty));
        }
    }
}
