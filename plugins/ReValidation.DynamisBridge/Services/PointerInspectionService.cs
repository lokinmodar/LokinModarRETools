using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class PointerInspectionService(IDynamisApiClient apiClient) : IPointerInspectionService
{
    public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors)
    {
        return anchors
            .Where(anchor => anchor.Address != 0)
            .Select(anchor => CreateSeed(anchor.AnchorId, anchor.Address, anchor.Role))
            .GroupBy(seed => seed.Address)
            .Select(group => group.First())
            .ToArray();
    }

    public bool InspectObject(nint address) => apiClient.InspectObject(address);
    public bool InspectRegion(nint address, nuint size) => apiClient.InspectRegion(address, size);
    public bool DrawPointer(string label, nint address) => apiClient.DrawPointer(label, address);

    private JournalCandidateSeed CreateSeed(string anchorId, nint address, string role)
    {
        var className = apiClient.GetClassName(address);
        var looksLikeLeafTextNode = className?.Contains("TextNode", StringComparison.OrdinalIgnoreCase) == true;
        return new JournalCandidateSeed(
            $"candidate-{address:X}",
            address,
            anchorId,
            role,
            className,
            "anchor-derived",
            null,
            looksLikeLeafTextNode,
            0);
    }
}
