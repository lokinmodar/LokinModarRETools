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

public sealed class TooltipScenarioTests
{
    [Fact]
    public void ItemCompare_Fails_WhenPayloadLineDiffers()
    {
        var left = new TooltipSnapshot("item", 5333, ["Potion", "Restores HP"], "Potion");
        var right = new TooltipSnapshot("item", 5333, ["Potion", "Restores MP"], "Potion");

        var compare = TooltipComparer.Compare(left, right);

        Assert.False(compare.IsMatch);
        Assert.Contains(compare.Differences, diff => diff.Contains("Restores HP", StringComparison.Ordinal));
    }

    [Fact]
    public void ItemCompare_Fails_WhenPayloadLineCountDiffers()
    {
        var left = new TooltipSnapshot("item", 5333, ["Potion", "Restores HP"], "Potion");
        var right = new TooltipSnapshot("item", 5333, ["Potion"], "Potion");

        var compare = TooltipComparer.Compare(left, right);

        Assert.False(compare.IsMatch);
        Assert.Contains(compare.Differences, diff => diff.Contains("Payload line count mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DetailKindMismatch_FailsComparisonAndCapture()
    {
        var left = new TooltipSnapshot("item", 5333, ["Potion"], "Potion");
        var right = new TooltipSnapshot("action", 5333, ["Potion"], "Potion");

        var compare = TooltipComparer.Compare(left, right);

        Assert.False(compare.IsMatch);
        Assert.Contains(compare.Differences, diff => diff.Contains("DetailKind mismatch", StringComparison.Ordinal));

        var scenario = new TooltipItemDetailLocalScenario(
            new FakeTooltipProbe("action", "Potion"),
            new LocalClientStructsAvailabilityDetector(propsPath: null, projectPath: null));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.CaptureOnly);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.CaptureAsync(context, CancellationToken.None).AsTask());

        Assert.Contains("expected 'item'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActionFullProof_RestoresOriginalVisibleText()
    {
        var probe = new FakeTooltipProbe(detailKind: "action", visibleText: "Sprint");
        var scenario = new TooltipActionDetailOwnerScenario(
            probe,
            [new SignatureRequirement("actionTooltip", "48 89 ?? ??", mustBeUnique: true)],
            [new SignatureResolution("actionTooltip", matchCount: 1, rva: 0x1234, failureReason: null)]);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.True(report.IsSuccess);
        Assert.Equal("Sprint", probe.VisibleText);
        Assert.Contains("[REVALIDATION] Tooltip Sentinel", report.OverrideResult!.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalFullProof_Blocks_WhenAvailabilityDetectorIsMissing()
    {
        var scenario = new TooltipItemDetailLocalScenario(new FakeTooltipProbe("item", "Potion"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("Local ClientStructs availability detector is required.", report.Precondition!.BlockingReason);
    }

    [Fact]
    public async Task OwnerFullProof_Blocks_WhenSignatureRequirementsAreMissing()
    {
        var scenario = new TooltipItemDetailOwnerScenario(new FakeTooltipProbe("item", "Potion"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("Tooltip signature requirements are required.", report.Precondition!.BlockingReason);
    }

    private sealed class FakeTooltipProbe(string detailKind, string visibleText) : ITooltipProbe
    {
        private string? originalVisibleText;

        public string VisibleText { get; private set; } = visibleText;

        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TooltipSnapshot(detailKind, 5333, [VisibleText], VisibleText));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken)
        {
            originalVisibleText = VisibleText;
            VisibleText = sentinel;
            return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket($"Applied {sentinel}", new JsonObject()));
        }

        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(
                string.Equals(VisibleText, sentinel, StringComparison.Ordinal),
                "Tooltip sentinel asserted",
                []));

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
        {
            VisibleText = originalVisibleText ?? VisibleText;
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "Tooltip text restored", []));
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
