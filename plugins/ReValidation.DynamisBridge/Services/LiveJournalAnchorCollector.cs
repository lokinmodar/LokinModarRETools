using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class LiveJournalAnchorCollector(
    Func<nint> getAddonAddress,
    Func<IReadOnlyList<nint>> getKnownCandidatePointers) : IJournalAnchorCollector
{
    public IReadOnlyList<JournalAnchorRecord> CaptureAnchors()
    {
        var anchors = new List<JournalAnchorRecord>();
        var addonAddress = getAddonAddress();
        if (addonAddress != 0)
            anchors.Add(new JournalAnchorRecord("journal-addon", addonAddress, "GameGui", "UI root"));

        foreach (var pointer in getKnownCandidatePointers())
            if (pointer != 0)
                anchors.Add(new JournalAnchorRecord($"known-{pointer:X}", pointer, "KnownCandidate", "Known pointer"));

        return anchors;
    }
}
