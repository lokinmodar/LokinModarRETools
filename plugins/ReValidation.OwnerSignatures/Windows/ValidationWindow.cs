using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ReValidation.Common.Models;
using ReValidation.Common.UI;

namespace ReValidation.OwnerSignatures.Windows;

public sealed class ValidationWindow : Window
{
    private readonly ValidationWindowController controller;

    public ValidationWindow(ValidationWindowController controller)
        : base("ReValidation: Owner Signatures")
    {
        ArgumentNullException.ThrowIfNull(controller);
        this.controller = controller;
        controller.State.SelectRoute(ValidationRoute.OwnerSignatures);
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Owner-signature route");
        ImGui.TextUnformatted($"Status: {controller.State.StatusText}");

        var scenarios = controller.Scenarios.Where(scenario => scenario.Definition.SupportedRoutes.Contains(ValidationRoute.OwnerSignatures)).ToArray();
        if (scenarios.Length == 0)
            ImGui.TextUnformatted("No scenarios are registered for this route.");

        foreach (var scenario in scenarios)
        {
            if (ImGui.Selectable(scenario.Definition.Name, controller.State.SelectedScenarioId == scenario.Definition.Id))
                controller.State.SelectScenario(scenario.Definition.Id);
        }

        foreach (var mode in Enum.GetValues<ValidationMode>())
        {
            if (ImGui.RadioButton(mode.ToString(), controller.State.SelectedMode == mode))
                controller.State.SelectMode(mode);
        }

        if (controller.State.SelectedScenarioId is not null && ImGui.Button("Run selected scenario"))
            _ = controller.RunSelectedScenarioAsync(CancellationToken.None);

        foreach (var artifactPath in controller.State.ArtifactPaths)
            ImGui.TextUnformatted(artifactPath);
    }
}
