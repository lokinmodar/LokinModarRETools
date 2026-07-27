using ReValidation.Common.Scenarios;

namespace ReValidation.LocalClientStructs.Services;

public sealed record LocalClientStructsScenarioDependencies(
    IJournalCompletedEntriesProbe JournalProbe,
    ITooltipProbe ItemTooltipProbe,
    ITooltipProbe ActionTooltipProbe,
    LocalClientStructsAvailabilityDetector AvailabilityDetector,
    IJournalCompletedEntriesComparisonSource? JournalComparisonSource = null,
    ITooltipComparisonSource? ItemTooltipComparisonSource = null,
    ITooltipComparisonSource? ActionTooltipComparisonSource = null);
