namespace ReValidation.Common.Discovery;

public sealed record DiscoveredTarget(
    string TargetId,
    string DeclaringType,
    string MemberName,
    BindingKind BindingKind,
    string PatternOrAddress,
    string SourceFile,
    bool ChangedAgainstBaseRef,
    TargetFamily Family,
    CueFamily CueFamily,
    ProofProfile ProofProfile,
    int RequiredProofLevel);
