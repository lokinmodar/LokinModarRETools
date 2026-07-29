using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class TooltipActionDetailOwnerScenario(
    ITooltipProbe probe,
    OwnerHookProofExecutor? proofExecutor = null,
    ITooltipComparisonSource? comparisonSource = null,
    string? runtimeBlockingReason = null) : OwnerTooltipValidationScenarioBase(
        probe,
        proofExecutor,
        comparisonSource,
        runtimeBlockingReason,
        "actionTooltip",
        "action",
        new ValidationScenarioDefinition(
            "tooltip.action-detail",
            "Tooltip Action Detail",
            "Open an action tooltip.",
            [ValidationRoute.OwnerSignatures]));
