namespace ReValidation.OwnerSignatures.Services;

public sealed class SignatureScannerResolver(ISignatureScanner scanner)
{
    private readonly ISignatureScanner scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

    public SignatureResolution Resolve(SignatureRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var matches = scanner.ScanAllText(requirement.Id, requirement.Pattern);
        if (matches.Count == 0)
            return new SignatureResolution(requirement.Id, 0, null, "zero matches");

        var resolvedAddress = requirement.AddressResolution is SignatureAddressResolution.FollowLeadingCallOrJump
            ? scanner.ResolveTextAddress(requirement.Id, requirement.Pattern)
            : matches[0];
        var first = unchecked((ulong)resolvedAddress.ToInt64());
        var rva = first >= scanner.ModuleBase
            ? first - scanner.ModuleBase
            : first;

        return new SignatureResolution(requirement.Id, matches.Count, rva, null);
    }

    public IReadOnlyList<SignatureResolution> ResolveAll(IEnumerable<SignatureRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        return requirements.Select(Resolve).ToArray();
    }
}
