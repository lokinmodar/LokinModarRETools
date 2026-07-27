namespace ReValidation.OwnerSignatures.Services;

public sealed record SignatureGateResult(bool CanRun, string? FailingRequirementId, string? FailureReason)
{
    public static SignatureGateResult Allowed() => new(true, null, null);

    public static SignatureGateResult Blocked(string failingRequirementId, string failureReason)
        => new(false, failingRequirementId, failureReason);
}
