using System.Globalization;
using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed class EvidencePathBuilder
{
    public string BuildPath(string evidenceRoot, string scenarioId, ValidationRoute route, DateTimeOffset timestamp, string extension)
    {
        var day = timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var directory = Path.Combine(evidenceRoot, day, scenarioId);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{stamp}-{route.ToString().ToLowerInvariant()}.{extension}");
    }
}
