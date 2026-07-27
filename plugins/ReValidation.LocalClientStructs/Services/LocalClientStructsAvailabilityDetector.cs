using ReValidation.Common.Models;

namespace ReValidation.LocalClientStructs.Services;

public sealed record LocalClientStructsAvailability(bool IsAvailable, string? BlockingReason, string? ProjectPath);

public sealed class LocalClientStructsAvailabilityDetector
{
    private readonly bool hasLocalConfiguration;
    private readonly string? projectPath;

    public LocalClientStructsAvailabilityDetector(bool hasLocalConfiguration, string? projectPath)
    {
        this.hasLocalConfiguration = hasLocalConfiguration;
        this.projectPath = projectPath;
    }

    public LocalClientStructsAvailabilityDetector(string? propsPath, string? projectPath)
        : this(!string.IsNullOrWhiteSpace(propsPath) && File.Exists(propsPath), projectPath)
    {
    }

    public LocalClientStructsAvailability Evaluate(ValidationMode mode)
    {
        if (mode is not ValidationMode.FullProof)
            return new LocalClientStructsAvailability(true, null, projectPath);

        if (!hasLocalConfiguration)
            return new LocalClientStructsAvailability(false, "plugins/local/LocalClientStructs.props is missing.", null);

        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return new LocalClientStructsAvailability(false, "ClientStructsProjectPath could not be resolved.", projectPath);

        return new LocalClientStructsAvailability(true, null, projectPath);
    }
}
