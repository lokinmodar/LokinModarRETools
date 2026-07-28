using System.Numerics;
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

    public bool InspectObject(nint address, string? name) => apiClient.InspectObject(address, name: name);

    public bool InspectRegion(nint address, uint size, string typeName, string? name) =>
        apiClient.InspectRegion(address, size, typeName, name: name);

    public bool DrawPointer(nint address, string? name) =>
        apiClient.DrawPointer(address, null, () => name, null, 0, Vector2.Zero);

    private JournalCandidateSeed CreateSeed(string anchorId, nint address, string role)
    {
        var className = apiClient.GetClass(address)?.Name;
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
