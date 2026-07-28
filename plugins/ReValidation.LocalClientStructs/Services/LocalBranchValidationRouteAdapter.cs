using ReValidation.Common.Discovery;
using ReValidation.Common.Models;
using ReValidation.Common.Proof;
using ReValidation.LocalClientStructs.Runtime;

namespace ReValidation.LocalClientStructs.Services;

public sealed class LocalBranchValidationRouteAdapter(
    ITooltipProofExecutor itemTooltipExecutor,
    ITooltipProofExecutor actionTooltipExecutor) : IBranchValidationRouteAdapter
{
    private readonly ITooltipProofExecutor itemTooltipExecutor = itemTooltipExecutor ?? throw new ArgumentNullException(nameof(itemTooltipExecutor));
    private readonly ITooltipProofExecutor actionTooltipExecutor = actionTooltipExecutor ?? throw new ArgumentNullException(nameof(actionTooltipExecutor));

    public ValidationRoute Route => ValidationRoute.LocalClientStructs;

    public ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.CueFamily switch
        {
            CueFamily.TooltipItemDetail => itemTooltipExecutor.RunAsync(group, requiredProofLevel, cancellationToken),
            CueFamily.TooltipActionDetail => actionTooltipExecutor.RunAsync(group, requiredProofLevel, cancellationToken),
            _ => ValueTask.FromResult(Blocked(group, "Cue family is not supported by the local route yet.")),
        };
    }

    private static ProofGroupRunReport Blocked(ProofGroupDefinition group, string reason) =>
        new(
            group.GroupId,
            group.Targets.Select(target => new TargetProofRecord(target.TargetId, "blocked", 0, null, 0, false, false, false, reason)).ToArray(),
            reason);
}
