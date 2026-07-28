using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ReValidation.DynamisBridge.Services;

namespace ReValidation.DynamisBridge.Windows;

public sealed class JournalExplorerWindow : Window
{
    private readonly IDynamisAvailabilityService availabilityService;

    public JournalExplorerWindow(IDynamisAvailabilityService availabilityService) : base("ReValidation: Dynamis Bridge")
    {
        this.availabilityService = availabilityService;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(availabilityService.Current.StatusText);
    }
}
