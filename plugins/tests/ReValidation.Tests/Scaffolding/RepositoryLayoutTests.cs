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

    [Fact]
    public void LocalClientStructsRequiredRouteValidatesPropsAndProjectPath()
    {
        var root = RepoRoot.Find();
        var sourceProject = Path.Combine(root, "plugins", "ReValidation.LocalClientStructs", "ReValidation.LocalClientStructs.csproj");

        using var temp = new TemporaryDirectory();
        var pluginProjectDirectory = Path.Combine(temp.Path, "plugins", "ReValidation.LocalClientStructs");
        var localDirectory = Path.Combine(temp.Path, "plugins", "local");
        Directory.CreateDirectory(pluginProjectDirectory);
        var projectPath = Path.Combine(pluginProjectDirectory, "ReValidation.LocalClientStructs.csproj");
        File.Copy(sourceProject, projectPath);

        var missingProps = RunValidation(temp.Path, projectPath);
        Assert.NotEqual(0, missingProps.ExitCode);

        Directory.CreateDirectory(localDirectory);
        File.WriteAllText(Path.Combine(localDirectory, "LocalClientStructs.props"),
            "<Project><PropertyGroup><ClientStructsProjectPath></ClientStructsProjectPath></PropertyGroup></Project>");
        var emptyPath = RunValidation(temp.Path, projectPath);
        Assert.NotEqual(0, emptyPath.ExitCode);

        File.WriteAllText(Path.Combine(localDirectory, "LocalClientStructs.props"),
            "<Project><PropertyGroup><ClientStructsProjectPath>missing.csproj</ClientStructsProjectPath></PropertyGroup></Project>");
        var invalidPath = RunValidation(temp.Path, projectPath);
        Assert.NotEqual(0, invalidPath.ExitCode);

        var validProjectDirectory = Path.Combine(temp.Path, "ClientStructs");
        Directory.CreateDirectory(validProjectDirectory);
        var validProject = Path.Combine(validProjectDirectory, "ClientStructs.csproj");
        File.WriteAllText(validProject, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(localDirectory, "LocalClientStructs.props"),
            $"<Project><PropertyGroup><ClientStructsProjectPath>{validProject}</ClientStructsProjectPath></PropertyGroup></Project>");
        var validPath = RunValidation(temp.Path, projectPath);
        Assert.Equal(0, validPath.ExitCode);
    }

    private static (int ExitCode, string Output) RunValidation(string workingDirectory, string projectPath)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" --nologo /p:RequireLocalClientStructs=true --verbosity:minimal",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"revalidation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
