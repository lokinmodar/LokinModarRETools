using ReValidation.Common.Discovery;
using Xunit;

namespace ReValidation.Tests.Discovery;

public sealed class InteropBindingSourceParserTests
{
    [Fact]
    public void ParseBindings_ExtractsJournalAndTooltipInteropMembers()
    {
        var parser = new InteropBindingSourceParser();
        var source = """
            using FFXIVClientStructs.FFXIV.Component.GUI;
            namespace FFXIVClientStructs.FFXIV.Client.Game.UI;
            public partial struct Journal {
                [StaticAddress("48 8D 0D ?? ?? ?? ?? 66 89 83", 3)]
                public static partial Journal* Instance();
                [MemberFunction("48 89 5C 24 ?? 48 89 74 24 ??")]
                public partial bool IsEntryComplete(uint questId);
            }
            """;

        var bindings = parser.Parse(
            @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs",
            source,
            changedAgainstBaseRef: true);

        Assert.Contains(bindings, binding => binding.TargetId == "Journal.Instance"
            && binding.BindingKind == BindingKind.StaticAddress
            && binding.PatternOrAddress == "48 8D 0D ?? ?? ?? ?? 66 89 83"
            && binding.Family == TargetFamily.Journal
            && binding.CueFamily == CueFamily.JournalCompletedList
            && binding.ProofProfile == ProofProfile.StaticAddressConsumer
            && binding.RequiredProofLevel == 3);
        Assert.Contains(bindings, binding => binding.TargetId == "Journal.IsEntryComplete"
            && binding.BindingKind == BindingKind.MemberFunction
            && binding.ProofProfile == ProofProfile.DetourFunction
            && binding.RequiredProofLevel == 3);
    }

    [Fact]
    public void ParseBindings_AssignsNestedBindingsToTheirContainingType()
    {
        var parser = new InteropBindingSourceParser();
        var source = """
            namespace FFXIVClientStructs.FFXIV.Client.Game.UI;
            public partial struct Journal {
                public partial struct NestedJournalData {
                    [MemberFunction("48 89 5C 24 ??")]
                    public partial void Refresh();
                }

                [MemberFunction("48 89 74 24 ??")]
                public partial void Update();
            }
            """;

        var bindings = parser.Parse(
            @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs",
            source,
            changedAgainstBaseRef: true);

        Assert.Contains(bindings, binding => binding.TargetId == "NestedJournalData.Refresh");
        Assert.Contains(bindings, binding => binding.TargetId == "Journal.Update");
        Assert.DoesNotContain(bindings, binding => binding.TargetId == "Journal.Refresh");
    }
}
