using ReValidation.Common.Discovery;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Runtime;
using ReValidation.LocalClientStructs.Services;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Routes;

public sealed class TooltipBranchValidationRouteAdapterTests
{
    [Fact]
    public async Task LocalRoute_RunsTooltipProofGroup_EndToEnd()
    {
        var group = ProofGroupFactory.CreateItemTooltipGroup();
        var adapter = new LocalBranchValidationRouteAdapter(
            new LocalTooltipProofExecutor(new FakeTooltipProbe(), new FakeTooltipProofHookFactory(observedHitCount: 1)),
            new LocalTooltipProofExecutor(new FakeTooltipProbe(), new FakeTooltipProofHookFactory(observedHitCount: 1)));

        var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

        Assert.All(report.Targets, target => Assert.Equal("passed", target.Verdict));
    }

    [Fact]
    public async Task LocalExecutor_Blocks_WhenTooltipHookDoesNotObserveACall()
    {
        var probe = new FakeTooltipProbe();
        var report = await new LocalTooltipProofExecutor(probe, new FakeTooltipProofHookFactory(observedHitCount: 0))
            .RunAsync(ProofGroupFactory.CreateItemTooltipGroup(), requiredProofLevel: 4, CancellationToken.None);

        var target = Assert.Single(report.Targets);
        Assert.Equal("not-observed", target.Verdict);
        Assert.True(target.EffectRestored);
        Assert.Equal(1, probe.RestoreCount);
    }

    [Fact]
    public async Task OwnerRoute_BlocksTooltipProofGroup_WhenSignatureResolutionIsNotUnique()
    {
        var group = ProofGroupFactory.CreateActionTooltipGroup();
        var adapter = new OwnerBranchValidationRouteAdapter(
            new FakeSignatureResolutionProvider(new SignatureResolution("AddonActionDetail.GenerateTooltip", 2, null, "multiple matches")));

        var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

        Assert.Contains(report.Targets, target => target.Verdict == "blocked" && target.BlockingReason == "Signature resolution was not unique.");
    }

    [Fact]
    public async Task LocalExecutor_RestoresAndDisposesHook_WhenCancelled()
    {
        var probe = new CancellingTooltipProbe();
        var hookFactory = new TrackingTooltipProofHookFactory(observedHitCount: 0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new LocalTooltipProofExecutor(probe, hookFactory)
                .RunAsync(ProofGroupFactory.CreateItemTooltipGroup(), requiredProofLevel: 4, new CancellationToken(canceled: true)).AsTask());

        Assert.Equal(1, probe.RestoreCount);
        Assert.Equal(1, hookFactory.LastCreatedHook?.EnableCount);
        Assert.Equal(1, hookFactory.LastCreatedHook?.DisposeCount);
    }

    [Fact]
    public async Task OwnerExecutor_RestoresAndDisposesHook_WhenCancelled()
    {
        var probe = new CancellingTooltipProbe();
        var hookFactory = new TrackingTooltipProofHookFactory(observedHitCount: 0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new OwnerTooltipProofExecutor(probe, hookFactory)
                .RunAsync(ProofGroupFactory.CreateActionTooltipGroup(), requiredProofLevel: 4, new CancellationToken(canceled: true)).AsTask());

        Assert.Equal(1, probe.RestoreCount);
        Assert.Equal(1, hookFactory.LastCreatedHook?.EnableCount);
        Assert.Equal(1, hookFactory.LastCreatedHook?.DisposeCount);
    }

    private sealed class FakeTooltipProbe : ITooltipProbe
    {
        public int RestoreCount { get; private set; }
        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new TooltipSnapshot("item", 1, ["ready"], "ready"));
        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) => ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket("applied", new()));
        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) => ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(true, "asserted", []));
        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
        {
            RestoreCount++;
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "restored", []));
        }
    }

    private sealed class CancellingTooltipProbe : ITooltipProbe
    {
        public int RestoreCount { get; private set; }

        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromCanceled<TooltipSnapshot>(cancellationToken);

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
        {
            RestoreCount++;
            return ValueTask.FromResult(new ScenarioRestoreResult(true, "restored", []));
        }
    }

    private sealed class FakeTooltipProofHookFactory(int observedHitCount) : ITooltipProofHookFactory
    {
        public ITooltipProofHook Create(string targetId) => new FakeTooltipProofHook(observedHitCount);
    }

    private sealed class FakeTooltipProofHook(int observedHitCount) : ITooltipProofHook
    {
        public int ObservedHitCount => observedHitCount;
        public void Enable() { }
        public void Dispose() { }
    }

    private sealed class TrackingTooltipProofHookFactory(int observedHitCount) : ITooltipProofHookFactory
    {
        public TrackingTooltipProofHook? LastCreatedHook { get; private set; }

        public ITooltipProofHook Create(string targetId)
        {
            LastCreatedHook = new TrackingTooltipProofHook(observedHitCount);
            return LastCreatedHook;
        }
    }

    private sealed class TrackingTooltipProofHook(int observedHitCount) : ITooltipProofHook
    {
        public int EnableCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ObservedHitCount => observedHitCount;

        public void Enable() => EnableCount++;
        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeSignatureResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string targetId) => resolution;
    }

    private static class ProofGroupFactory
    {
        public static ProofGroupDefinition CreateItemTooltipGroup() =>
            Create("tooltip.item-detail:detour-function", CueFamily.TooltipItemDetail, "AddonItemDetail.GenerateTooltip");

        public static ProofGroupDefinition CreateActionTooltipGroup() =>
            Create("tooltip.action-detail:detour-function", CueFamily.TooltipActionDetail, "AddonActionDetail.GenerateTooltip");

        private static ProofGroupDefinition Create(string groupId, CueFamily cueFamily, string targetId) =>
            new(
                groupId,
                cueFamily,
                ProofProfile.DetourFunction,
                [new DiscoveredTarget(targetId, "Addon", "GenerateTooltip", BindingKind.MemberFunction, "48 89", "Addon.cs", true, cueFamily is CueFamily.TooltipItemDetail ? TargetFamily.ItemTooltip : TargetFamily.ActionTooltip, cueFamily, ProofProfile.DetourFunction, 4)]);
    }
}
