using System.Diagnostics;
using System.Text;

namespace ReValidation.Common.Discovery;

public sealed class GitProcessDiffReader : IGitDiffReader
{
    public async ValueTask<IReadOnlyList<string>> ReadChangedFilesAsync(
        string repositoryPath,
        string baseRef,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        cancellationToken.ThrowIfCancellationRequested();

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
        startInfo.ArgumentList.Add("diff");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("--diff-filter=ACMR");
        startInfo.ArgumentList.Add($"{baseRef}...HEAD");
        startInfo.ArgumentList.Add("--");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git diff.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git diff failed with exit code {process.ExitCode}: {error.Trim()}");

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
