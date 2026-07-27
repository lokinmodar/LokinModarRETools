using ReValidation.Common.Scenarios;

namespace ReValidation.OwnerSignatures.Services;

public sealed record OwnerSignaturesScenarioDependencies(
    IJournalCompletedEntriesProbe JournalProbe,
    ITooltipProbe ItemTooltipProbe,
    ITooltipProbe ActionTooltipProbe,
    IReadOnlyList<SignatureResolution> Resolutions,
    IJournalCompletedEntriesComparisonSource? JournalComparisonSource = null,
    ITooltipComparisonSource? ItemTooltipComparisonSource = null,
    ITooltipComparisonSource? ActionTooltipComparisonSource = null);
