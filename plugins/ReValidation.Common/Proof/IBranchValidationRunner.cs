namespace ReValidation.Common.Proof;

public interface IBranchValidationRunner
{
    ValueTask<BranchValidationRunReport> RunAsync(
        BranchValidationPlan plan,
        IBranchValidationRouteAdapter routeAdapter,
        string evidenceRoot,
        CancellationToken cancellationToken);
}
