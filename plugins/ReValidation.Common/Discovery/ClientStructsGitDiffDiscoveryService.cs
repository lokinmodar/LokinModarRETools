namespace ReValidation.Common.Discovery;

public sealed class ClientStructsGitDiffDiscoveryService : IClientStructsDiscoveryService
{
    private readonly IGitDiffReader diffReader;
    private readonly InteropBindingSourceParser parser;
    private readonly Func<string, string, string> readSource;

    public ClientStructsGitDiffDiscoveryService(
        IGitDiffReader diffReader,
        InteropBindingSourceParser parser,
        Func<string, string, string>? readSource = null)
    {
        this.diffReader = diffReader;
        this.parser = parser;
        this.readSource = readSource ?? ReadSource;
    }

    public async ValueTask<DiscoveredTargetCatalog> DiscoverAsync(
        ClientStructsDiscoveryOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var changedFiles = await diffReader.ReadChangedFilesAsync(options.RepositoryPath, options.BaseRef, cancellationToken);
        var targets = changedFiles
            .Where(IsApprovedFamilyPath)
            .SelectMany(sourceFile => parser.Parse(sourceFile, readSource(options.RepositoryPath, sourceFile), changedAgainstBaseRef: true))
            .Where(target => options.Families.Contains(target.Family))
            .Where(target => string.IsNullOrWhiteSpace(options.TargetFilter)
                || target.TargetId.StartsWith(options.TargetFilter, StringComparison.Ordinal))
            .ToArray();

        return new DiscoveredTargetCatalog(options, targets, []);
    }

    private static bool IsApprovedFamilyPath(string sourceFile) => sourceFile.Replace('/', '\\') is
        "FFXIVClientStructs\\FFXIV\\Client\\Game\\UI\\Journal.cs" or
        "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonItemDetail.cs" or
        "FFXIVClientStructs\\FFXIV\\Client\\UI\\AddonActionDetail.cs";

    private static string ReadSource(string repositoryPath, string sourceFile) =>
        File.ReadAllText(Path.Combine(repositoryPath, sourceFile));
}
