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
        var targetsReport = group.Targets
            .Select(target => new TargetProofRecord(
                target.TargetId,
                session.StageRecords.Any(stage => stage.Stage is OwnerHookProofStage.HitObserved && stage.Status is OwnerHookProofStatus.Passed) ? "passed" : "not-observed",
                resolution.MatchCount,
                resolution.Rva is null ? null : checked((long)resolution.Rva.Value),
                session.Hook.ObservedHitCount,
                session.StageRecords.Any(stage => stage.Stage is OwnerHookProofStage.HookInstalled && stage.Status is OwnerHookProofStatus.Passed),
                true,
                true,
                null))
            .ToArray();
        return new ProofGroupRunReport(group.GroupId, targetsReport, "Owner tooltip proof completed through the generic owner hook pipeline.");
    }

    private static ProofGroupRunReport Blocked(ProofGroupDefinition group, string reason, SignatureResolution? resolution = null) =>
        new(
            group.GroupId,
            group.Targets.Select(target => new TargetProofRecord(target.TargetId, "blocked", resolution?.MatchCount ?? 0, resolution?.Rva is null ? null : checked((long)resolution.Rva.Value), 0, false, false, false, reason)).ToArray(),
            reason);
}
