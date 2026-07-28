using ReValidation.Common.Evidence;
using ReValidation.Common.Models;

namespace ReValidation.Common.Proof;

public sealed record BranchValidationRunReport
{
    public BranchValidationRunReport(
        ValidationRoute Route,
        int requiredProofLevel,
        IReadOnlyList<ProofGroupRunReport> Groups,
        IReadOnlyList<EvidenceWriteResult>? Evidence = null)
    {
        this.Route = Route;
        RequiredProofLevel = requiredProofLevel;
        this.Groups = Groups;
        this.Evidence = Evidence;
    }

    public ValidationRoute Route { get; }
    public int RequiredProofLevel { get; }
    public IReadOnlyList<ProofGroupRunReport> Groups { get; }
    public IReadOnlyList<EvidenceWriteResult>? Evidence { get; init; }
    public IReadOnlyList<TargetProofRecord> Targets => Groups.SelectMany(group => group.Targets).ToArray();
    public bool IsSuccess => Targets.Count > 0 && Targets.All(target => string.Equals(target.Verdict, "passed", StringComparison.Ordinal));

    public static BranchValidationRunReport From(
        BranchValidationPlan plan,
        ValidationRoute route,
        IReadOnlyList<ProofGroupRunReport> groupReports)
    {
        var reportedTargetIds = groupReports
            .SelectMany(group => group.Targets)
            .Select(target => target.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var missingTargets = plan.Targets
            .Where(target => !reportedTargetIds.Contains(target.TargetId))
            .Select(target => new TargetProofRecord(
                target.TargetId,
                "not-proven",
                0,
                null,
                0,
                false,
                false,
                false,
                "No proof record was returned for the target."))
            .ToArray();

        if (missingTargets.Length == 0)
            return new BranchValidationRunReport(route, plan.RequiredProofLevel, groupReports);

        var reports = groupReports.ToList();
        reports.Add(new ProofGroupRunReport("unreported-targets", missingTargets, "Required targets did not produce proof records."));
        return new BranchValidationRunReport(route, plan.RequiredProofLevel, reports);
    }
}
