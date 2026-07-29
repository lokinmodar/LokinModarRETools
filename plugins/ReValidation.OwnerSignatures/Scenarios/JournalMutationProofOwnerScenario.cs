using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class JournalMutationProofOwnerScenario(
    OwnerHookProofExecutor executor,
    string targetId) : IValidationScenario
{
    private OwnerHookSession? session;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.mutation-proof",
        "Journal Mutation Proof",
        "Open the Journal list and attempt controlled mutation at the current journalProvider hook.",
        [ValidationRoute.OwnerSignatures]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ScenarioPreconditionResult(true, null));

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        session = await executor.CaptureAsync(targetId, cancellationToken);
        return new ScenarioCapture("Journal mutation proof armed", new JsonObject
        {
            ["ownerHook"] = session.BuildEvidence().ToJson(),
        });
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
}
