using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.Common.UI;

namespace ReValidation.OwnerSignatures.Windows;

public sealed class ValidationWindow : Window
{
    private const int MinArmDurationSeconds = 1;
    private const int MaxArmDurationSeconds = 120;
    private readonly ValidationWindowController controller;
    private int armDurationSeconds = 10;

    public ValidationWindow(ValidationWindowController controller)
        : base("ReValidation: Owner Signatures")
    {
        ArgumentNullException.ThrowIfNull(controller);
        this.controller = controller;
        controller.State.SelectRoute(ValidationRoute.OwnerSignatures);
    }

    public override void Draw()
    {
        if (controller.State.IsArmed)
            _ = ObserveArmPulseAsync();

        ImGui.TextUnformatted("Owner-signature route");
        ImGui.TextUnformatted($"Status: {controller.State.StatusText}");

        var scenarios = controller.Scenarios.Where(scenario => scenario.Definition.SupportedRoutes.Contains(ValidationRoute.OwnerSignatures)).ToArray();
        var selectedScenario = scenarios.FirstOrDefault(scenario => scenario.Definition.Id == controller.State.SelectedScenarioId);
        var armableScenario = selectedScenario as IArmableValidationScenario;
        if (scenarios.Length == 0)
            ImGui.TextUnformatted("No scenarios are registered for this route.");

        ImGui.BeginDisabled(controller.State.IsBusy);
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
        ImGui.EndDisabled();

        if (armableScenario is not null)
        {
            ImGui.Separator();
            ImGui.TextWrapped($"{armableScenario.ArmPrompt} Recommended for hover-driven tooltip validation.");
            ImGui.BeginDisabled(controller.State.IsBusy);
            ImGui.SetNextItemWidth(120f);
            var configuredDuration = armDurationSeconds;
            if (ImGui.InputInt("Arm window (seconds)", ref configuredDuration))
                armDurationSeconds = Math.Clamp(configuredDuration, MinArmDurationSeconds, MaxArmDurationSeconds);

            if (ImGui.Button("Run selected scenario now"))
                _ = ObserveRunAsync();

            ImGui.SameLine();
            if (ImGui.Button("Arm selected scenario"))
                controller.ArmSelectedScenario(TimeSpan.FromSeconds(armDurationSeconds));
            ImGui.EndDisabled();

            ImGui.BeginDisabled(!controller.State.IsArmed);
            if (ImGui.Button("Disarm selected scenario"))
                controller.DisarmSelectedScenario();
            ImGui.EndDisabled();
        }
        else if (controller.State.SelectedScenarioId is not null)
        {
            ImGui.BeginDisabled(controller.State.IsBusy);
            if (ImGui.Button("Run selected scenario"))
                _ = ObserveRunAsync();
            ImGui.EndDisabled();
        }

        foreach (var artifactPath in controller.State.ArtifactPaths)
            ImGui.TextUnformatted(artifactPath);
    }

    private async Task ObserveRunAsync()
    {
        try
        {
            await controller.RunSelectedScenarioAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            controller.State.SetCompleted("Cancelled", Array.Empty<string>());
        }
        catch (Exception)
        {
            controller.State.SetCompleted("Failed", Array.Empty<string>());
        }
    }

    private async Task ObserveArmPulseAsync()
    {
        try
        {
            await controller.PulseArmedScenarioAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            controller.State.SetCompleted("Cancelled", Array.Empty<string>());
        }
        catch (Exception)
        {
            controller.State.SetCompleted("Failed", Array.Empty<string>());
        }
    }
}
