using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public interface IPointerInspectionService
{
    IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors);
    bool InspectObject(nint address, string? name);
    bool InspectRegion(nint address, uint size, string typeName, string? name);
    bool DrawPointer(nint address, string? name);
}
