using System.Text.Json;
using ReValidation.Common.Proof;

namespace ReValidation.Common.Evidence;

public sealed class BranchJsonEvidenceWriter(EvidencePathBuilder paths)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async ValueTask<EvidenceWriteResult> WriteAsync(
        BranchValidationRunReport report,
        string evidenceRoot,
        CancellationToken cancellationToken)
    {
        var outputPath = paths.BuildPath(evidenceRoot, "branch-validation", report.Route, DateTimeOffset.UtcNow, "json");
        var payload = JsonSerializer.Serialize(BranchValidationEvidenceEnvelope.From(report), JsonOptions);
        await File.WriteAllTextAsync(outputPath, payload, cancellationToken);
        return new EvidenceWriteResult("json", outputPath);
    }
}
