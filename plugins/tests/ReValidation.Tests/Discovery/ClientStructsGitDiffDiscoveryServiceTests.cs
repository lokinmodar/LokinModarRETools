using ReValidation.Common.Discovery;
using Xunit;

namespace ReValidation.Tests.Discovery;

public sealed class ClientStructsGitDiffDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsOnlyChangedBindingsFromApprovedFamilies()
    {
        var journalPath = @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs";
        var itemPath = @"FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs";
        var unrelatedPath = @"FFXIVClientStructs\FFXIV\Client\UI\Agent\AgentMap.cs";
        var diffReader = new FakeGitDiffReader([journalPath, itemPath, unrelatedPath]);
        var sources = new Dictionary<string, string>
        {
            [journalPath] = """
                namespace FFXIVClientStructs.FFXIV.Client.Game.UI;
                public partial struct Journal {
                    [StaticAddress("48 8D 0D ?? ?? ?? ?? 66 89 83", 3)]
                    public static partial Journal* Instance();
                }
                """,
            [itemPath] = """
                namespace FFXIVClientStructs.FFXIV.Client.UI;
                public partial struct AddonItemDetail {
                    [MemberFunction("48 89 5C 24 ?? 55 56 57")]
                    public partial void GenerateTooltip();
                }
                """,
            [unrelatedPath] = """
                namespace FFXIVClientStructs.FFXIV.Client.UI.Agent;
                public partial struct AgentMap {
                    [MemberFunction("48 89 5C 24 ??")]
                    public partial void Update();
                }
                """,
        };
        var discovery = new ClientStructsGitDiffDiscoveryService(
            diffReader,
            new InteropBindingSourceParser(),
            (_, path) => sources[path]);

        var catalog = await discovery.DiscoverAsync(
            new ClientStructsDiscoveryOptions(
                @"C:\repo",
                "upstream/main",
                DiscoveryMode.Diff,
                [TargetFamily.Journal, TargetFamily.ItemTooltip],
                null),
            CancellationToken.None);

        Assert.Contains(catalog.Targets, target => target.TargetId == "Journal.Instance");
        Assert.Contains(catalog.Targets, target => target.TargetId == "AddonItemDetail.GenerateTooltip");
        Assert.DoesNotContain(catalog.Targets, target => target.DeclaringType == "AgentMap");
        Assert.All(catalog.Targets, target => Assert.True(target.ChangedAgainstBaseRef));
    }

    private sealed class FakeGitDiffReader(IReadOnlyList<string> changedFiles) : IGitDiffReader
    {
        public ValueTask<IReadOnlyList<string>> ReadChangedFilesAsync(
            string repositoryPath,
            string baseRef,
            CancellationToken cancellationToken) => ValueTask.FromResult(changedFiles);
    }
}
