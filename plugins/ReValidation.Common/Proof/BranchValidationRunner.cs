using ReValidation.Common.Evidence;

namespace ReValidation.Common.Proof;

public sealed class BranchValidationRunner(
    BranchJsonEvidenceWriter jsonWriter,
    BranchMarkdownEvidenceWriter markdownWriter) : IBranchValidationRunner
{
    public async ValueTask<BranchValidationRunReport> RunAsync(
        BranchValidationPlan plan,
        IBranchValidationRouteAdapter routeAdapter,
        string evidenceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(routeAdapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRoot);

        var groupReports = new List<ProofGroupRunReport>();
        foreach (var group in plan.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            groupReports.Add(await routeAdapter.RunProofGroupAsync(group, plan.RequiredProofLevel, cancellationToken));
        }

        var report = BranchValidationRunReport.From(plan, routeAdapter.Route, groupReports);
        var evidence = new[]
        {
            await jsonWriter.WriteAsync(report, evidenceRoot, cancellationToken),
            await markdownWriter.WriteAsync(report, evidenceRoot, cancellationToken),
        };
        return report with { Evidence = evidence };
    }
}
