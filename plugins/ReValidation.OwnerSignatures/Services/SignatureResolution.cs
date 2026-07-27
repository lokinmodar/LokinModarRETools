namespace ReValidation.OwnerSignatures.Services;

public sealed record SignatureResolution
{
    public SignatureResolution(string id, int matchCount, ulong? rva, string? failureReason)
    {
        Id = id;
        MatchCount = matchCount;
        Rva = rva;
        FailureReason = failureReason;
    }

    public string Id { get; }
    public int MatchCount { get; }
    public ulong? Rva { get; }
    public string? FailureReason { get; }
}
