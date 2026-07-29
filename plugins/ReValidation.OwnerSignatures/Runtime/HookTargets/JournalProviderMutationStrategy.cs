using System.Text.Json.Nodes;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed class JournalProviderMutationStrategy : IHookMutationStrategy
{
    public ValueTask<ScenarioOverrideTicket?> ApplyAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.MutationAttempted,
            OwnerHookProofStatus.EffectNotProven,
            "Journal mutation was not applied because a safe semantic control is not confirmed.",
            new JsonObject()));

        return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket(
            "Journal mutation control was not applied.",
            new JsonObject { ["status"] = "effect_not_proven" }));
    }

    public ValueTask<ScenarioAssertResult?> AssertAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.EffectAsserted,
            OwnerHookProofStatus.EffectNotProven,
            "Journal semantic mutation effect is not proven.",
            new JsonObject()));

        return ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(
            false,
            "Journal mutation effect was not proven.",
            ["effect_not_proven"]));
    }

    public ValueTask<ScenarioRestoreResult> RestoreAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.RestoreAttempted,
            OwnerHookProofStatus.Passed,
            "No Journal mutation was applied; restore is not required.",
            new JsonObject()));

        return ValueTask.FromResult(new ScenarioRestoreResult(
            true,
            "No Journal mutation was applied; restore is not required.",
            []));
    }
}
