using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ReValidation.DynamisBridge.Windows;

public sealed class JournalExplorerWindow : Window
{
    public JournalExplorerWindow() : base("ReValidation: Dynamis Bridge")
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Dynamis bridge scaffold loaded.");
    }
}
