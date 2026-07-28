using ReValidation.Common.Proof;
using ReValidation.Common.Scenarios;

namespace ReValidation.LocalClientStructs.Runtime;

public interface ITooltipProofExecutor
{
    ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken);
}

public sealed class LocalTooltipProofExecutor(ITooltipProbe probe) : ITooltipProofExecutor
{
    private const string Sentinel = "[REVALIDATION] Tooltip Sentinel";
    private readonly ITooltipProbe probe = probe ?? throw new ArgumentNullException(nameof(probe));

    public async ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        var effectApplied = false;
        var effectRestored = false;
        string? blockingReason = null;

        try
        {
            await probe.CaptureAsync(cancellationToken);
            effectApplied = await probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken) is not null;
            var assertion = await probe.AssertSentinelAsync(Sentinel, cancellationToken);
            if (assertion is null || !assertion.Passed)
                blockingReason = "Tooltip sentinel was not visibly asserted.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            blockingReason = exception.Message;
        }
        finally
        {
            try
            {
                effectRestored = (await probe.RestoreAsync(CancellationToken.None)).Passed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                blockingReason ??= exception.Message;
            }
        }

        blockingReason ??= "Tooltip effect was exercised, but no target execution hook was proven.";
        var targets = group.Targets
            .Select(target => new TargetProofRecord(
                target.TargetId,
                "blocked",
                matchCount: 1,
                rva: null,
                observedHitCount: 0,
                hookInstalled: false,
                effectApplied,
                effectRestored,
                blockingReason))
            .ToArray();
        return new ProofGroupRunReport(group.GroupId, targets, "Local tooltip sentinel lifecycle completed.");
    }
}
