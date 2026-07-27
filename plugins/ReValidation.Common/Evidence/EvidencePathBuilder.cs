using System.Globalization;
using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed class EvidencePathBuilder
{
    public string BuildPath(string evidenceRoot, string scenarioId, ValidationRoute route, DateTimeOffset timestamp, string extension, string? runId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        ValidateScenarioId(scenarioId);

        var day = timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var root = Path.GetFullPath(evidenceRoot);
        var directory = Path.GetFullPath(Path.Combine(root, day, scenarioId));
        var outputPath = Path.GetFullPath(Path.Combine(directory, $"{stamp}-{route.ToString().ToLowerInvariant()}-{runId ?? Guid.NewGuid().ToString("N")}.{extension}"));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Evidence output path must remain under the evidence root.", nameof(scenarioId));

        Directory.CreateDirectory(directory);
        return outputPath;
    }

    private static void ValidateScenarioId(string scenarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        if (Path.IsPathRooted(scenarioId)
            || scenarioId is "." or ".."
            || scenarioId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || scenarioId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Scenario ID must be a single valid path segment.", nameof(scenarioId));
    }
}
