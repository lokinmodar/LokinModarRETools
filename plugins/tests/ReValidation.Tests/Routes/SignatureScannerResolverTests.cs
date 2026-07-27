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
    public void Resolve_ReportsZeroMatchesWhenSignatureIsMissing()
    {
        var resolver = new SignatureScannerResolver(new FakeSignatureScanner());

        var resolution = resolver.Resolve(new SignatureRequirement("itemTooltip", "48 89 ?? ??", mustBeUnique: true));

        Assert.Equal("itemTooltip", resolution.Id);
        Assert.Equal(0, resolution.MatchCount);
        Assert.Null(resolution.Rva);
        Assert.Equal("zero matches", resolution.FailureReason);
    }

    private sealed class FakeSignatureScanner(Dictionary<string, nint[]>? matchesById = null) : ISignatureScanner
    {
        private readonly Dictionary<string, nint[]> matchesById = matchesById ?? [];

        public IReadOnlyList<nint> ScanAllText(string id, string pattern) =>
            matchesById.TryGetValue(id, out var matches)
                ? matches
                : [];

        public ulong SearchBase => 0x140000000;
    }
}
