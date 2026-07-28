using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public sealed class PointerInspectionService(
    IDynamisApiClient apiClient,
    NeighborPointerEnumerator neighborPointerEnumerator) : IPointerInspectionService
{
    public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors)
    {
        var seeds = new List<JournalCandidateSeed>();
        foreach (var anchor in anchors)
        {
            seeds.Add(CreateSeed(anchor.AnchorId, anchor.Address, anchor.Role));
            foreach (var neighbor in neighborPointerEnumerator.Enumerate(anchor.Address, pointerSlots: 8))
                seeds.Add(CreateSeed(anchor.AnchorId, neighbor, "neighbor"));
        }

        return seeds
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
        var childPointers = neighborPointerEnumerator.Enumerate(address, pointerSlots: 4).Count;
        return new JournalCandidateSeed(
            $"candidate-{address:X}",
            address,
            anchorId,
            role,
            className,
            $"neighbors={childPointers}",
            null,
            looksLikeLeafTextNode,
            childPointers);
    }
}
