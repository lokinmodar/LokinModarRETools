using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Scenarios;

public sealed class TooltipOwnerHookMigrationTests
{
    [Fact]
    public async Task TooltipItemOwnerScenario_UsesOwnerHookPipelineForFullProof()
    {
        var probe = new FakeTooltipProbe("item", "Potion");
        var scenario = new TooltipItemDetailOwnerScenario(
            probe,
            new OwnerHookProofExecutor(
                OwnerHookTargetRegistryFactory.WithTooltipTargets(probe),
                new FakeOwnerHookInstaller(new FakeOwnerHook(1, [new JsonObject { ["detailKind"] = "item" }])),
                new StaticResolutionProvider(new SignatureResolution("itemTooltip", 1, 0x1234, null))),
            new FakeTooltipComparisonSource("item", "Potion"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);
        await ((IArmableValidationScenario)scenario).ArmAsync(CancellationToken.None);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.True(report.OverrideAttempted);
        Assert.True(report.AssertResult!.Passed);
        Assert.True(report.RestoreResult!.Passed);
    }

    [Fact]
    public async Task TooltipItemOwnerScenario_WithoutObservedHook_DoesNotApplyMutationOrPassFullProof()
    {
        var probe = new FakeTooltipProbe("item", "Potion");
        var scenario = new TooltipItemDetailOwnerScenario(
            probe,
            new OwnerHookProofExecutor(
                OwnerHookTargetRegistryFactory.WithTooltipTargets(probe),
                new FakeOwnerHookInstaller(new FakeOwnerHook(0, [])),
                new StaticResolutionProvider(new SignatureResolution("itemTooltip", 1, 0x1234, null))),
            new FakeTooltipComparisonSource("item", "Potion"));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);
        var armable = Assert.IsAssignableFrom<IArmableValidationScenario>(scenario);
        Assert.True(armable.RequiresArming);
        await armable.ArmAsync(CancellationToken.None);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal(0, probe.ApplyCount);
        Assert.False(report.AssertResult!.Passed);
    }

    private static class OwnerHookTargetRegistryFactory
    {
        public static OwnerHookTargetRegistry WithTooltipTargets(ITooltipProbe probe) =>
            new(
            [
                new OwnerHookTargetDefinition(
                    "itemTooltip",
                    "itemTooltip",
                    "Open an item tooltip.",
                    new TooltipHookContextCapture("item"),
                    new TooltipOwnerMutationStrategy(probe)),
            ]);
    }

    private sealed class FakeTooltipProbe(string detailKind, string visibleText) : ITooltipProbe
    {
        private string? originalVisibleText;

        public int ApplyCount { get; private set; }

        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TooltipSnapshot(detailKind, 5333, [visibleText], visibleText));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken)
        {
            ApplyCount++;
            originalVisibleText = visibleText;
            visibleText = sentinel;
            return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket("applied", new JsonObject()));
        }

        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(string.Equals(visibleText, sentinel, StringComparison.Ordinal), "asserted", []));

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
        {
            visibleText = originalVisibleText ?? visibleText;
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "restored", []));
        }
    }

    private sealed class FakeTooltipComparisonSource(string detailKind, string visibleText) : ITooltipComparisonSource
    {
        public ValueTask<TooltipSnapshot> CaptureReferenceAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TooltipSnapshot(detailKind, 5333, [visibleText], visibleText));
    }

    private sealed class FakeOwnerHookInstaller(IOwnerHook hook) : IOwnerHookInstaller
    {
        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution) => hook;
    }

    private sealed class FakeOwnerHook(int observedHitCount, IReadOnlyList<JsonObject> contexts) : IOwnerHook
    {
        public int ObservedHitCount { get; } = observedHitCount;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => contexts;

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
