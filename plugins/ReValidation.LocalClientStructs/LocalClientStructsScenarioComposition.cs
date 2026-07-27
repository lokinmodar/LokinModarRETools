using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs;

public static class LocalClientStructsScenarioComposition
{
    private const string BlockingReason = "Local ClientStructs runtime probes are not configured.";

    public static ValidationScenarioRegistry CreateRegistry() =>
        new(
        [
            new JournalCompletedEntriesLocalScenario(
                new UnavailableJournalProbe(),
                runtimeBlockingReason: BlockingReason),
            new TooltipItemDetailLocalScenario(
                new UnavailableTooltipProbe(),
                runtimeBlockingReason: BlockingReason),
            new TooltipActionDetailLocalScenario(
                new UnavailableTooltipProbe(),
                runtimeBlockingReason: BlockingReason),
        ]);

    public static ValidationScenarioRegistry CreateRegistry(LocalClientStructsScenarioDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        return new ValidationScenarioRegistry(
        [
            new JournalCompletedEntriesLocalScenario(
                dependencies.JournalProbe,
                dependencies.AvailabilityDetector,
                dependencies.JournalComparisonSource,
                supportsMutationProof: dependencies.SupportsJournalMutationProof,
                mutationBlockingReason: dependencies.JournalMutationBlockingReason),
            new TooltipItemDetailLocalScenario(
                dependencies.ItemTooltipProbe,
                dependencies.AvailabilityDetector,
                dependencies.ItemTooltipComparisonSource),
            new TooltipActionDetailLocalScenario(
                dependencies.ActionTooltipProbe,
                dependencies.AvailabilityDetector,
                dependencies.ActionTooltipComparisonSource),
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
}
