using ReValidation.Common.Discovery;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using Xunit;

namespace ReValidation.Tests.Proof;

public sealed class BranchValidationRunnerTests
{
    [Fact]
    public async Task RunAsync_FailsAggregateWhenAnyReturnedTargetIsNotProven()
    {
        var plan = BranchValidationPlanFactory.CreateSingleTooltipPlan();
        var routeAdapter = new FakeBranchValidationRouteAdapter(
            [
                new ProofGroupRunReport(
                    "tooltip.item-detail:detour-function",
                    [
                        new TargetProofRecord("AddonItemDetail.GenerateTooltip", "passed", matchCount: 1, rva: 0x1234, observedHitCount: 1, hookInstalled: true, effectApplied: true, effectRestored: true, blockingReason: null),
                        new TargetProofRecord("AgentItemDetail.ReceiveEvent", "effect-not-proven", matchCount: 1, rva: 0x2234, observedHitCount: 1, hookInstalled: true, effectApplied: false, effectRestored: false, blockingReason: "No reversible effect was verified."),
                    ],
                    artifactSummary: "tooltip group"),
            ]);

        var report = await new BranchValidationRunner(
                new BranchJsonEvidenceWriter(new EvidencePathBuilder()),
                new BranchMarkdownEvidenceWriter(new EvidencePathBuilder()))
            .RunAsync(plan, routeAdapter, Path.GetTempPath(), CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Contains(report.Targets, x => x.TargetId == "AgentItemDetail.ReceiveEvent" && x.Verdict == "wrong-proof-group");
    }

    [Fact]
    public async Task RunAsync_FailsAggregateForDiagnosticProofLevel()
    {
        var plan = BranchValidationPlanFactory.CreateSingleTooltipPlan(requiredProofLevel: 3);
        var report = await RunAsync(
            plan,
            new ProofGroupRunReport(
                "tooltip.item-detail:detour-function",
                [CreatePassedTooltipRecord()],
                artifactSummary: "tooltip group"));

        Assert.False(report.IsSuccess);
        Assert.Contains(report.Targets, x => x.TargetId == "AddonItemDetail.GenerateTooltip" && x.Verdict == "diagnostic-only");
    }

    [Fact]
    public async Task RunAsync_FailsAggregateWhenTooltipEffectIsNotRestored()
    {
        var plan = BranchValidationPlanFactory.CreateSingleTooltipPlan();
        var report = await RunAsync(
            plan,
            new ProofGroupRunReport(
                "tooltip.item-detail:detour-function",
                [CreatePassedTooltipRecord(effectRestored: false)],
                artifactSummary: "tooltip group"));

        Assert.False(report.IsSuccess);
        Assert.Contains(report.Targets, x => x.TargetId == "AddonItemDetail.GenerateTooltip" && x.Verdict == "effect-not-proven");
    }

    [Fact]
    public async Task RunAsync_FailsAggregateWhenTargetIsReturnedInWrongProofGroup()
    {
        var plan = BranchValidationPlanFactory.CreateSingleTooltipPlan();
        var report = await RunAsync(
            plan,
            new ProofGroupRunReport(
                "journal.completed-entries:consumer-chain",
                [CreatePassedTooltipRecord()],
                artifactSummary: "wrong group"));

        Assert.False(report.IsSuccess);
        Assert.Contains(report.Targets, x => x.TargetId == "AddonItemDetail.GenerateTooltip" && x.Verdict == "wrong-proof-group");
        Assert.Contains(report.Targets, x => x.TargetId == "AddonItemDetail.GenerateTooltip" && x.Verdict == "not-proven");
    }

    [Fact]
    public async Task RunAsync_FailsAggregateWhenValidProofGroupsAreReturnedInDispatchOrderSwap()
    {
        var plan = BranchValidationPlanFactory.CreateTwoTooltipGroupsPlan();
        var report = await RunAsync(
            plan,
            new ProofGroupRunReport(
                "tooltip.action-detail:detour-function",
                [CreatePassedActionTooltipRecord()],
                artifactSummary: "action tooltip group"),
            new ProofGroupRunReport(
                "tooltip.item-detail:detour-function",
                [CreatePassedTooltipRecord()],
                artifactSummary: "item tooltip group"));

        Assert.False(report.IsSuccess);
        Assert.Equal(2, report.Targets.Count(target => target.Verdict == "wrong-proof-group"));
        Assert.Equal(2, report.Targets.Count(target => target.Verdict == "not-proven"));
    }

    private static async Task<BranchValidationRunReport> RunAsync(BranchValidationPlan plan, params ProofGroupRunReport[] proofReports) =>
        await new BranchValidationRunner(
                new BranchJsonEvidenceWriter(new EvidencePathBuilder()),
                new BranchMarkdownEvidenceWriter(new EvidencePathBuilder()))
            .RunAsync(plan, new FakeBranchValidationRouteAdapter(proofReports), Path.GetTempPath(), CancellationToken.None);

    private static TargetProofRecord CreatePassedTooltipRecord(bool effectRestored = true) =>
        new("AddonItemDetail.GenerateTooltip", "passed", matchCount: 1, rva: 0x1234, observedHitCount: 1, hookInstalled: true, effectApplied: true, effectRestored: effectRestored, blockingReason: null);

    private static TargetProofRecord CreatePassedActionTooltipRecord() =>
        new("AddonActionDetail.GenerateTooltip", "passed", matchCount: 1, rva: 0x2234, observedHitCount: 1, hookInstalled: true, effectApplied: true, effectRestored: true, blockingReason: null);

    private sealed class FakeBranchValidationRouteAdapter(IReadOnlyList<ProofGroupRunReport> proofReports) : IBranchValidationRouteAdapter
    {
        private readonly Queue<ProofGroupRunReport> proofReports = new(proofReports);

        public ValidationRoute Route => ValidationRoute.LocalClientStructs;

        public ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken) =>
            ValueTask.FromResult(proofReports.Dequeue());
    }

    private static class BranchValidationPlanFactory
    {
        public static BranchValidationPlan CreateSingleTooltipPlan(int requiredProofLevel = 4)
        {
            var target = new DiscoveredTarget(
                "AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89", "AddonItemDetail.cs", true,
                TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4);
            var options = new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.ItemTooltip], null);
            var group = new ProofGroupDefinition("tooltip.item-detail:detour-function", CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, [target]);
            return new BranchValidationPlan(options, [target], [group], requiredProofLevel);
        }

        public static BranchValidationPlan CreateTwoTooltipGroupsPlan()
        {
            var itemTarget = new DiscoveredTarget(
                "AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89", "AddonItemDetail.cs", true,
                TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4);
            var actionTarget = new DiscoveredTarget(
                "AddonActionDetail.GenerateTooltip", "AddonActionDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 8B", "AddonActionDetail.cs", true,
                TargetFamily.ActionTooltip, CueFamily.TooltipActionDetail, ProofProfile.DetourFunction, 4);
            var options = new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.ItemTooltip, TargetFamily.ActionTooltip], null);
            var itemGroup = new ProofGroupDefinition("tooltip.item-detail:detour-function", CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, [itemTarget]);
            var actionGroup = new ProofGroupDefinition("tooltip.action-detail:detour-function", CueFamily.TooltipActionDetail, ProofProfile.DetourFunction, [actionTarget]);
            return new BranchValidationPlan(options, [itemTarget, actionTarget], [itemGroup, actionGroup], 4);
        }
    }
}
