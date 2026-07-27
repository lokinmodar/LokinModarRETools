using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Scenarios;

public sealed class TooltipItemDetailLocalScenario(
    ITooltipProbe probe,
    LocalClientStructsAvailabilityDetector? availabilityDetector = null)
    : TooltipValidationScenarioBase(
        probe,
        new ValidationScenarioDefinition(
            "tooltip.item-detail",
            "Tooltip Item Detail",
            "Open an item tooltip.",
            [ValidationRoute.LocalClientStructs]),
        "item")
{
    public override ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        if (availabilityDetector is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Local ClientStructs availability detector is required."));

        var availability = availabilityDetector.Evaluate(context.Mode);
        return ValueTask.FromResult(new ScenarioPreconditionResult(availability.IsAvailable, availability.BlockingReason));
    }
}
