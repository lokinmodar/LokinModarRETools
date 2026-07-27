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
    public async Task FullProof_BoundsNonCooperativeRestore()
    {
        var restoreCanFinish = new TaskCompletionSource<ScenarioRestoreResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scenario = new RecordingScenario(restoreAsync: _ => new ValueTask<ScenarioRestoreResult>(restoreCanFinish.Task));
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), TimeSpan.Zero, new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        try
        {
            var report = await runner.RunAsync(scenario, context, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(report.IsSuccess);
            Assert.Equal("restore", report.FailedPhase);
            Assert.IsAssignableFrom<OperationCanceledException>(report.Exception);
        }
        finally
        {
            restoreCanFinish.TrySetResult(new ScenarioRestoreResult(true, "restore", []));
        }
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

    [Fact]
    public async Task Compare_ReportsMissingComparisonAsFailure()
    {
        var scenario = new RecordingScenario(returnsNullCompare: true);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.Compare);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("compare", report.FailedPhase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FullProof_RestoresAfterOverrideAttemptEvenWithoutTicket(bool overrideThrows)
    {
        var scenario = new RecordingScenario(overrideThrows: overrideThrows, returnsNullOverride: !overrideThrows);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, CancellationToken.None);

        Assert.Contains("restore", scenario.Calls);
        Assert.NotNull(report.RestoreResult);
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

    [Fact]
    public async Task Export_AttemptsEveryWriterAndReportsSanitizedFailure()
    {
        var failed = new ThrowingEvidenceWriter("json", "sensitive writer detail");
        var markdown = new RecordingEvidenceWriter("markdown", "report.md");
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), failed, markdown);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var report = await runner.RunAsync(new RecordingScenario(), context, CancellationToken.None);

        Assert.Equal(1, failed.WriteCount);
        Assert.Equal(1, markdown.WriteCount);
        Assert.False(report.IsSuccess);
        Assert.Equal("export", report.FailedPhase);
        var failure = Assert.Single(report.Evidence, evidence => !evidence.IsSuccess);
        Assert.Equal("json", failure.Kind);
        Assert.DoesNotContain("sensitive", failure.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_RecordsMetadataFromConfiguredRouteProvider()
    {
        var provider = new RecordingRouteMetadataProvider();
        var runner = new ValidationScenarioRunner(provider, new NullEvidenceWriter());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var report = await runner.RunAsync(new RecordingScenario(), context, CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal("matchCount=1;rva=0x1234", report.RouteMetadata["signature:journal"]);
    }

    [Fact]
    public async Task FullProof_ExportsEvidenceAfterCallerCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var writer = new RecordingEvidenceWriter("json", "report.json", requireActiveToken: true);
        var scenario = new RecordingScenario(assertAction: cancellationSource.Cancel);
        var runner = new ValidationScenarioRunner(new NullRouteMetadataProvider(), writer);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await runner.RunAsync(scenario, context, cancellationSource.Token);

        Assert.Equal(1, writer.WriteCount);
        Assert.Single(report.Evidence);
    }

    private sealed class RecordingScenario : IValidationScenario
    {
        private readonly bool assertThrows;
        private readonly bool compareMatches;
        private readonly bool returnsNullCompare;
        private readonly bool overrideThrows;
        private readonly bool returnsNullOverride;
        private readonly bool returnsNullAssert;
        private readonly Action? assertAction;
        private readonly Func<CancellationToken, ScenarioRestoreResult>? restore;
        private readonly Func<CancellationToken, ValueTask<ScenarioRestoreResult>>? restoreAsync;

        public static RecordingScenario? Current { get; private set; }

        public RecordingScenario(
            bool assertThrows = false,
            bool compareMatches = true,
            bool returnsNullCompare = false,
            bool overrideThrows = false,
            bool returnsNullOverride = false,
            bool returnsNullAssert = false,
            Action? assertAction = null,
            Func<CancellationToken, ScenarioRestoreResult>? restore = null,
            Func<CancellationToken, ValueTask<ScenarioRestoreResult>>? restoreAsync = null)
        {
            this.assertThrows = assertThrows;
            this.compareMatches = compareMatches;
            this.returnsNullCompare = returnsNullCompare;
            this.overrideThrows = overrideThrows;
            this.returnsNullOverride = returnsNullOverride;
            this.returnsNullAssert = returnsNullAssert;
            this.assertAction = assertAction;
            this.restore = restore;
            this.restoreAsync = restoreAsync;
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
            if (returnsNullCompare)
                return ValueTask.FromResult<ScenarioCompareResult?>(null);

            return ValueTask.FromResult<ScenarioCompareResult?>(new ScenarioCompareResult(compareMatches, "compare", []));
        }

        public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
        {
            Calls.Add("override");
            if (overrideThrows)
                throw new InvalidOperationException("override failed after mutation");

            if (returnsNullOverride)
                return ValueTask.FromResult<ScenarioOverrideTicket?>(null);

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
            if (restoreAsync is not null)
                return restoreAsync(cancellationToken);

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
        public string Kind => "null";

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            RecordingScenario.Current!.Calls.Add("export");
            return ValueTask.FromResult(new EvidenceWriteResult("null", string.Empty));
        }
    }

    private sealed class RecordingEvidenceWriter(string kind, string outputPath, bool requireActiveToken = false) : IEvidenceWriter
    {
        public int WriteCount { get; private set; }
        public string Kind => kind;

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            if (requireActiveToken)
                cancellationToken.ThrowIfCancellationRequested();

            WriteCount++;
            return ValueTask.FromResult(new EvidenceWriteResult(kind, outputPath));
        }
    }

    private sealed class ThrowingEvidenceWriter(string kind, string message) : IEvidenceWriter
    {
        public int WriteCount { get; private set; }
        public string Kind => kind;

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
        {
            WriteCount++;
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingRouteMetadataProvider : IRouteMetadataProvider
    {
        public int CallCount { get; private set; }
        public ValidationRoute Route => ValidationRoute.OwnerSignatures;

        public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(
                new Dictionary<string, string?> { ["signature:journal"] = "matchCount=1;rva=0x1234" });
        }
    }
}
