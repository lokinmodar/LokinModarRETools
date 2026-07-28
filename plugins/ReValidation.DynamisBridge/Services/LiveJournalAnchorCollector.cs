using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class LiveJournalAnchorCollector(
    Func<nint> getAddonAddress,
    Func<nint> getJournalAgentAddress) : IJournalAnchorCollector
{
    public IReadOnlyList<JournalAnchorRecord> CaptureAnchors()
    {
        var anchors = new List<JournalAnchorRecord>();
        var addonAddress = getAddonAddress();
        if (addonAddress != 0)
            anchors.Add(new JournalAnchorRecord("journal-addon", addonAddress, "GameGui", "UI root"));

        var agentAddress = getJournalAgentAddress();
        if (agentAddress != 0)
            anchors.Add(new JournalAnchorRecord("journal-agent", agentAddress, "FFXIVClientStructs", "Agent state"));

        return anchors;
    }
}
