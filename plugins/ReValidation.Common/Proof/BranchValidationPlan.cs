using ReValidation.Common.Discovery;

namespace ReValidation.Common.Proof;

public sealed record BranchValidationPlan(
    ClientStructsDiscoveryOptions DiscoveryOptions,
    IReadOnlyList<DiscoveredTarget> Targets,
    IReadOnlyList<ProofGroupDefinition> Groups,
    int RequiredProofLevel);
