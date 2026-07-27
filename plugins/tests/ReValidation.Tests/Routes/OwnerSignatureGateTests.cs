using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Routes;

public sealed class OwnerSignatureGateTests
{
    [Fact]
    public void ZeroMatchRequirement_BlocksScenario()
    {
        var gate = new SignatureGate();
        var requirement = new SignatureRequirement("journalProvider", "48 89 ?? ??", mustBeUnique: true);
        var resolution = new SignatureResolution("journalProvider", matchCount: 0, rva: null, failureReason: "zero matches");

        var result = gate.Evaluate([requirement], [resolution]);

        Assert.False(result.CanRun);
        Assert.Equal("journalProvider", result.FailingRequirementId);
    }

    [Fact]
    public void MultiMatchUniqueRequirement_BlocksScenario()
    {
        var gate = new SignatureGate();
        var requirement = new SignatureRequirement("tooltipBuilder", "E8 ?? ?? ?? ?? 48 8B", mustBeUnique: true);
        var resolution = new SignatureResolution("tooltipBuilder", matchCount: 3, rva: null, failureReason: "multiple matches");

        var result = gate.Evaluate([requirement], [resolution]);

        Assert.False(result.CanRun);
        Assert.Equal("tooltipBuilder", result.FailingRequirementId);
    }

    [Fact]
    public async Task MetadataProvider_ExportsOnlyAllowlistedResolutionMetadata()
    {
        var provider = new OwnerSignatureMetadataProvider(
        [
            new SignatureResolution("journalProvider", matchCount: 1, rva: 0x1234, failureReason: "private detail"),
            new SignatureResolution("tooltipBuilder", matchCount: 3, rva: null, failureReason: "multiple matches"),
        ]);

        var metadata = await provider.GetMetadataAsync(CancellationToken.None);

        Assert.Equal("matchCount=1;rva=0x1234", metadata["signature:journalProvider"]);
        Assert.Equal("3 matches", metadata["signature:tooltipBuilder"]);
        Assert.DoesNotContain(metadata.Values, value => value!.Contains("private detail", StringComparison.Ordinal));
    }
}
