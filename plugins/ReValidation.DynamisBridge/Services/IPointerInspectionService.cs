using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public interface IPointerInspectionService
{
    IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors);
    bool InspectObject(nint address);
    bool InspectRegion(nint address, nuint size);
    bool DrawPointer(string label, nint address);
}
