using Dalamud.Plugin.Services;

namespace ReValidation.OwnerSignatures.Services;

public sealed class DalamudSignatureScanner(ISigScanner scanner) : ISignatureScanner
{
    private readonly ISigScanner scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

    public ulong ModuleBase => unchecked((ulong)scanner.Module.BaseAddress.ToInt64());

    public ulong SearchBase => unchecked((ulong)scanner.SearchBase.ToInt64());

    public IReadOnlyList<nint> ScanAllText(string id, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return scanner.ScanAllText(pattern).ToArray();
    }

    public nint ResolveTextAddress(string id, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return scanner.ScanText(pattern);
    }
}
