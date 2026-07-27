using ReValidation.Common.Models;
using ReValidation.LocalClientStructs.Services;
using Xunit;

namespace ReValidation.Tests.Routes;

public sealed class LocalClientStructsAvailabilityTests
{
    [Fact]
    public void MissingProps_BlocksFullProof()
    {
        var detector = new LocalClientStructsAvailabilityDetector(propsPath: null, projectPath: null);
        var availability = detector.Evaluate(ValidationMode.FullProof);

        Assert.False(availability.IsAvailable);
        Assert.Equal("plugins/local/LocalClientStructs.props is missing.", availability.BlockingReason);
    }

    [Fact]
    public async Task RealMetadataProvider_ReportsBranchCommitDirtyStateAndAssemblyHash()
    {
        var provider = new RealLocalClientStructsMetadataProvider(
            branch: "feature/journal-provider",
            commit: "abc1234",
            isDirty: true,
            assemblySha256: "deadbeef");

        var metadata = await provider.GetMetadataAsync(CancellationToken.None);

        Assert.Equal("feature/journal-provider", metadata["clientStructsBranch"]);
        Assert.Equal("abc1234", metadata["clientStructsCommit"]);
        Assert.Equal("true", metadata["clientStructsDirty"]);
        Assert.Equal("deadbeef", metadata["clientStructsAssemblySha256"]);
    }
}
