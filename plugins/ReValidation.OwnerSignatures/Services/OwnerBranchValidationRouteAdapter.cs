using ReValidation.Common.Discovery;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using ReValidation.OwnerSignatures.Runtime;

namespace ReValidation.OwnerSignatures.Services;

public interface ISignatureResolutionProvider
{
    SignatureResolution GetResolution(string signatureId);
}

public sealed class OwnerBranchValidationRouteAdapter(
    ISignatureResolutionProvider resolutionProvider,
    IOwnerTooltipProofExecutor? itemTooltipExecutor = null,
    IOwnerTooltipProofExecutor? actionTooltipExecutor = null) : IBranchValidationRouteAdapter
{
    private readonly ISignatureResolutionProvider resolutionProvider = resolutionProvider ?? throw new ArgumentNullException(nameof(resolutionProvider));
    private readonly IOwnerTooltipProofExecutor? itemTooltipExecutor = itemTooltipExecutor;
    private readonly IOwnerTooltipProofExecutor? actionTooltipExecutor = actionTooltipExecutor;

    public ValidationRoute Route => ValidationRoute.OwnerSignatures;

    public ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.CueFamily switch
        {
            CueFamily.TooltipItemDetail => RunTooltipAsync(group, "itemTooltip", itemTooltipExecutor, requiredProofLevel, cancellationToken),
            CueFamily.TooltipActionDetail => RunTooltipAsync(group, "actionTooltip", actionTooltipExecutor, requiredProofLevel, cancellationToken),
            _ => ValueTask.FromResult(Blocked(group, "Cue family is not supported by the owner route yet.")),
        };
    }

    private async ValueTask<ProofGroupRunReport> RunTooltipAsync(
        ProofGroupDefinition group,
        string signatureId,
        IOwnerTooltipProofExecutor? executor,
        int requiredProofLevel,
        CancellationToken cancellationToken)
    {
        var resolution = resolutionProvider.GetResolution(signatureId);
        if (resolution.MatchCount != 1)
            return Blocked(group, "Signature resolution was not unique.", resolution);

        if (executor is null)
            return Blocked(group, "Tooltip proof executor is not configured.", resolution);

        var report = await executor.RunAsync(group, requiredProofLevel, cancellationToken);
        var targets = report.Targets
            .Select(target => new TargetProofRecord(
                target.TargetId,
                target.Verdict,
                resolution.MatchCount,
                resolution.Rva is null ? null : checked((long)resolution.Rva.Value),
                target.ObservedHitCount,
                target.HookInstalled,
                target.EffectApplied,
                target.EffectRestored,
                target.BlockingReason))
            .ToArray();
        return new ProofGroupRunReport(report.GroupId, targets, report.ArtifactSummary);
    }

    private static ProofGroupRunReport Blocked(ProofGroupDefinition group, string reason, SignatureResolution? resolution = null) =>
        new(
            group.GroupId,
            group.Targets.Select(target => new TargetProofRecord(target.TargetId, "blocked", resolution?.MatchCount ?? 0, resolution?.Rva is null ? null : checked((long)resolution.Rva.Value), 0, false, false, false, reason)).ToArray(),
            reason);
}
