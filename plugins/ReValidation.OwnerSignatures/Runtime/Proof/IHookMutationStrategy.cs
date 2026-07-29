using ReValidation.Common.Models;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public interface IHookMutationStrategy
{
    ValueTask<ScenarioOverrideTicket?> ApplyAsync(OwnerHookSession session, CancellationToken cancellationToken);

    ValueTask<ScenarioAssertResult?> AssertAsync(OwnerHookSession session, CancellationToken cancellationToken);

    ValueTask<ScenarioRestoreResult> RestoreAsync(OwnerHookSession session, CancellationToken cancellationToken);
}
