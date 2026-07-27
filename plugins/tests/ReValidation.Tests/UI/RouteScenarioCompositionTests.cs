using ReValidation.Common.Models;
using ReValidation.LocalClientStructs;
using ReValidation.OwnerSignatures;
using Xunit;

public sealed class RouteScenarioCompositionTests
{
    [Theory]
    [InlineData(ValidationRoute.LocalClientStructs)]
    [InlineData(ValidationRoute.OwnerSignatures)]
    public void CreateRegistry_RegistersJournalAndTooltipScenarios(ValidationRoute route)
    {
        var registry = route is ValidationRoute.LocalClientStructs
            ? LocalClientStructsScenarioComposition.CreateRegistry()
            : OwnerSignaturesScenarioComposition.CreateRegistry();

        Assert.Equal(3, registry.Scenarios.Count);
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "journal.completed-entries");
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.item-detail");
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.action-detail");
        Assert.All(registry.Scenarios, scenario => Assert.Contains(route, scenario.Definition.SupportedRoutes));
    }
}
