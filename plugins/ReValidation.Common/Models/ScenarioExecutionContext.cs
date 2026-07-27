namespace ReValidation.Common.Models;

public sealed record ScenarioExecutionContext(
    ValidationRoute Route,
    ValidationMode Mode,
    IReadOnlyDictionary<string, string?> Metadata,
    string EvidenceRoot)
{
    public static ScenarioExecutionContext CreateForTests(ValidationRoute route, ValidationMode mode, string? evidenceRoot = null) =>
        new(route, mode, new Dictionary<string, string?>(), evidenceRoot ?? Path.GetTempPath());
}
