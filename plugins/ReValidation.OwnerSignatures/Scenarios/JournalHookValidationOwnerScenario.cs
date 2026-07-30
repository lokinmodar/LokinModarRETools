using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class JournalHookValidationOwnerScenario(
    OwnerHookProofExecutor executor,
    string targetId) : IValidationScenario, IArmableValidationScenario
{
    private OwnerHookSession? session;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.hook-validation",
        "Journal Hook Validation",
        "Open the Journal list and trigger the current journalProvider hook.",
        [ValidationRoute.OwnerSignatures]);

    public string ArmPrompt => "Arm the scenario, then open the Journal list.";

    public bool RequiresArming => true;

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(executor.ValidateTarget(targetId));
    }

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var activeSession = RequireArmedSession();
        try
        {
            await executor.CaptureArmedAsync(activeSession, cancellationToken);
            RecordDisarm(activeSession);
            session = null;
            var requiredProofObserved = HasPassedStage(activeSession, OwnerHookProofStage.SignatureResolved)
                && HasPassedStage(activeSession, OwnerHookProofStage.HookInstalled)
                && HasPassedStage(activeSession, OwnerHookProofStage.HitObserved)
                && HasPassedStage(activeSession, OwnerHookProofStage.ContextCaptured);
            return new ScenarioCapture(
                "Journal hook stages captured",
                new JsonObject
                {
                    ["ownerHook"] = activeSession.BuildEvidence().ToJson(),
                },
                requiredProofObserved,
                requiredProofObserved ? null : "Journal hook context was not observed.");
        }
        finally
        {
            if (ReferenceEquals(session, activeSession))
            {
                session = null;
                activeSession.Dispose();
            }
        }
    }

    public ValueTask ArmAsync(CancellationToken cancellationToken)
    {
        if (session is not null)
            throw new InvalidOperationException("Journal hook validation is already armed.");

        session = executor.ArmAsync(targetId, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(session is { Hook.ObservedHitCount: > 0 }
            ? new ScenarioArmState(true, "Journal hook cue observed.")
            : new ScenarioArmState(false, "Waiting for the Journal hook cue."));

    public ValueTask DisarmAsync(CancellationToken cancellationToken)
    {
        if (session is not null)
            RecordDisarm(session);
        session = null;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioCompareResult?>(null);

    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioOverrideTicket?>(null);

    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioAssertResult?>(null);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        DisarmAsync(cancellationToken);
        return ValueTask.FromResult(new ScenarioRestoreResult(true, "Journal hook session disposed.", []));
    }

    private OwnerHookSession RequireArmedSession() =>
        session ?? throw new InvalidOperationException("Journal hook validation must be armed before capture.");

    private static void RecordDisarm(OwnerHookSession activeSession)
    {
        activeSession.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.RestoreAttempted,
            OwnerHookProofStatus.Passed,
            "Journal hook session disposed.",
            new JsonObject()));
        activeSession.Dispose();
    }

    private static bool HasPassedStage(OwnerHookSession session, OwnerHookProofStage stage) =>
        session.StageRecords.Any(record => record.Stage == stage && record.Status is OwnerHookProofStatus.Passed);
}
