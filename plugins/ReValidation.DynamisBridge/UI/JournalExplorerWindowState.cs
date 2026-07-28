using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.UI;

public sealed class JournalExplorerWindowState
{
    public string StatusText { get; private set; } = "Idle";
    public string StatusDetailText { get; private set; } = string.Empty;
    public IReadOnlyList<JournalAnchorRecord> Anchors { get; private set; } = Array.Empty<JournalAnchorRecord>();
    public IReadOnlyList<JournalCandidateRecord> Candidates { get; private set; } = Array.Empty<JournalCandidateRecord>();
    public string? SelectedCandidateId { get; private set; }
    public string? LastExportPath { get; private set; }

    public void SetBlocked(string detail)
    {
        StatusText = "Blocked";
        StatusDetailText = detail;
        Anchors = Array.Empty<JournalAnchorRecord>();
        Candidates = Array.Empty<JournalCandidateRecord>();
        SelectedCandidateId = null;
    }

    public void SetSession(IReadOnlyList<JournalAnchorRecord> anchors, IReadOnlyList<JournalCandidateRecord> candidates)
    {
        StatusText = "Armed";
        StatusDetailText = "Journal session captured.";
        Anchors = anchors;
        Candidates = candidates;
        SelectedCandidateId = candidates.FirstOrDefault()?.CandidateId;
    }

    public void SetExport(string path)
    {
        LastExportPath = path;
        StatusText = "Exported";
        StatusDetailText = path;
    }

    public void SetSelectedCandidate(string candidateId) => SelectedCandidateId = candidateId;

    public void SetCandidateDisposition(JournalCandidateDisposition disposition)
    {
        if (SelectedCandidateId is null)
            return;

        Candidates = Candidates
            .Select(candidate => candidate.CandidateId == SelectedCandidateId ? candidate with { Disposition = disposition } : candidate)
            .ToArray();
    }
}
