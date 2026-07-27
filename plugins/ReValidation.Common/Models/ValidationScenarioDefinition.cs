namespace ReValidation.Common.Models;

public sealed record ValidationScenarioDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<ValidationRoute> SupportedRoutes)
{
    public ValidationScenarioDefinition(string id, string name)
        : this(id, name, string.Empty, [])
    {
    }
}
