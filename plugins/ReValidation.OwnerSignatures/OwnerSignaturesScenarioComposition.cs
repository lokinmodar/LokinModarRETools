using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;

namespace ReValidation.OwnerSignatures;

public static class OwnerSignaturesScenarioComposition
{
    private const string BlockingReason = "Owner-signature runtime probes and signature inputs are not configured.";

    public static ValidationScenarioRegistry CreateRegistry() =>
        new(
        [
            CreateScenario("journal.completed-entries", "Journal Completed Entries", "Open the completed Journal list."),
            CreateScenario("tooltip.item-detail", "Tooltip Item Detail", "Open an item tooltip."),
            CreateScenario("tooltip.action-detail", "Tooltip Action Detail", "Open an action tooltip."),
        ]);

    private static NotConfiguredValidationScenario CreateScenario(string id, string name, string description) =>
        new(new ValidationScenarioDefinition(id, name, description, [ValidationRoute.OwnerSignatures]), BlockingReason);
}
