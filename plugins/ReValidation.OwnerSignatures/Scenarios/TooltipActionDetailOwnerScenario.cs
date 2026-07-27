using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class TooltipActionDetailOwnerScenario : TooltipValidationScenarioBase
{
    private readonly IReadOnlyList<SignatureRequirement> requirements;
    private readonly IReadOnlyList<SignatureResolution> resolutions;
    private readonly SignatureGate signatureGate;

    public TooltipActionDetailOwnerScenario(
        ITooltipProbe probe,
        IEnumerable<SignatureRequirement>? requirements = null,
        IEnumerable<SignatureResolution>? resolutions = null,
        SignatureGate? signatureGate = null)
        : base(
            probe,
            new ValidationScenarioDefinition(
                "tooltip.action-detail",
                "Tooltip Action Detail",
                "Open an action tooltip.",
                [ValidationRoute.OwnerSignatures]),
            "action")
    {
        this.requirements = requirements?.ToArray() ?? [];
        this.resolutions = resolutions?.ToArray() ?? [];
        this.signatureGate = signatureGate ?? new SignatureGate();
    }

    public override ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
        TooltipOwnerScenarioValidation.Validate(requirements, resolutions, signatureGate);
}

internal static class TooltipOwnerScenarioValidation
{
    public static ValueTask<ScenarioPreconditionResult> Validate(
        IReadOnlyList<SignatureRequirement> requirements,
        IReadOnlyList<SignatureResolution> resolutions,
        SignatureGate signatureGate)
    {
        if (requirements.Count == 0)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Tooltip signature requirements are required."));

        if (resolutions.Count == 0)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Tooltip signature resolutions are required."));

        var result = signatureGate.Evaluate(requirements, resolutions);
        var reason = result.CanRun
            ? null
            : $"Signature requirement '{result.FailingRequirementId}' is blocked: {result.FailureReason}.";
        return ValueTask.FromResult(new ScenarioPreconditionResult(result.CanRun, reason));
    }
}
