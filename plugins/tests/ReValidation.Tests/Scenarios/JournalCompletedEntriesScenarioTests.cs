using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Scenarios;
using ReValidation.LocalClientStructs.Services;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Scenarios;

public sealed class JournalCompletedEntriesScenarioTests
{
    [Fact]
    public void Compare_Fails_WhenEntryTextDiffers()
    {
        var local = new JournalCompletedEntriesSnapshot(
            [new JournalEntryRecord(1, 0, "The Company You Keep", "65632", "local")],
            "1 entry");
        var owner = new JournalCompletedEntriesSnapshot(
            [new JournalEntryRecord(1, 0, "The Company You Held", "65632", "owner")],
            "1 entry");

        var compare = JournalCompletedEntriesComparer.Compare(local, owner);

        Assert.False(compare.IsMatch);
        Assert.Contains(compare.Differences, diff => diff.Contains("The Company You Keep", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FullProof_AppliesSentinelAndRestoresOriginalText()
    {
        var probe = new FakeJournalProbe(originalText: "The Company You Keep");
        var scenario = new JournalCompletedEntriesLocalScenario(probe);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.True(report.IsSuccess);
        Assert.Equal("The Company You Keep", probe.CurrentText);
        Assert.Contains("[REVALIDATION] Journal Sentinel", report.OverrideResult!.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalFullProof_Blocks_WhenClientStructsIsUnavailable()
    {
        var scenario = new JournalCompletedEntriesLocalScenario(
            new FakeJournalProbe("The Company You Keep"),
            new LocalClientStructsAvailabilityDetector(propsPath: null, projectPath: null));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("plugins/local/LocalClientStructs.props is missing.", report.Precondition!.BlockingReason);
    }

    [Fact]
    public async Task OwnerFullProof_Blocks_WhenRequiredSignatureIsUnresolved()
    {
        var scenario = new JournalCompletedEntriesOwnerScenario(
            new FakeJournalProbe("The Company You Keep"),
            [new SignatureRequirement("journalProvider", "48 89 ?? ??", mustBeUnique: true)],
            [new SignatureResolution("journalProvider", matchCount: 0, rva: null, failureReason: "zero matches")],
            new SignatureGate());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Contains("journalProvider", report.Precondition!.BlockingReason, StringComparison.Ordinal);
    }

    private sealed class FakeJournalProbe(string originalText) : IJournalCompletedEntriesProbe
    {
        private string? originalText;

        public string CurrentText { get; private set; } = originalText;

        public ValueTask<JournalCompletedEntriesSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new JournalCompletedEntriesSnapshot(
                [new JournalEntryRecord(1, 0, CurrentText, "65632", "test")],
                "1 entry"));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken)
        {
            originalText = CurrentText;
            CurrentText = sentinel;
            return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket($"Applied {sentinel}", new JsonObject()));
        }

        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(
                string.Equals(CurrentText, sentinel, StringComparison.Ordinal),
                "Journal sentinel asserted",
                []));

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
        {
            CurrentText = originalText ?? CurrentText;
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "Journal text restored", []));
        }
    }

    private sealed class NullRouteMetadataProvider : IRouteMetadataProvider
    {
        public ValidationRoute Route => ValidationRoute.LocalClientStructs;

        public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }

    private sealed class NullEvidenceWriter : IEvidenceWriter
    {
        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EvidenceWriteResult("null", string.Empty));
    }
}
