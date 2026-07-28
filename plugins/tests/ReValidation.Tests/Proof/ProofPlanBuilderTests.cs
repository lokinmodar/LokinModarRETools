using ReValidation.Common.Discovery;
using ReValidation.Common.Proof;
using Xunit;

namespace ReValidation.Tests.Proof;

public sealed class ProofPlanBuilderTests
{
    [Fact]
    public void Build_GroupsTargetsByCueFamilyAndProofProfile()
    {
        var catalog = CreateCatalog(
            [
                JournalInstance,
                JournalQuestData,
                ItemTooltip,
            ]);

        var plan = new ProofPlanBuilder().Build(catalog, requiredProofLevel: 4);

        Assert.Equal(3, plan.Groups.Count);
        Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.JournalCompletedList
            && x.ProofProfile == ProofProfile.StaticAddressConsumer
            && x.Targets.Select(target => target.TargetId).SequenceEqual([JournalInstance.TargetId]));
        Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.JournalCompletedList
            && x.ProofProfile == ProofProfile.ConsumerChain
            && x.Targets.Select(target => target.TargetId).SequenceEqual([JournalQuestData.TargetId]));
        Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.TooltipItemDetail && x.Targets.Count == 1);
    }

    [Fact]
    public void Build_GroupingIsIndependentOfTargetInputOrder()
    {
        var targets = new[] { JournalInstance, JournalQuestData, ItemTooltip };
        var firstPlan = new ProofPlanBuilder().Build(CreateCatalog(targets), requiredProofLevel: 3);
        var secondPlan = new ProofPlanBuilder().Build(CreateCatalog(targets.Reverse().ToArray()), requiredProofLevel: 3);

        var firstGroups = firstPlan.Groups
            .Select(group => $"{group.GroupId}:{string.Join(",", group.Targets.Select(target => target.TargetId).OrderBy(id => id))}")
            .OrderBy(group => group)
            .ToArray();
        var secondGroups = secondPlan.Groups
            .Select(group => $"{group.GroupId}:{string.Join(",", group.Targets.Select(target => target.TargetId).OrderBy(id => id))}")
            .OrderBy(group => group)
            .ToArray();

        Assert.Equal(firstGroups, secondGroups);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Build_RejectsProofLevelsOutsideSupportedRange(int requiredProofLevel)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProofPlanBuilder().Build(CreateCatalog([ItemTooltip]), requiredProofLevel));

        Assert.Equal("requiredProofLevel", exception.ParamName);
    }

    private static DiscoveredTargetCatalog CreateCatalog(IReadOnlyList<DiscoveredTarget> targets) => new(
        new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.Journal, TargetFamily.ItemTooltip], null),
        targets,
        []);

    private static readonly DiscoveredTarget JournalInstance = new(
        "Journal.Instance", "Journal", "Instance", BindingKind.StaticAddress, "48 8D 0D", @"Journal.cs", true,
        TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.StaticAddressConsumer, 3);

    private static readonly DiscoveredTarget JournalQuestData = new(
        "Journal.GetQuestData", "Journal", "GetQuestData", BindingKind.MemberFunction, "48 89 5C 24", @"Journal.cs", true,
        TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.ConsumerChain, 3);

    private static readonly DiscoveredTarget ItemTooltip = new(
        "AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89 5C 24", @"AddonItemDetail.cs", true,
        TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4);
}
