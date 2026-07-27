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
    public void NonexistentProps_BlocksFullProofAsMissing()
    {
        var projectPath = Path.GetTempFileName();
        var propsPath = Path.Combine(Path.GetTempPath(), $"revalidation-{Guid.NewGuid():N}.props");

        try
        {
            var detector = new LocalClientStructsAvailabilityDetector(propsPath, projectPath);
            var availability = detector.Evaluate(ValidationMode.FullProof);

            Assert.False(availability.IsAvailable);
            Assert.Equal("plugins/local/LocalClientStructs.props is missing.", availability.BlockingReason);
        }
        finally
        {
            File.Delete(projectPath);
        }
    }

    [Fact]
    public void UnresolvedProjectPath_BlocksFullProof()
    {
        var propsPath = Path.GetTempFileName();
        var projectPath = Path.Combine(Path.GetTempPath(), $"revalidation-{Guid.NewGuid():N}.csproj");

        try
        {
            var detector = new LocalClientStructsAvailabilityDetector(propsPath, projectPath);
            var availability = detector.Evaluate(ValidationMode.FullProof);

            Assert.False(availability.IsAvailable);
            Assert.Equal("ClientStructsProjectPath could not be resolved.", availability.BlockingReason);
        }
        finally
        {
            File.Delete(propsPath);
        }
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

    [Fact]
    public void MissingMetadataProvider_RejectsUnallowlistedBlockingReason()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MissingLocalClientStructsMetadataProvider("C:\\sensitive-path"));
    }
}
