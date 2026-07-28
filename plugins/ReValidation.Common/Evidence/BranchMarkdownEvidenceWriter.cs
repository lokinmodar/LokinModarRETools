using ReValidation.Common.Proof;

namespace ReValidation.Common.Evidence;

public sealed class BranchMarkdownEvidenceWriter(EvidencePathBuilder paths)
{
    public async ValueTask<EvidenceWriteResult> WriteAsync(
        BranchValidationRunReport report,
        string evidenceRoot,
        CancellationToken cancellationToken)
    {
        var outputPath = paths.BuildPath(evidenceRoot, "branch-validation", report.Route, DateTimeOffset.UtcNow, "md");
        var groupSections = string.Join(
            Environment.NewLine,
            report.Groups.Select(group => $$"""
            ## {{group.GroupId}}

            {{group.ArtifactSummary ?? "No artifact summary."}}

            {{string.Join(Environment.NewLine, group.Targets.Select(target => $"- Target: `{target.TargetId}`; verdict: `{target.Verdict}`; matches: `{target.MatchCount}`; RVA: `{FormatRva(target.Rva)}`; observed hits: `{target.ObservedHitCount}`; hook installed: `{target.HookInstalled}`; effect applied: `{target.EffectApplied}`; Effect restored: `{target.EffectRestored}`{FormatBlockingReason(target.BlockingReason)}"))}}
            """));
        var markdown = $$"""
        # Branch Validation

        - Route: `{{report.Route}}`
        - Required proof level: `{{report.RequiredProofLevel}}`
        - Status: `{{(report.IsSuccess ? "passed" : "failed")}}`

        {{groupSections}}
        """;

        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        return new EvidenceWriteResult("markdown", outputPath);
    }

    private static string FormatRva(long? rva) => rva is null ? "none" : $"0x{rva:X}";

    private static string FormatBlockingReason(string? blockingReason) =>
        string.IsNullOrWhiteSpace(blockingReason) ? string.Empty : $"; blocking reason: `{blockingReason}`";
}
