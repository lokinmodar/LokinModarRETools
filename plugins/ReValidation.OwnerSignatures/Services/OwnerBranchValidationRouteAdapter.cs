using ReValidation.Common.Discovery;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
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

    public ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        cancellationToken.ThrowIfCancellationRequested();

        var targetId = group.CueFamily switch
        {
            CueFamily.TooltipItemDetail => "itemTooltip",
            CueFamily.TooltipActionDetail => "actionTooltip",
            _ => null,
        };

        if (targetId is null)
            return ValueTask.FromResult(Blocked(group, "Cue family is not supported by the owner route yet."));

        var target = targets.Get(targetId);
        var resolution = resolutionProvider.GetResolution(target.SignatureId);
        if (resolution.MatchCount != 1)
            return ValueTask.FromResult(Blocked(group, "Signature resolution was not unique.", resolution));

        _ = proofExecutor.ValidateTarget(targetId);
        return ValueTask.FromResult(Blocked(
            group,
            "Owner tooltip branch validation requires an interactive arm/cue window; run the individual armed tooltip scenario.",
            resolution));
    }

    private static ProofGroupRunReport Blocked(ProofGroupDefinition group, string reason, SignatureResolution? resolution = null) =>
        new(
            group.GroupId,
            group.Targets.Select(target => new TargetProofRecord(target.TargetId, "blocked", resolution?.MatchCount ?? 0, resolution?.Rva is null ? null : checked((long)resolution.Rva.Value), 0, false, false, false, reason)).ToArray(),
            reason);

}
