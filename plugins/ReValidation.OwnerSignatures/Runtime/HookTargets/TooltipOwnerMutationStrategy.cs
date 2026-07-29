using System.Text.Json.Nodes;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed class TooltipHookContextCapture(string detailKind) : IHookContextCapture
{
    public JsonObject Capture(JsonObject rawContext)
    {
        if (rawContext["detailKind"]?.GetValue<string>() != detailKind)
            return new JsonObject();

        return new JsonObject { ["detailKind"] = detailKind };
    }
}

public sealed class TooltipOwnerMutationStrategy(ITooltipProbe probe) : IHookMutationStrategy
{
    private const string Sentinel = "[REVALIDATION] Tooltip Sentinel";
    private readonly ITooltipProbe probe = probe ?? throw new ArgumentNullException(nameof(probe));

    public async ValueTask<ScenarioOverrideTicket?> ApplyAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        var ticket = await probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken);
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.MutationAttempted,
            ticket is null ? OwnerHookProofStatus.EffectNotProven : OwnerHookProofStatus.Passed,
            ticket is null ? "Tooltip sentinel override was not applied." : "Tooltip sentinel override applied.",
            new JsonObject()));
        return ticket;
    }

    public async ValueTask<ScenarioAssertResult?> AssertAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        var assertion = await probe.AssertSentinelAsync(Sentinel, cancellationToken);
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.EffectAsserted,
            assertion is { Passed: true } ? OwnerHookProofStatus.Passed : OwnerHookProofStatus.EffectNotProven,
            assertion?.Summary ?? "Tooltip sentinel was not visibly asserted.",
            new JsonObject()));
        return assertion;
    }

    public async ValueTask<ScenarioRestoreResult> RestoreAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        var restore = await probe.RestoreAsync(cancellationToken);
        session.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.RestoreAttempted,
            restore.Passed ? OwnerHookProofStatus.Passed : OwnerHookProofStatus.EffectNotProven,
            restore.Summary,
            new JsonObject()));
        return restore;
    }
}
