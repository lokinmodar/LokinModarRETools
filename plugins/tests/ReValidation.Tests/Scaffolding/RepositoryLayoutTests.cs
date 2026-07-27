using Xunit;

public sealed class RepositoryLayoutTests
{
    [Fact]
    public void ReValidationSolutionContainsExpectedProjects()
    {
        var root = RepoRoot.Find();
        var pluginsRoot = Path.Combine(root, "plugins");

        Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.sln")));
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.Common", "ReValidation.Common.csproj")));
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.LocalClientStructs", "ReValidation.LocalClientStructs.csproj")));
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.OwnerSignatures", "ReValidation.OwnerSignatures.csproj")));
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "tests", "ReValidation.Tests", "ReValidation.Tests.csproj")));
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "local", "LocalClientStructs.props.example")));
    }
}
