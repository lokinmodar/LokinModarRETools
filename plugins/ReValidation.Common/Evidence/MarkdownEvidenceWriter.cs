using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed class MarkdownEvidenceWriter(EvidencePathBuilder paths) : IEvidenceWriter
{
    public async ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var outputPath = paths.BuildPath(context.EvidenceRoot, report.Scenario.Id, context.Route, report.Timestamp, "md");
        var markdown = $$"""
        # {{report.Scenario.Name}}

        - Scenario: `{{report.Scenario.Id}}`
        - Route: `{{context.Route}}`
        - Mode: `{{context.Mode}}`
        - Status: `{{report.Status}}`
        - Timestamp: `{{report.Timestamp:O}}`

        ## Verdict

        {{report.Summary}}
        """;
        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        return new EvidenceWriteResult("markdown", outputPath);
    }
}
