using ReValidation.Common.Evidence;
using ReValidation.Common.Discovery;
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
    public bool IsSuccess => RequiredProofLevel >= 4
        && Targets.Count > 0
        && Targets.All(target => string.Equals(target.Verdict, "passed", StringComparison.Ordinal));

    public static BranchValidationRunReport From(
        BranchValidationPlan plan,
        ValidationRoute route,
        IReadOnlyList<(ProofGroupDefinition DispatchedGroup, ProofGroupRunReport ReturnedReport)> groupReports)
    {
        var plannedTargets = plan.Targets.ToDictionary(target => target.TargetId, StringComparer.Ordinal);
        var matchedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        var normalizedReports = new List<ProofGroupRunReport>();

        foreach (var (dispatchedGroup, report) in groupReports)
        {
            var normalizedTargets = new List<TargetProofRecord>();
            foreach (var target in report.Targets)
            {
                if (!string.Equals(report.GroupId, dispatchedGroup.GroupId, StringComparison.Ordinal)
                    || !dispatchedGroup.Targets.Any(expected => expected.TargetId == target.TargetId))
                {
                    normalizedTargets.Add(NormalizeFailure(
                        target,
                        "wrong-proof-group",
                        "Target was returned outside its dispatched proof group."));
                    continue;
                }

                matchedTargetIds.Add(target.TargetId);
                normalizedTargets.Add(NormalizeForPlan(target, plannedTargets[target.TargetId], plan.RequiredProofLevel));
            }

            normalizedReports.Add(new ProofGroupRunReport(report.GroupId, normalizedTargets, report.ArtifactSummary));
        }

        var missingTargets = plan.Targets
            .Where(target => !matchedTargetIds.Contains(target.TargetId))
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
            return new BranchValidationRunReport(route, plan.RequiredProofLevel, normalizedReports);

        normalizedReports.Add(new ProofGroupRunReport("unreported-targets", missingTargets, "Required targets did not produce proof records."));
        return new BranchValidationRunReport(route, plan.RequiredProofLevel, normalizedReports);
    }

    private static TargetProofRecord NormalizeForPlan(TargetProofRecord target, DiscoveredTarget plannedTarget, int requiredProofLevel)
    {
        if (!string.Equals(target.Verdict, "passed", StringComparison.Ordinal))
            return target;

        if (requiredProofLevel < 4)
            return NormalizeFailure(target, "diagnostic-only", "Branch validation requires proof level 4; lower proof levels are diagnostic only.");

        if (requiredProofLevel > plannedTarget.RequiredProofLevel)
            return NormalizeFailure(target, "required-proof-level-unavailable", "The discovered target does not support the required proof level.");

        if (plannedTarget.Family is TargetFamily.ItemTooltip or TargetFamily.ActionTooltip
            && (!target.EffectApplied || !target.EffectRestored))
            return NormalizeFailure(target, "effect-not-proven", "Tooltip proof requires a reversible visible effect and restore.");

        return target;
    }

    private static TargetProofRecord NormalizeFailure(TargetProofRecord target, string verdict, string blockingReason) =>
        new(
            target.TargetId,
            verdict,
            target.MatchCount,
            target.Rva,
            target.ObservedHitCount,
            target.HookInstalled,
            target.EffectApplied,
            target.EffectRestored,
            blockingReason);
}
