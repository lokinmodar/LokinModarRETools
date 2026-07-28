namespace ReValidation.DynamisBridge.Models;

public sealed record JournalCandidateSeed(
    string CandidateId,
    nint Address,
    string AnchorId,
    string LogicalRoleGuess,
    string? ClassName,
    string? RegionSummary,
    string? NearbyStringSample,
    bool LooksLikeLeafTextNode,
    int ChildPointerCount);
