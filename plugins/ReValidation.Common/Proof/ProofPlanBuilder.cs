using ReValidation.Common.Discovery;

namespace ReValidation.Common.Proof;

public sealed class ProofPlanBuilder
{
    public BranchValidationPlan Build(DiscoveredTargetCatalog catalog, int requiredProofLevel)
    {
        var groups = catalog.Targets
            .GroupBy(x => x.CueFamily)
            .Select(group => new ProofGroupDefinition(
                $"{group.Key}:{group.First().ProofProfile}",
                group.Key,
                group.First().ProofProfile,
                group.ToArray()))
            .ToArray();

        return new BranchValidationPlan(catalog.Options, catalog.Targets, groups, requiredProofLevel);
    }
}
