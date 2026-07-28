using ReValidation.Common.Models;

namespace ReValidation.Common.Proof;

public interface IBranchValidationRouteAdapter
{
    ValidationRoute Route { get; }

    ValueTask<ProofGroupRunReport> RunProofGroupAsync(
        ProofGroupDefinition group,
        int requiredProofLevel,
        CancellationToken cancellationToken);
}
