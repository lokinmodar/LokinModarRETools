namespace ReValidation.OwnerSignatures.Services;

public sealed record SignatureResolution(string Id, int MatchCount, ulong? Rva, string? FailureReason)
{
    public SignatureResolution(string id, int matchCount, ulong? rva, string? failureReason, object? compatibility = null)
        : this(id, matchCount, rva, failureReason)
    {
    }
}
