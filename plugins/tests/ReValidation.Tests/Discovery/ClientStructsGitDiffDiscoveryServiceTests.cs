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
        using var checkout = new ClientStructsCheckoutDirectory();
        var discovery = new ClientStructsGitDiffDiscoveryService(diffReader, new InteropBindingSourceParser(), (_, path) => sources[path]);

        var catalog = await discovery.DiscoverAsync(
            new ClientStructsDiscoveryOptions(
                checkout.Path,
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

    [Fact]
    public async Task DiscoverAsync_RejectsRootsThatAreNotClientStructsCheckouts()
    {
        var diffReader = new FakeGitDiffReader([]);
        var discovery = new ClientStructsGitDiffDiscoveryService(diffReader, new InteropBindingSourceParser());

        await Assert.ThrowsAsync<ArgumentException>(() => discovery.DiscoverAsync(
            new ClientStructsDiscoveryOptions(
                Path.GetTempPath(),
                "upstream/main",
                DiscoveryMode.Diff,
                [TargetFamily.Journal],
                null),
            CancellationToken.None).AsTask());

        Assert.False(diffReader.WasRead);
    }

    private sealed class FakeGitDiffReader(IReadOnlyList<string> changedFiles) : IGitDiffReader
    {
        public bool WasRead { get; private set; }

        public ValueTask<IReadOnlyList<string>> ReadChangedFilesAsync(
            string repositoryPath,
            string baseRef,
            CancellationToken cancellationToken)
        {
            WasRead = true;
            return ValueTask.FromResult(changedFiles);
        }
    }

    private sealed class ClientStructsCheckoutDirectory : IDisposable
    {
        public ClientStructsCheckoutDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"revalidation-clientstructs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "FFXIVClientStructs"));
            File.WriteAllText(System.IO.Path.Combine(Path, "FFXIVClientStructs.slnx"), "<Solution />");
            File.WriteAllText(System.IO.Path.Combine(Path, "FFXIVClientStructs", "FFXIVClientStructs.csproj"), "<Project />");
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
