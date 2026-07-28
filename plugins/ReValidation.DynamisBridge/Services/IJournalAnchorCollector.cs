using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public interface IJournalAnchorCollector
{
    IReadOnlyList<JournalAnchorRecord> CaptureAnchors();
}
