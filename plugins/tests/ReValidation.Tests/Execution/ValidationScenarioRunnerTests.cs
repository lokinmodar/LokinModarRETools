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

    [Fact]
    public async Task FullProof_RestoresWithActiveCleanupToken_AfterCallerCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var cleanupTokenWasCancelled = true;
        var scenario = new RecordingScenario(assertAction: cancellationSource.Cancel, restore: cancellationToken =>
        {
            cleanupTokenWasCancelled = cancellationToken.IsCancellationRequested;
            return new ScenarioRestoreResult(true, "restore", []);
        });
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, cancellationSource.Token);

        Assert.False(cleanupTokenWasCancelled);
        Assert.NotNull(report.RestoreResult);
    }

    [Fact]
    public async Task FullProof_RecordsRestoreException()
    {
        var scenario = new RecordingScenario(restore: _ => throw new InvalidOperationException("restore failed"));
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("restore", report.FailedPhase);
        Assert.IsType<InvalidOperationException>(report.Exception);
    }

    [Fact]
    public async Task FullProof_RecordsRestoreTimeout()
    {
        var scenario = new RecordingScenario(restore: cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ScenarioRestoreResult(true, "restore", []);
        });
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), TimeSpan.Zero, new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("restore", report.FailedPhase);
        Assert.IsType<OperationCanceledException>(report.Exception);
    }

    [Fact]
    public async Task FullProof_RecordsFailedRestoreResult()
    {
        var scenario = new RecordingScenario(restore: _ => new ScenarioRestoreResult(false, "restore failed", ["unhook failed"]));
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("restore", report.FailedPhase);
        Assert.False(report.RestoreResult!.Passed);
    }

    [Fact]
    public async Task Compare_ReportsMismatchAsFailure()
    {
        var scenario = new RecordingScenario(compareMatches: false);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.Compare);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("compare", report.FailedPhase);
    }

    [Theory]
    [InlineData(ValidationMode.OverrideAssert)]
    [InlineData(ValidationMode.FullProof)]
    public async Task OverrideModes_ReportNullAssertAsFailure(ValidationMode mode)
    {
        var scenario = new RecordingScenario(returnsNullAssert: true);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, mode);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("assert", report.FailedPhase);
    }

    [Fact]
    public async Task CaptureOnly_AppendsAllEvidenceWriterOutputs()
    {
        var first = new RecordingEvidenceWriter("json", "report.json");
        var second = new RecordingEvidenceWriter("markdown", "report.md");
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), first, second);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var report = await runner.RunAsync(new RecordingScenario(), context, CancellationToken.None);

        Assert.Equal(new[] { "json", "markdown" }, report.Evidence.Select(evidence => evidence.Kind));
        Assert.Equal(1, first.WriteCount);
        Assert.Equal(1, second.WriteCount);
    }

    private sealed class RecordingScenario : IValidationScenario
    {
        private readonly bool assertThrows;
        private readonly bool compareMatches;
        private readonly bool returnsNullAssert;
        private readonly Action? assertAction;
        private readonly Func<CancellationToken, ScenarioRestoreResult>? restore;

        public static RecordingScenario? Current { get; private set; }

        public RecordingScenario(
            bool assertThrows = false,
            bool compareMatches = true,
            bool returnsNullAssert = false,
            Action? assertAction = null,
            Func<CancellationToken, ScenarioRestoreResult>? restore = null)
        {
            this.assertThrows = assertThrows;
            this.compareMatches = compareMatches;
            this.returnsNullAssert = returnsNullAssert;
            this.assertAction = assertAction;
            this.restore = restore;
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
            return ValueTask.FromResult<ScenarioCompareResult?>(new ScenarioCompareResult(compareMatches, "compare", []));
        }

        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
        {
            Calls.Add("override");
            return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket("override", new JsonObject()));
        }

        public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
        {
            Calls.Add("assert");
            assertAction?.Invoke();
            if (assertThrows)
                throw new InvalidOperationException("assert failed");

            if (returnsNullAssert)
                return ValueTask.FromResult<ScenarioAssertResult?>(null);

            return ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(true, "assert", []));
        }

        public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
        {
            Calls.Add("restore");
            return ValueTask.FromResult(restore?.Invoke(cancellationToken) ?? new ScenarioRestoreResult(true, "restore", []));
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

    private sealed class RecordingEvidenceWriter(string kind, string outputPath) : IEvidenceWriter
    {
        public int WriteCount { get; private set; }

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            WriteCount++;
            return ValueTask.FromResult(new EvidenceWriteResult(kind, outputPath));
        }
    }
}
