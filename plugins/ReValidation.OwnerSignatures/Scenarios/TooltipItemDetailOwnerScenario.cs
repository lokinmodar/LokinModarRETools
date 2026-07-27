using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class TooltipItemDetailOwnerScenario : TooltipValidationScenarioBase
{
    private readonly IReadOnlyList<SignatureRequirement> requirements;
    private readonly IReadOnlyList<SignatureResolution> resolutions;
    private readonly SignatureGate signatureGate;

    public TooltipItemDetailOwnerScenario(
        ITooltipProbe probe,
        IEnumerable<SignatureRequirement>? requirements = null,
        IEnumerable<SignatureResolution>? resolutions = null,
        SignatureGate? signatureGate = null,
        ITooltipComparisonSource? comparisonSource = null,
        string? runtimeBlockingReason = null)
        : base(
            probe,
            new ValidationScenarioDefinition(
                "tooltip.item-detail",
                "Tooltip Item Detail",
                "Open an item tooltip.",
                [ValidationRoute.OwnerSignatures]),
            "item",
            comparisonSource,
            runtimeBlockingReason)
    {
        this.requirements = requirements?.ToArray() ?? [];
        this.resolutions = resolutions?.ToArray() ?? [];
        this.signatureGate = signatureGate ?? new SignatureGate();
    }

    public override ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var runtimeFailure = GetRuntimePreconditionFailure();
        if (runtimeFailure is not null)
            return ValueTask.FromResult(runtimeFailure);

        var routeValidation = TooltipOwnerScenarioValidation.Validate(requirements, resolutions, signatureGate);
        if (!routeValidation.CanRun)
            return ValueTask.FromResult(routeValidation);

        return ValueTask.FromResult(
            GetComparisonPreconditionFailure(context)
            ?? routeValidation);
    }
}
