using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Scenarios;

namespace ReValidation.OwnerSignatures;

public static class OwnerSignaturesScenarioComposition
{
    private const string BlockingReason = "Owner-signature runtime probes and signature inputs are not configured.";

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
