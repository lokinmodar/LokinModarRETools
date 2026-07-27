using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Scenarios;

public sealed class TooltipActionDetailLocalScenario(
    ITooltipProbe probe,
    LocalClientStructsAvailabilityDetector? availabilityDetector = null,
    ITooltipComparisonSource? comparisonSource = null,
    string? runtimeBlockingReason = null)
    : TooltipValidationScenarioBase(
        probe,
        new ValidationScenarioDefinition(
            "tooltip.action-detail",
            "Tooltip Action Detail",
            "Open an action tooltip.",
            [ValidationRoute.LocalClientStructs]),
        "action",
        comparisonSource,
        runtimeBlockingReason)
{
    public override ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var runtimeFailure = GetRuntimePreconditionFailure();
        if (runtimeFailure is not null)
            return ValueTask.FromResult(runtimeFailure);

        if (availabilityDetector is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Local ClientStructs availability detector is required."));

        var availability = availabilityDetector.Evaluate(context.Mode);
        if (!availability.IsAvailable)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, availability.BlockingReason));

        return ValueTask.FromResult(
            GetComparisonPreconditionFailure(context)
            ?? new ScenarioPreconditionResult(true, null));
    }
}
