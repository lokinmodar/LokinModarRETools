using ReValidation.Common.Discovery;
using ReValidation.Common.Proof;

namespace ReValidation.Common.UI;

public sealed record BranchValidationWorkflow(
    IClientStructsDiscoveryService DiscoveryService,
    ProofPlanBuilder PlanBuilder,
    IBranchValidationRunner Runner,
    IBranchValidationRouteAdapter RouteAdapter,
    ClientStructsDiscoveryOptions DiscoveryOptions,
    string EvidenceRoot);
