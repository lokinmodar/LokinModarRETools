using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed class MarkdownEvidenceWriter(EvidencePathBuilder paths) : IEvidenceWriter
{
    public string Kind => "markdown";

    public async ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var outputPath = paths.BuildPath(context.EvidenceRoot, report.Scenario.Id, context.Route, report.Timestamp, "md", report.EvidenceRunId);
        var evidence = RunEvidenceEnvelope.From(report, context);
        var routeMetadata = evidence.RouteMetadata.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, evidence.RouteMetadata.Select(pair => $"- `{pair.Key}`: `{pair.Value}`"));
        var captureMetrics = evidence.Proof.CaptureMetrics.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, evidence.Proof.CaptureMetrics.Select(pair => $"- `{pair.Key}`: `{pair.Value}`"));
        var exportFailures = evidence.ExportFailures.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, evidence.ExportFailures.Select(failure => $"- `{failure.Kind}`: {failure.FailureReason}"));
        var markdown = $$"""
        # {{evidence.ScenarioName}}

        - Scenario: `{{evidence.ScenarioId}}`
        - Route: `{{evidence.Route}}`
        - Mode: `{{evidence.Mode}}`
        - Status: `{{evidence.Status}}`
        - Timestamp: `{{evidence.Timestamp:O}}`

        ## Verdict

        Run completed with status: `{{evidence.Status}}`.

        ## Proof

        - Preconditions passed: `{{evidence.Proof.PreconditionsPassed}}`
        - Capture completed: `{{evidence.Proof.CaptureCompleted}}`
        - Comparison performed: `{{evidence.Proof.ComparisonPerformed}}`
        - Comparison match: `{{evidence.Proof.IsMatch}}`
        - Comparison difference count: `{{evidence.Proof.ComparisonDifferenceCount}}`
        - Override attempted: `{{evidence.Proof.OverrideAttempted}}`
        - Override ticket created: `{{evidence.Proof.OverrideTicketCreated}}`
        - Assertion passed: `{{evidence.Proof.AssertPassed}}`
        - Restore attempted: `{{evidence.Proof.RestoreAttempted}}`
        - Restore passed: `{{evidence.Proof.RestorePassed}}`

        ### Capture Metrics

        {{captureMetrics}}

        ## Route Metadata

        {{routeMetadata}}

        ## Export Failures

        {{exportFailures}}
        """;
        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        return new EvidenceWriteResult(Kind, outputPath);
    }
}
