using ReValidation.Common.Abstractions;

namespace ReValidation.Common.Execution;

public sealed class ValidationScenarioRegistry
{
    private readonly IReadOnlyDictionary<string, IValidationScenario> scenarios;

    public ValidationScenarioRegistry(IEnumerable<IValidationScenario> scenarios)
    {
        this.scenarios = scenarios.ToDictionary(scenario => scenario.Definition.Id, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IValidationScenario> Scenarios => scenarios.Values.ToArray();

    public bool TryGet(string id, out IValidationScenario? scenario) => scenarios.TryGetValue(id, out scenario);
}
