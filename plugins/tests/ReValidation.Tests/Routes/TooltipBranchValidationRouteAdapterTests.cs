using ReValidation.Common.Discovery;
using ReValidation.Common.Proof;
using ReValidation.LocalClientStructs.Runtime;
using ReValidation.LocalClientStructs.Services;
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
            new FakeLocalTooltipProofExecutor(TargetProofRecordFactory.PassedTooltip("AddonItemDetail.GenerateTooltip")),
            new FakeLocalTooltipProofExecutor(TargetProofRecordFactory.PassedTooltip("AgentItemDetail.ReceiveEvent")));

        var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

        Assert.All(report.Targets, target => Assert.Equal("passed", target.Verdict));
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

    private sealed class FakeLocalTooltipProofExecutor(TargetProofRecord record) : ITooltipProofExecutor
    {
        public ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProofGroupRunReport(group.GroupId, [record], "fake tooltip proof"));
    }

    private sealed class FakeSignatureResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string targetId) => resolution;
    }

    private static class TargetProofRecordFactory
    {
        public static TargetProofRecord PassedTooltip(string targetId) =>
            new(targetId, "passed", 1, 0x1234, 1, true, true, true, null);
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
                [new DiscoveredTarget(targetId, "Addon", "GenerateTooltip", BindingKind.MemberFunction, "48 89", "Addon.cs", true, TargetFamily.ItemTooltip, cueFamily, ProofProfile.DetourFunction, 4)]);
    }
}
