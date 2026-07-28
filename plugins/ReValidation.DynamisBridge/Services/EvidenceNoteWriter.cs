using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class EvidenceNoteWriter(TimeProvider timeProvider)
{
    public async Task<string> WriteAsync(JournalProbeSession session, string outputRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, $"journal-session-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.md");
        var lines = new List<string>
        {
            "# Journal Session Note",
            string.Empty,
            $"Started: {session.StartedAtUtc:O}",
            $"Plugin: {session.PluginVersion}",
            $"Dynamis API: {session.DynamisApiVersion?.ToString() ?? "unknown"}",
            $"Executable: {session.ExecutableIdentity ?? "unknown"}",
            string.Empty,
            "## Anchors",
        };

        lines.AddRange(session.Anchors.Select(anchor => $"- `{anchor.AnchorId}` `{anchor.Address:X}` {anchor.Role}"));
        lines.Add(string.Empty);
        lines.Add("## Candidates");
        lines.AddRange(session.Candidates.Select(candidate =>
            $"- `{candidate.CandidateId}` `{candidate.Address:X}` {candidate.Classification} {candidate.Disposition}: {candidate.Notes}"));

        await File.WriteAllLinesAsync(path, lines, cancellationToken);
        return path;
    }
}
