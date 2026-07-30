namespace ReValidation.OwnerSignatures.Services;

public interface ISignatureScanner
{
    IReadOnlyList<nint> ScanAllText(string id, string pattern);
    nint ResolveTextAddress(string id, string pattern);
    ulong ModuleBase { get; }
    ulong SearchBase { get; }
}
