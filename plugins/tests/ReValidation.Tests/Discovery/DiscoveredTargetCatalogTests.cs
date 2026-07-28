using ReValidation.Common.Discovery;
using Xunit;

namespace ReValidation.Tests.Discovery;

public sealed class DiscoveredTargetCatalogTests
{
    [Fact]
    public void FilterTargets_ReturnsOnlyRequestedFamilyAndPrefix()
    {
        var options = new ClientStructsDiscoveryOptions(
            repositoryPath: @"C:\Dante\_dalamud\FFXIVClientStructs-journal-tooltip-unified",
            baseRef: "upstream/main",
            mode: DiscoveryMode.Diff,
            families: [TargetFamily.ItemTooltip, TargetFamily.ActionTooltip],
            targetFilter: "AddonItemDetail.");
        var catalog = new DiscoveredTargetCatalog(
            options,
            [
                new DiscoveredTarget("AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89", @"FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs", true, TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4),
                new DiscoveredTarget("Journal.Instance", "Journal", "Instance", BindingKind.StaticAddress, "48 8D 0D", @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.StaticAddressConsumer, 4),
            ],
            warnings: []);

        var filtered = catalog.Filter(TargetFamily.ItemTooltip, "AddonItemDetail.");

        Assert.Single(filtered);
        Assert.Equal("AddonItemDetail.GenerateTooltip", filtered[0].TargetId);
    }
}
