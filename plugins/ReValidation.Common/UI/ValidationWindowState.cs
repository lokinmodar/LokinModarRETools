using ReValidation.Common.Models;

namespace ReValidation.Common.UI;

public sealed class ValidationWindowState
{
    public string? SelectedScenarioId { get; private set; }
    public ValidationRoute SelectedRoute { get; private set; } = ValidationRoute.OwnerSignatures;
    public ValidationMode SelectedMode { get; private set; } = ValidationMode.CaptureOnly;
    public string StatusText { get; private set; } = "Idle";
    public IReadOnlyList<string> ArtifactPaths { get; private set; } = Array.Empty<string>();

    public void SelectScenario(string scenarioId) => SelectedScenarioId = scenarioId;
    public void SelectRoute(ValidationRoute route) => SelectedRoute = route;
    public void SelectMode(ValidationMode mode) => SelectedMode = mode;
    public void SetRunning() => StatusText = "Running";

    public void SetCompleted(string statusText, IReadOnlyList<string> artifactPaths)
    {
        StatusText = statusText;
        ArtifactPaths = artifactPaths;
    }
}
