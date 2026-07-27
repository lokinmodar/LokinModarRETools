namespace ReValidation.OwnerSignatures.Services;

public sealed record SignatureRequirement(string Id, string Pattern, bool MustBeUnique)
{
    public SignatureRequirement(string id, string pattern, bool mustBeUnique, object? compatibility = null)
        : this(id, pattern, mustBeUnique)
    {
    }
}
