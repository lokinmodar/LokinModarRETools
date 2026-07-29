using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class JournalHookValidationOwnerScenario(
    OwnerHookProofExecutor executor,
    string targetId) : IValidationScenario
{
    private OwnerHookSession? session;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.hook-validation",
        "Journal Hook Validation",
        "Open the Journal list and trigger the current journalProvider hook.",
        [ValidationRoute.OwnerSignatures]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ScenarioPreconditionResult(true, null));

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        session = await executor.CaptureAsync(targetId, cancellationToken);
        return new ScenarioCapture("Journal hook stages captured", new JsonObject
        {
            ["ownerHook"] = session.BuildEvidence().ToJson(),
        });
    }

    public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioCompareResult?>(null);

    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioOverrideTicket?>(null);

    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioAssertResult?>(null);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        session?.Dispose();
        session = null;
        return ValueTask.FromResult(new ScenarioRestoreResult(true, "Journal hook session disposed.", []));
    }
}
