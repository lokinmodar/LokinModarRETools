namespace ReValidation.Common.Models;

public sealed record ScenarioExecutionContext(
    ValidationRoute Route,
    ValidationMode Mode,
    IReadOnlyDictionary<string, string?> Metadata)
{
    public static ScenarioExecutionContext CreateForTests(ValidationRoute route, ValidationMode mode) =>
        new(route, mode, new Dictionary<string, string?>());
}
