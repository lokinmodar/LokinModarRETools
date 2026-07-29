using ReValidation.Common.Abstractions;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures;

public static class OwnerSignaturesScenarioComposition
{
    private const string BlockingReason = "Owner-signature runtime probes and signature inputs are not configured.";
    private static readonly SignatureRequirement JournalRequirement = new("journalProvider", "E8 ?? ?? ?? ?? 41 88 84 2E", mustBeUnique: true);
    private static readonly SignatureRequirement ItemTooltipRequirement = new("itemTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA", mustBeUnique: true);
    private static readonly SignatureRequirement ActionTooltipRequirement = new("actionTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 48 8B 42 28 4C 8B FA 48 8B F1 49 8B E8", mustBeUnique: true);

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

        var scenarios = new List<IValidationScenario>
        {
            new JournalCompletedEntriesOwnerScenario(
                dependencies.JournalProbe,
                [JournalRequirement],
                FindResolutions(dependencies.Resolutions, JournalRequirement.Id),
                comparisonSource: dependencies.JournalComparisonSource,
                supportsMutationProof: dependencies.SupportsJournalMutationProof,
                mutationBlockingReason: dependencies.JournalMutationBlockingReason),
            new TooltipItemDetailOwnerScenario(
                dependencies.ItemTooltipProbe,
                dependencies.HookProofExecutor,
                dependencies.ItemTooltipComparisonSource),
            new TooltipActionDetailOwnerScenario(
                dependencies.ActionTooltipProbe,
                dependencies.HookProofExecutor,
                dependencies.ActionTooltipComparisonSource),
        };

        if (dependencies.HookProofExecutor is not null && dependencies.HookTargets is not null)
        {
            scenarios.Add(new JournalHookValidationOwnerScenario(
                dependencies.HookProofExecutor,
                JournalHookTargetIds.JournalProvider));
            scenarios.Add(new JournalMutationProofOwnerScenario(
                dependencies.HookProofExecutor,
                JournalHookTargetIds.JournalProvider));
        }

        return new ValidationScenarioRegistry(scenarios);
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
