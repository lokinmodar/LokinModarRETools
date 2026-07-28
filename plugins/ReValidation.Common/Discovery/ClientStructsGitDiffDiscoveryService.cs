using System.Diagnostics;
using System.Text;

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
        if (!string.Equals(Path.GetFileName(normalizedRoot), "FFXIVClientStructs", StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(normalizedRoot)
            || !File.Exists(Path.Combine(normalizedRoot, "FFXIVClientStructs.slnx"))
            || !File.Exists(Path.Combine(normalizedRoot, "FFXIVClientStructs", "FFXIVClientStructs.csproj"))
            || !IsGitCheckoutRoot(normalizedRoot))
            throw new ArgumentException("Repository path must be a local FFXIVClientStructs checkout.", nameof(repositoryPath));
    }

    private static bool IsGitCheckoutRoot(string repositoryPath)
    {
        var isInsideWorkTree = ReadGitOutput(repositoryPath, "rev-parse", "--is-inside-work-tree");
        if (!string.Equals(isInsideWorkTree, "true", StringComparison.OrdinalIgnoreCase))
            return false;

        var topLevel = ReadGitOutput(repositoryPath, "rev-parse", "--show-toplevel");
        return !string.IsNullOrWhiteSpace(topLevel)
            && string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel)),
                repositoryPath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadGitOutput(string repositoryPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryPath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        if (process is null)
            return null;

        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : null;
    }

    private static string ReadSource(string repositoryPath, string sourceFile) =>
        File.ReadAllText(Path.Combine(repositoryPath, sourceFile));
}
