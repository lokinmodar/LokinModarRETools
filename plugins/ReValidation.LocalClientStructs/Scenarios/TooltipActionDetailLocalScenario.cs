using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Scenarios;

public sealed class TooltipActionDetailLocalScenario(
    ITooltipProbe probe,
    LocalClientStructsAvailabilityDetector? availabilityDetector = null)
    : TooltipValidationScenarioBase(
        probe,
        new ValidationScenarioDefinition(
            "tooltip.action-detail",
            "Tooltip Action Detail",
            "Open an action tooltip.",
            [ValidationRoute.LocalClientStructs]),
        "action")
{
    public override ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        if (availabilityDetector is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Local ClientStructs availability detector is required."));

        var availability = availabilityDetector.Evaluate(context.Mode);
        return ValueTask.FromResult(new ScenarioPreconditionResult(availability.IsAvailable, availability.BlockingReason));
    }
}
