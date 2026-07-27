using ReValidation.Common.Models;

namespace ReValidation.Common.UI;

public sealed class ValidationWindowState
{
    public string? SelectedScenarioId { get; private set; }
    public ValidationRoute SelectedRoute { get; private set; } = ValidationRoute.OwnerSignatures;
    public ValidationMode SelectedMode { get; private set; } = ValidationMode.CaptureOnly;
    public bool IsArmed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsBusy => IsArmed || IsRunning;
    public string StatusText { get; private set; } = "Idle";
    public string StatusDetailText { get; private set; } = string.Empty;
    public IReadOnlyList<string> ArtifactPaths { get; private set; } = Array.Empty<string>();

    public void SelectScenario(string scenarioId) => SelectedScenarioId = scenarioId;
    public void SelectRoute(ValidationRoute route) => SelectedRoute = route;
    public void SelectMode(ValidationMode mode) => SelectedMode = mode;

    public void SetArmed(string statusText, string statusDetailText)
    {
        IsArmed = true;
        IsRunning = false;
        StatusText = statusText;
        StatusDetailText = statusDetailText;
        ArtifactPaths = Array.Empty<string>();
    }

    public void UpdateArmedStatus(string statusText)
    {
        if (!IsArmed)
            return;

        StatusText = statusText;
    }

    public void SetRunning()
    {
        IsArmed = false;
        IsRunning = true;
        StatusText = "Running";
        StatusDetailText = string.Empty;
        ArtifactPaths = Array.Empty<string>();
    }

    public void SetCompleted(string statusText, string? statusDetailText, IReadOnlyList<string> artifactPaths)
    {
        IsArmed = false;
        IsRunning = false;
        StatusText = statusText;
        StatusDetailText = statusDetailText ?? string.Empty;
        ArtifactPaths = artifactPaths;
    }

    public void Reset()
    {
        IsArmed = false;
        IsRunning = false;
        StatusText = "Idle";
        StatusDetailText = string.Empty;
        ArtifactPaths = Array.Empty<string>();
    }
}
