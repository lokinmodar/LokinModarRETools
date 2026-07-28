using ReValidation.Common.Discovery;
using ReValidation.Common.Proof;
using Xunit;

namespace ReValidation.Tests.Proof;

public sealed class ProofPlanBuilderTests
{
    [Fact]
    public void Build_GroupsTargetsByCueFamilyAndProofProfile()
    {
        var options = new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.Journal, TargetFamily.ItemTooltip], null);
        var catalog = new DiscoveredTargetCatalog(
            options,
            [
                new DiscoveredTarget("Journal.Instance", "Journal", "Instance", BindingKind.StaticAddress, "48 8D 0D", @"Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.StaticAddressConsumer, 4),
                new DiscoveredTarget("Journal.GetQuestData", "Journal", "GetQuestData", BindingKind.MemberFunction, "48 89 5C 24", @"Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.ConsumerChain, 4),
                new DiscoveredTarget("AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89 5C 24", @"AddonItemDetail.cs", true, TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4),
            ],
            []);

        var plan = new ProofPlanBuilder().Build(catalog, requiredProofLevel: 4);

        Assert.Equal(2, plan.Groups.Count);
        Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.JournalCompletedList && x.Targets.Count == 2);
        Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.TooltipItemDetail && x.Targets.Count == 1);
    }
}
