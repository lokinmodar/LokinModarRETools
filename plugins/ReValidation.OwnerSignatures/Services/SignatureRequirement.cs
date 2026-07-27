namespace ReValidation.OwnerSignatures.Services;

public sealed record SignatureRequirement
{
    public SignatureRequirement(string id, string pattern, bool mustBeUnique)
    {
        Id = id;
        Pattern = pattern;
        MustBeUnique = mustBeUnique;
    }

    public string Id { get; }
    public string Pattern { get; }
    public bool MustBeUnique { get; }
}
