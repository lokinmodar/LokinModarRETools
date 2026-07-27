using System.Text.Json;
using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed class JsonEvidenceWriter(EvidencePathBuilder paths) : IEvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Kind => "json";

    public async ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var outputPath = paths.BuildPath(context.EvidenceRoot, report.Scenario.Id, context.Route, report.Timestamp, "json", report.EvidenceRunId);
        var payload = JsonSerializer.Serialize(RunEvidenceEnvelope.From(report, context), JsonOptions);
        await File.WriteAllTextAsync(outputPath, payload, cancellationToken);
        return new EvidenceWriteResult(Kind, outputPath);
    }
}
