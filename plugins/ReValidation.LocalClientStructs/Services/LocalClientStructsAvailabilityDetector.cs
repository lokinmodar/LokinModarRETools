using ReValidation.Common.Models;

namespace ReValidation.LocalClientStructs.Services;

public sealed record LocalClientStructsAvailability(bool IsAvailable, string? BlockingReason, string? ProjectPath);

public sealed class LocalClientStructsAvailabilityDetector(string? propsPath, string? projectPath)
{
    private readonly string? propsPath = propsPath;
    private readonly string? projectPath = projectPath;

    public LocalClientStructsAvailability Evaluate(ValidationMode mode)
    {
        if (mode is not ValidationMode.FullProof)
            return new LocalClientStructsAvailability(true, null, projectPath);

        if (string.IsNullOrWhiteSpace(propsPath))
            return new LocalClientStructsAvailability(false, "plugins/local/LocalClientStructs.props is missing.", null);

        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return new LocalClientStructsAvailability(false, "ClientStructsProjectPath could not be resolved.", projectPath);

        return new LocalClientStructsAvailability(true, null, projectPath);
    }
}
