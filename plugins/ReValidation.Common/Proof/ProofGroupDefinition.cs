using ReValidation.Common.Discovery;

namespace ReValidation.Common.Proof;

public sealed record ProofGroupDefinition(
    string GroupId,
    CueFamily CueFamily,
    ProofProfile ProofProfile,
    IReadOnlyList<DiscoveredTarget> Targets);
