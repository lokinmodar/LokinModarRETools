using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class EvidenceNoteWriter(TimeProvider timeProvider)
{
    public async Task<string> WriteAsync(JournalProbeSession session, string outputRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, $"journal-session-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.md");
        var dynamisApi = session.DynamisApiVersion is { } version
            ? $"{version} (features 0x{version.FeatureFlags:X})"
            : "unknown";
        var lines = new List<string>
        {
            "# Journal Session Note",
            string.Empty,
            $"Started: {session.StartedAtUtc:O}",
            $"Plugin: {session.PluginVersion}",
            $"Dynamis API: {dynamisApi}",
            $"Executable: {session.ExecutableIdentity ?? "unknown"}",
            string.Empty,
            "## Anchors",
        };

        lines.AddRange(session.Anchors.Select(anchor => $"- `{anchor.AnchorId}` `{anchor.Address:X}` {anchor.Role}"));
        lines.Add(string.Empty);
        lines.Add("## Candidates");
        lines.AddRange(session.Candidates.Select(candidate =>
            $"- `{candidate.CandidateId}` `{candidate.Address:X}` {candidate.Classification} Confidence: {candidate.Confidence} {candidate.Disposition}: {candidate.Notes}"));

        await File.WriteAllLinesAsync(path, lines, cancellationToken);
        return path;
    }
}
