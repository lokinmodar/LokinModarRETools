using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public sealed record RunEvidenceEnvelope(
    DateTimeOffset Timestamp,
    string RunId,
    string ScenarioId,
    ValidationRoute Route,
    ValidationMode Mode,
    string Status,
    string? FailedPhase)
{
    public static RunEvidenceEnvelope From(ScenarioRunReport report, ScenarioExecutionContext context) =>
        new(report.Timestamp, report.EvidenceRunId, report.Scenario.Id, context.Route, context.Mode, report.Status, report.FailedPhase);
}
