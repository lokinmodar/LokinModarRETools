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
        ValidateClientStructsCheckout(options.RepositoryPath);

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

    private static void ValidateClientStructsCheckout(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        var gitPath = Path.Combine(normalizedRoot, ".git");
        if (!string.Equals(Path.GetFileName(normalizedRoot), "FFXIVClientStructs", StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(normalizedRoot)
            || !File.Exists(Path.Combine(normalizedRoot, "FFXIVClientStructs.slnx"))
            || !File.Exists(Path.Combine(normalizedRoot, "FFXIVClientStructs", "FFXIVClientStructs.csproj"))
            || (!Directory.Exists(gitPath) && !File.Exists(gitPath)))
            throw new ArgumentException("Repository path must be a local FFXIVClientStructs checkout.", nameof(repositoryPath));
    }

    private static string ReadSource(string repositoryPath, string sourceFile) =>
        File.ReadAllText(Path.Combine(repositoryPath, sourceFile));
}
