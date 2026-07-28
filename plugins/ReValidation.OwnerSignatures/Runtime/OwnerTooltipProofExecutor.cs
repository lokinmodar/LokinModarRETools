using ReValidation.Common.Proof;
using ReValidation.Common.Scenarios;

namespace ReValidation.OwnerSignatures.Runtime;

public interface IOwnerTooltipProofExecutor
{
    ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken);
}

public sealed class OwnerTooltipProofExecutor(ITooltipProbe probe, ITooltipProofHookFactory hookFactory) : IOwnerTooltipProofExecutor
{
    private const string Sentinel = "[REVALIDATION] Tooltip Sentinel";
    private readonly ITooltipProbe probe = probe ?? throw new ArgumentNullException(nameof(probe));
    private readonly ITooltipProofHookFactory hookFactory = hookFactory ?? throw new ArgumentNullException(nameof(hookFactory));

    public async ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        var targets = new List<TargetProofRecord>();
        foreach (var target in group.Targets)
            targets.Add(await RunTargetAsync(target.TargetId, cancellationToken));
        return new ProofGroupRunReport(group.GroupId, targets, "Owner tooltip sentinel lifecycle completed.");
    }

    private async ValueTask<TargetProofRecord> RunTargetAsync(string targetId, CancellationToken cancellationToken)
    {
        ITooltipProofHook? hook = null;
        var applied = false;
        var restored = false;
        string? reason = null;
        try { hook = hookFactory.Create(targetId); hook.Enable(); await probe.CaptureAsync(cancellationToken); applied = await probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken) is not null; var assertion = await probe.AssertSentinelAsync(Sentinel, cancellationToken); if (assertion is null || !assertion.Passed) reason = "Tooltip sentinel was not visibly asserted."; }
        catch (Exception exception) when (exception is not OperationCanceledException) { reason = exception.Message; }
        finally
        {
            try { restored = (await probe.RestoreAsync(CancellationToken.None)).Passed; }
            catch (Exception exception) when (exception is not OperationCanceledException) { reason ??= exception.Message; }
            hook?.Dispose();
        }
        var hits = hook?.ObservedHitCount ?? 0;
        var installed = hook is not null;
        var verdict = reason is not null ? "blocked" : !installed ? "blocked" : hits == 0 ? "not-observed" : !restored ? "effect-not-proven" : "passed";
        reason ??= verdict == "not-observed" ? "Tooltip function hook did not observe a call." : verdict == "effect-not-proven" ? "Tooltip effect was not restored." : null;
        return new TargetProofRecord(targetId, verdict, 0, null, hits, installed, applied, restored, reason);
    }
}
