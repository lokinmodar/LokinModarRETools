using ReValidation.Common.Models;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed class NoOpHookMutationStrategy(string message) : IHookMutationStrategy
{
    public ValueTask<ScenarioOverrideTicket?> ApplyAsync(OwnerHookSession session, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioOverrideTicket?>(null);

    public ValueTask<ScenarioAssertResult?> AssertAsync(OwnerHookSession session, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioAssertResult?>(null);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(OwnerHookSession session, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ScenarioRestoreResult(true, message, []));
}
