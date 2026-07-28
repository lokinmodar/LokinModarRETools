using ReValidation.Common.Models;
using ReValidation.Common.Proof;

namespace ReValidation.Common.Evidence;

public sealed record BranchValidationEvidenceEnvelope(
    ValidationRoute Route,
    int RequiredProofLevel,
    bool IsSuccess,
    IReadOnlyList<ProofGroupRunReport> Groups)
{
    public static BranchValidationEvidenceEnvelope From(BranchValidationRunReport report) =>
        new(report.Route, report.RequiredProofLevel, report.IsSuccess, report.Groups);
}
