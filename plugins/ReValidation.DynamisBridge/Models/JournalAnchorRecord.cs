namespace ReValidation.DynamisBridge.Models;

public sealed record JournalAnchorRecord(
    string AnchorId,
    nint Address,
    string Source,
    string Role);
