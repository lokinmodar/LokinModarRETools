using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs;
using ReValidation.LocalClientStructs.Scenarios;
using ReValidation.OwnerSignatures;
using ReValidation.OwnerSignatures.Scenarios;
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
        Assert.DoesNotContain(registry.Scenarios, scenario => scenario is NotConfiguredValidationScenario);

        if (route is ValidationRoute.LocalClientStructs)
        {
            Assert.Contains(registry.Scenarios, scenario => scenario is JournalCompletedEntriesLocalScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipItemDetailLocalScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipActionDetailLocalScenario);
        }
        else
        {
            Assert.Contains(registry.Scenarios, scenario => scenario is JournalCompletedEntriesOwnerScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipItemDetailOwnerScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipActionDetailOwnerScenario);
        }
    }

    [Theory]
    [InlineData(ValidationRoute.LocalClientStructs)]
    [InlineData(ValidationRoute.OwnerSignatures)]
    public async Task ComposedScenarios_BlockOnMissingRuntimeAdapters(ValidationRoute route)
    {
        var registry = route is ValidationRoute.LocalClientStructs
            ? LocalClientStructsScenarioComposition.CreateRegistry()
            : OwnerSignaturesScenarioComposition.CreateRegistry();
        var context = ScenarioExecutionContext.CreateForTests(route, ValidationMode.CaptureOnly);

        foreach (var scenario in registry.Scenarios)
        {
            var precondition = await scenario.ValidateAsync(context, CancellationToken.None);

            Assert.False(precondition.CanRun);
            Assert.Contains("runtime", precondition.BlockingReason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
