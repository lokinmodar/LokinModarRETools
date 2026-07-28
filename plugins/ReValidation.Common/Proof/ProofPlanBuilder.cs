using ReValidation.Common.Discovery;

namespace ReValidation.Common.Proof;

public sealed class ProofPlanBuilder
{
    public BranchValidationPlan Build(DiscoveredTargetCatalog catalog, int requiredProofLevel)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredProofLevel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(requiredProofLevel, 4);

        var groups = catalog.Targets
            .GroupBy(x => (x.CueFamily, x.ProofProfile))
            .OrderBy(group => group.Key.CueFamily)
            .ThenBy(group => group.Key.ProofProfile)
            .Select(group => new ProofGroupDefinition(
                $"{group.Key.CueFamily}:{group.Key.ProofProfile}",
                group.Key.CueFamily,
                group.Key.ProofProfile,
                group.OrderBy(target => target.TargetId).ToArray()))
            .ToArray();

        return new BranchValidationPlan(catalog.Options, catalog.Targets, groups, requiredProofLevel);
    }
}
