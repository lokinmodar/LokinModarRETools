namespace ReValidation.OwnerSignatures.Services;

public interface ISignatureScanner
{
    IReadOnlyList<nint> ScanAllText(string id, string pattern);
    ulong SearchBase { get; }
}
