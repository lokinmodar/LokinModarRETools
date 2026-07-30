namespace ReValidation.OwnerSignatures.Services;

public enum SignatureAddressResolution
{
    MatchAddress,
    FollowLeadingCallOrJump,
}

public sealed record SignatureRequirement
{
    public SignatureRequirement(
        string id,
        string pattern,
        bool mustBeUnique,
        SignatureAddressResolution addressResolution = SignatureAddressResolution.MatchAddress)
    {
        Id = id;
        Pattern = pattern;
        MustBeUnique = mustBeUnique;
        AddressResolution = addressResolution;
    }

    public string Id { get; }

    public string Pattern { get; }

    public bool MustBeUnique { get; }

    public SignatureAddressResolution AddressResolution { get; }
}
