using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures;

public static class OwnerSignaturesScenarioComposition
{
    private const string BlockingReason = "Owner-signature runtime probes and signature inputs are not configured.";
    private static readonly SignatureRequirement JournalRequirement = new("journalProvider", "48 89 ?? ??", mustBeUnique: true);
    private static readonly SignatureRequirement ItemTooltipRequirement = new("itemTooltip", "48 89 ?? ??", mustBeUnique: true);
    private static readonly SignatureRequirement ActionTooltipRequirement = new("actionTooltip", "48 89 ?? ??", mustBeUnique: true);

    public static ValidationScenarioRegistry CreateRegistry() =>
        new(
        [
            new JournalCompletedEntriesOwnerScenario(
                new UnavailableJournalProbe(),
                runtimeBlockingReason: BlockingReason),
            new TooltipItemDetailOwnerScenario(
                new UnavailableTooltipProbe(),
                runtimeBlockingReason: BlockingReason),
            new TooltipActionDetailOwnerScenario(
                new UnavailableTooltipProbe(),
                runtimeBlockingReason: BlockingReason),
        ]);

    public static ValidationScenarioRegistry CreateRegistry(OwnerSignaturesScenarioDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        return new ValidationScenarioRegistry(
        [
            new JournalCompletedEntriesOwnerScenario(
                dependencies.JournalProbe,
                [JournalRequirement],
                FindResolutions(dependencies.Resolutions, JournalRequirement.Id),
                comparisonSource: dependencies.JournalComparisonSource),
            new TooltipItemDetailOwnerScenario(
                dependencies.ItemTooltipProbe,
                [ItemTooltipRequirement],
                FindResolutions(dependencies.Resolutions, ItemTooltipRequirement.Id),
                comparisonSource: dependencies.ItemTooltipComparisonSource),
            new TooltipActionDetailOwnerScenario(
                dependencies.ActionTooltipProbe,
                [ActionTooltipRequirement],
                FindResolutions(dependencies.Resolutions, ActionTooltipRequirement.Id),
                comparisonSource: dependencies.ActionTooltipComparisonSource),
        ]);
    }

    private sealed class UnavailableJournalProbe : IJournalCompletedEntriesProbe
    {
        public ValueTask<JournalCompletedEntriesSnapshot> CaptureAsync(CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) => throw Unavailable();
    }

    private sealed class UnavailableTooltipProbe : ITooltipProbe
    {
        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) => throw Unavailable();
        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) => throw Unavailable();
    }

    private static InvalidOperationException Unavailable() => new(BlockingReason);

    private static IReadOnlyList<SignatureResolution> FindResolutions(
        IEnumerable<SignatureResolution> resolutions,
        string id) =>
        resolutions.Where(resolution => string.Equals(resolution.Id, id, StringComparison.Ordinal)).ToArray();
}
