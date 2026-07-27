using System.Reflection;
using System.Security.Cryptography;
using ReValidation.Common.Abstractions;

namespace ReValidation.LocalClientStructs.Services;

public sealed record LocalClientStructsBuildMetadata(
    bool HasLocalConfiguration,
    string? ProjectPath,
    string? Branch,
    string? Commit,
    bool IsDirty,
    string? AssemblySha256);

public static class LocalClientStructsBuildMetadataLoader
{
    public static LocalClientStructsBuildMetadata Load(Assembly pluginAssembly, Assembly clientStructsAssembly)
    {
        ArgumentNullException.ThrowIfNull(pluginAssembly);
        ArgumentNullException.ThrowIfNull(clientStructsAssembly);

        var projectPath = pluginAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "ClientStructsProjectPath", StringComparison.Ordinal))
            ?.Value;
        var hasLocalConfiguration = !string.IsNullOrWhiteSpace(projectPath);
        var repoRoot = ResolveRepoRoot(projectPath);

        return new LocalClientStructsBuildMetadata(
            hasLocalConfiguration,
            projectPath,
            ReadGitOutput(repoRoot, "rev-parse --abbrev-ref HEAD"),
            ReadGitOutput(repoRoot, "rev-parse --short HEAD"),
            !string.IsNullOrWhiteSpace(ReadGitOutput(repoRoot, "status --short --untracked-files=no")),
            ComputeAssemblySha256(clientStructsAssembly.Location));
    }

    public static IRouteMetadataProvider CreateMetadataProvider(LocalClientStructsBuildMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (!metadata.HasLocalConfiguration)
            return new MissingLocalClientStructsMetadataProvider("plugins/local/LocalClientStructs.props is missing.");

        if (string.IsNullOrWhiteSpace(metadata.ProjectPath) || !File.Exists(metadata.ProjectPath))
            return new MissingLocalClientStructsMetadataProvider("ClientStructsProjectPath could not be resolved.");

        return new RealLocalClientStructsMetadataProvider(
            metadata.Branch ?? "unknown",
            metadata.Commit ?? "unknown",
            metadata.IsDirty,
            metadata.AssemblySha256 ?? "unknown");
    }

    private static string? ResolveRepoRoot(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        var projectFile = new FileInfo(projectPath);
        return projectFile.Directory?.Parent?.FullName;
    }

    private static string? ReadGitOutput(string? workingDirectory, string arguments)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return null;

        var startInfo = new System.Diagnostics.ProcessStartInfo("git", $"-C \"{workingDirectory}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
            return null;

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(3000);
        return process.ExitCode == 0 ? output : null;
    }

    private static string? ComputeAssemblySha256(string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            return null;

        using var stream = File.OpenRead(assemblyPath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
