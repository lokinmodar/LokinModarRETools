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
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
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
            CreateProofExecutor(probe, "actionTooltip", "action"),
            new FakeTooltipComparisonSource("action", "Sprint"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);
        await ((IArmableValidationScenario)scenario).ArmAsync(CancellationToken.None);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
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

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("Local ClientStructs availability detector is required.", report.Precondition!.BlockingReason);
    }

    [Fact]
    public async Task OwnerFullProof_Blocks_WhenHookProofExecutorIsMissing()
    {
        var scenario = new TooltipItemDetailOwnerScenario(new FakeTooltipProbe("item", "Potion"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("Owner tooltip hook proof executor is required.", report.Precondition!.BlockingReason);
    }

    [Fact]
    public async Task Compare_UsesReferenceSourceAndReportsMismatch()
    {
        var probe = new FakeTooltipProbe("item", "Potion");
        var scenario = new TooltipItemDetailOwnerScenario(
            probe,
            CreateProofExecutor(probe, "itemTooltip", "item"),
            new FakeTooltipComparisonSource("item", "Ether"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.Compare);
        await ((IArmableValidationScenario)scenario).ArmAsync(CancellationToken.None);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("compare", report.FailedPhase);
        Assert.Contains(report.CompareResult!.Differences, difference => difference.Contains("Potion", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ArmCuePolling_IsReady_WhenTooltipCaptureSucceeds()
    {
        var scenario = new TooltipItemDetailLocalScenario(
            new FakeTooltipProbe("item", "Potion"),
            new LocalClientStructsAvailabilityDetector(propsPath: null, projectPath: null));

        var armable = Assert.IsAssignableFrom<IArmableValidationScenario>(scenario);
        var cue = await armable.PollArmCueAsync(CancellationToken.None);

        Assert.True(cue.IsReady);
        Assert.Equal("Tooltip cue ready.", cue.StatusText);
    }

    [Fact]
    public async Task ArmCuePolling_Waits_WhenTooltipHookCueIsNotObserved()
    {
        var scenario = new TooltipActionDetailOwnerScenario(
            new ThrowingTooltipProbe("ActionDetail addon is not visible."));

        var armable = Assert.IsAssignableFrom<IArmableValidationScenario>(scenario);
        var cue = await armable.PollArmCueAsync(CancellationToken.None);

        Assert.False(cue.IsReady);
        Assert.Equal("Waiting for action tooltip hook cue.", cue.StatusText);
    }

    private static OwnerHookProofExecutor CreateProofExecutor(ITooltipProbe probe, string targetId, string detailKind) =>
        new(
            new OwnerHookTargetRegistry(
            [
                new OwnerHookTargetDefinition(
                    targetId,
                    targetId,
                    "Open a tooltip.",
                    new TooltipHookContextCapture(detailKind),
                    new TooltipOwnerMutationStrategy(probe),
                    TestOwnerHookBinding.Instance),
            ]),
            new FakeOwnerHookInstaller(new FakeOwnerHook(detailKind)),
            new StaticResolutionProvider(new SignatureResolution(targetId, 1, 0x1234, null)));

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

    private sealed class FakeTooltipComparisonSource(string detailKind, string visibleText) : ITooltipComparisonSource
    {
        public ValueTask<TooltipSnapshot> CaptureReferenceAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TooltipSnapshot(detailKind, 5333, [visibleText], visibleText));
    }

    private sealed class ThrowingTooltipProbe(string message) : ITooltipProbe
    {
        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromException<TooltipSnapshot>(new InvalidOperationException(message));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeOwnerHookInstaller(IOwnerHook hook) : IOwnerHookInstaller
    {
        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution) => hook;
    }

    private sealed class FakeOwnerHook(string detailKind) : IOwnerHook
    {
        public int ObservedHitCount => 1;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => [new JsonObject { ["detailKind"] = detailKind }];

        public void Enable() { }

        public void Dispose() { }
    }

    private sealed class StaticResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) => resolution;
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
}
