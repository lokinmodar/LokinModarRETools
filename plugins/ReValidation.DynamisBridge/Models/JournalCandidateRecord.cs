namespace ReValidation.DynamisBridge.Models;

public sealed record JournalCandidateRecord(
    string CandidateId,
    nint Address,
    string AnchorId,
    string LogicalRoleGuess,
    string? ClassName,
    string? RegionSummary,
    JournalCandidateClassification Classification,
    int Confidence,
    JournalCandidateDisposition Disposition,
    string Notes);
