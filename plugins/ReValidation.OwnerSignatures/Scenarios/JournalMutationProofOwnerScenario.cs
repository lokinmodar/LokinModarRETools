using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class JournalMutationProofOwnerScenario(
    OwnerHookProofExecutor executor,
    string targetId) : IValidationScenario, IArmableValidationScenario
{
    private OwnerHookSession? session;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.mutation-proof",
        "Journal Mutation Proof",
        "Open the Journal list and attempt controlled mutation at the current journalProvider hook.",
        [ValidationRoute.OwnerSignatures]);

    public string ArmPrompt => "Arm the scenario, then open the Journal list.";

    public bool RequiresArming => true;

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ScenarioPreconditionResult(true, null));

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var activeSession = RequireArmedSession();
        try
        {
            await executor.CaptureArmedAsync(activeSession, cancellationToken);
            if (context.Mode is ValidationMode.CaptureOnly or ValidationMode.Compare)
            {
                RecordDisarm(activeSession);
                session = null;
            }

            var capture = new ScenarioCapture("Journal mutation proof armed", new JsonObject
            {
                ["ownerHook"] = activeSession.BuildEvidence().ToJson(),
            });
            return capture;
        }
        catch
        {
            await DisarmAsync(CancellationToken.None);
            throw;
        }
    }

    public ValueTask ArmAsync(CancellationToken cancellationToken)
    {
        if (session is not null)
            throw new InvalidOperationException("Journal mutation proof is already armed.");

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

    public async ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
    {
        if (session is null)
            return null;

        var ticket = await session.MutationStrategy.ApplyAsync(session, cancellationToken);
        UpdateEvidence(capture);
        return ticket;
    }

    public async ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        if (session is null)
            return null;

        var assertion = await session.MutationStrategy.AssertAsync(session, cancellationToken);
        UpdateEvidence(capture);
        return assertion;
    }

    public async ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        if (session is null)
            return new ScenarioRestoreResult(true, "No Journal hook session was active.", []);

        var activeSession = session;
        session = null;
        try
        {
            var restore = await activeSession.MutationStrategy.RestoreAsync(activeSession, cancellationToken);
            capture.Data["ownerHook"] = activeSession.BuildEvidence().ToJson();
            return restore;
        }
        finally
        {
            activeSession.Dispose();
        }
    }

    private void UpdateEvidence(ScenarioCapture capture)
    {
        if (session is not null)
            capture.Data["ownerHook"] = session.BuildEvidence().ToJson();
    }

    private OwnerHookSession RequireArmedSession() =>
        session ?? throw new InvalidOperationException("Journal mutation proof must be armed before capture.");

    private static void RecordDisarm(OwnerHookSession activeSession)
    {
        activeSession.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.RestoreAttempted,
            OwnerHookProofStatus.Passed,
            "Journal hook session disposed.",
            new JsonObject()));
        activeSession.Dispose();
    }
}
