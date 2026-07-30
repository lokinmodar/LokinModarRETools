using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Routes;

public sealed class SignatureScannerResolverTests
{
    [Fact]
    public void Resolve_ReportsMatchCountAndFirstRva()
    {
        var resolver = new SignatureScannerResolver(new FakeSignatureScanner(new Dictionary<string, nint[]>
        {
            ["journalProvider"] = [unchecked((nint)0x140001234UL), unchecked((nint)0x140001ABCUL)],
        }));

        var resolution = resolver.Resolve(new SignatureRequirement("journalProvider", "48 89 ?? ??", mustBeUnique: true));

        Assert.Equal("journalProvider", resolution.Id);
        Assert.Equal(2, resolution.MatchCount);
        Assert.Equal(0x1234UL, resolution.Rva);
        Assert.Null(resolution.FailureReason);
    }

    [Fact]
    public void Resolve_NormalizesModuleAddressAgainstModuleBase_WhenScannerUsesCopiedSearchBuffer()
    {
        var resolver = new SignatureScannerResolver(new FakeSignatureScanner(
            new Dictionary<string, nint[]>
            {
                ["itemTooltip"] = [unchecked((nint)0x140001234UL)],
            },
            moduleBase: 0x140000000,
            searchBase: 0x7FFF00000000));

        var resolution = resolver.Resolve(new SignatureRequirement("itemTooltip", "48 89 ?? ??", mustBeUnique: true));

        Assert.Equal(0x1234UL, resolution.Rva);
    }

    [Fact]
    public void Resolve_FollowsLeadingCallTarget_WhenRequirementDeclaresRel32Resolution()
    {
        var scanner = new FakeSignatureScanner(
            new Dictionary<string, nint[]>
            {
                ["journalProvider"] = [unchecked((nint)0x140001000UL)],
            },
            resolvedTextAddresses: new Dictionary<string, nint>
            {
                ["journalProvider"] = unchecked((nint)0x140005678UL),
            });
        var resolver = new SignatureScannerResolver(scanner);

        var resolution = resolver.Resolve(new SignatureRequirement(
            "journalProvider",
            "E8 ?? ?? ?? ?? 41 88 84 2E",
            mustBeUnique: true,
            SignatureAddressResolution.FollowLeadingCallOrJump));

        Assert.Equal(0x5678UL, resolution.Rva);
        Assert.Equal(1, scanner.ResolveTextAddressCount);
    }

    [Fact]
    public void Resolve_ReportsZeroMatchesWhenSignatureIsMissing()
    {
        var resolver = new SignatureScannerResolver(new FakeSignatureScanner());

        var resolution = resolver.Resolve(new SignatureRequirement("itemTooltip", "48 89 ?? ??", mustBeUnique: true));

        Assert.Equal("itemTooltip", resolution.Id);
        Assert.Equal(0, resolution.MatchCount);
        Assert.Null(resolution.Rva);
        Assert.Equal("zero matches", resolution.FailureReason);
    }

    private sealed class FakeSignatureScanner(
        Dictionary<string, nint[]>? matchesById = null,
        ulong moduleBase = 0x140000000,
        ulong searchBase = 0x140000000,
        Dictionary<string, nint>? resolvedTextAddresses = null) : ISignatureScanner
    {
        private readonly Dictionary<string, nint[]> matchesById = matchesById ?? [];
        private readonly Dictionary<string, nint> resolvedTextAddresses = resolvedTextAddresses ?? [];

        public IReadOnlyList<nint> ScanAllText(string id, string pattern) =>
            matchesById.TryGetValue(id, out var matches)
                ? matches
                : [];

        public nint ResolveTextAddress(string id, string pattern)
        {
            ResolveTextAddressCount++;
            return resolvedTextAddresses[id];
        }

        public int ResolveTextAddressCount { get; private set; }

        public ulong ModuleBase { get; } = moduleBase;

        public ulong SearchBase { get; } = searchBase;
    }
}
