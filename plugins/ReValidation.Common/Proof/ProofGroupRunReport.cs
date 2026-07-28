namespace ReValidation.Common.Proof;

public sealed record ProofGroupRunReport
{
    public ProofGroupRunReport(string GroupId, IReadOnlyList<TargetProofRecord> Targets, string? artifactSummary)
    {
        this.GroupId = GroupId;
        this.Targets = Targets;
        ArtifactSummary = artifactSummary;
    }

    public string GroupId { get; }
    public IReadOnlyList<TargetProofRecord> Targets { get; }
    public string? ArtifactSummary { get; }
}
