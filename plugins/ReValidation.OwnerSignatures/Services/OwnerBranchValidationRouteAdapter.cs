using ReValidation.Common.Discovery;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Services;

public interface ISignatureResolutionProvider
{
    SignatureResolution GetResolution(string signatureId);
}

public sealed class OwnerBranchValidationRouteAdapter(
    ISignatureResolutionProvider resolutionProvider,
    OwnerHookProofExecutor proofExecutor,
    OwnerHookTargetRegistry targets) : IBranchValidationRouteAdapter
{
    private readonly ISignatureResolutionProvider resolutionProvider = resolutionProvider ?? throw new ArgumentNullException(nameof(resolutionProvider));
    private readonly OwnerHookProofExecutor proofExecutor = proofExecutor ?? throw new ArgumentNullException(nameof(proofExecutor));
    private readonly OwnerHookTargetRegistry targets = targets ?? throw new ArgumentNullException(nameof(targets));

    public ValidationRoute Route => ValidationRoute.OwnerSignatures;

    public async ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        var targetId = group.CueFamily switch
        {
            CueFamily.TooltipItemDetail => "itemTooltip",
            CueFamily.TooltipActionDetail => "actionTooltip",
            _ => null,
        };

        if (targetId is null)
            return Blocked(group, "Cue family is not supported by the owner route yet.");

        var target = targets.Get(targetId);
        var resolution = resolutionProvider.GetResolution(target.SignatureId);
        if (resolution.MatchCount != 1)
            return Blocked(group, "Signature resolution was not unique.", resolution);

        using var session = await proofExecutor.CaptureAsync(targetId, cancellationToken);
        var hookInstalled = HasPassedStage(session, OwnerHookProofStage.HookInstalled);
        var hookObserved = HasPassedStage(session, OwnerHookProofStage.HitObserved)
            && HasPassedStage(session, OwnerHookProofStage.ContextCaptured);
        if (!hookObserved)
            return CreateReport(group, resolution, session, "not-observed", hookInstalled, false, false, "Tooltip hook/context evidence was not observed.");

        var effectApplied = false;
        var effectAsserted = false;
        var effectRestored = false;
        string? reason = null;
        try
        {
            var ticket = await session.MutationStrategy.ApplyAsync(session, cancellationToken);
            effectApplied = ticket is not null;
            if (!effectApplied)
                reason = "Tooltip sentinel override was not applied.";
            else
            {
                var assertion = await session.MutationStrategy.AssertAsync(session, cancellationToken);
                effectAsserted = assertion is { Passed: true };
                if (!effectAsserted)
                    reason = assertion?.Summary ?? "Tooltip sentinel was not visibly asserted.";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            reason = exception.Message;
        }
        finally
        {
            try
            {
                var restore = await session.MutationStrategy.RestoreAsync(session, CancellationToken.None);
                effectRestored = effectApplied && restore.Passed;
                if (!restore.Passed)
                    reason ??= restore.Summary;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                reason ??= exception.Message;
            }
        }

        var passed = effectApplied && effectAsserted && effectRestored;
        return CreateReport(
            group,
            resolution,
            session,
            passed ? "passed" : "effect-not-proven",
            hookInstalled,
            effectApplied,
            effectRestored,
            reason ?? "Tooltip controlled mutation lifecycle was not proven.");
    }

    private static ProofGroupRunReport CreateReport(
        ProofGroupDefinition group,
        SignatureResolution resolution,
        OwnerHookSession session,
        string verdict,
        bool hookInstalled,
        bool effectApplied,
        bool effectRestored,
        string? blockingReason)
    {
        var targetsReport = group.Targets
            .Select(target => new TargetProofRecord(
                target.TargetId,
                verdict,
                resolution.MatchCount,
                resolution.Rva is null ? null : checked((long)resolution.Rva.Value),
                session.Hook.ObservedHitCount,
                hookInstalled,
                effectApplied,
                effectRestored,
                blockingReason))
            .ToArray();
        var summary = verdict is "passed"
            ? "Owner tooltip proof completed through the generic owner hook pipeline."
            : "Owner tooltip proof did not demonstrate the complete controlled lifecycle.";
        return new ProofGroupRunReport(group.GroupId, targetsReport, summary);
    }

    private static ProofGroupRunReport Blocked(ProofGroupDefinition group, string reason, SignatureResolution? resolution = null) =>
        new(
            group.GroupId,
            group.Targets.Select(target => new TargetProofRecord(target.TargetId, "blocked", resolution?.MatchCount ?? 0, resolution?.Rva is null ? null : checked((long)resolution.Rva.Value), 0, false, false, false, reason)).ToArray(),
            reason);

    private static bool HasPassedStage(OwnerHookSession session, OwnerHookProofStage stage) =>
        session.StageRecords.Any(record => record.Stage == stage && record.Status is OwnerHookProofStatus.Passed);
}
