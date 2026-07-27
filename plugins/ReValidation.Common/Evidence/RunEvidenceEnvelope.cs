using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed record RunEvidenceEnvelope(
    DateTimeOffset Timestamp,
    ScenarioRunReport Report,
    ValidationRoute Route,
    ValidationMode Mode,
    IReadOnlyDictionary<string, string?> Metadata)
{
    public static RunEvidenceEnvelope From(ScenarioRunReport report, ScenarioExecutionContext context) =>
        new(report.Timestamp, report, context.Route, context.Mode, context.Metadata);
}
