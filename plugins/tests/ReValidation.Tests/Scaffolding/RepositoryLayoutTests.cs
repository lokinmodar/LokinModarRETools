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

    [Fact]
    public void LocalClientStructsProjectWiresOptionalClientStructsReference()
    {
        var root = RepoRoot.Find();
        var project = File.ReadAllText(Path.Combine(root, "plugins", "ReValidation.LocalClientStructs", "ReValidation.LocalClientStructs.csproj"));
        var propsExample = File.ReadAllText(Path.Combine(root, "plugins", "local", "LocalClientStructs.props.example"));

        Assert.Contains("LocalClientStructs.props", project);
        Assert.Contains("ClientStructsProjectPath", project);
        Assert.Contains("ProjectReference", project);
        Assert.Contains("Exists", project);
        Assert.Contains("ClientStructsProjectPath", propsExample);
    }
}
