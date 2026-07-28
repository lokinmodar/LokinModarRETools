using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ReValidation.DynamisBridge.Commands;
using ReValidation.DynamisBridge.Ipc;
using ReValidation.DynamisBridge.Services;
using ReValidation.DynamisBridge.UI;
using ReValidation.DynamisBridge.Windows;

namespace ReValidation.DynamisBridge;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem;
    private readonly JournalExplorerWindow window;
    private readonly PluginCommandRegistrar commandRegistrar;
    private readonly DynamisApiClient dynamisApiClient;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        this.pluginInterface = pluginInterface;
        pluginInterface.Create<PluginServices>();
        var gateway = new DalamudDynamisIpcGateway(pluginInterface);
        dynamisApiClient = new DynamisApiClient(gateway, minimumApiVersion: 4);
        dynamisApiClient.Refresh();
        var availabilityService = new DynamisAvailabilityService(dynamisApiClient);
        var exportRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "dynamis-bridge");
        var anchorCollector = new LiveJournalAnchorCollector(
            () => PluginServices.GameGui.GetAddonByName("Journal", 1).Address,
            () => Array.Empty<nint>());
        var inspectionService = new PointerInspectionService(dynamisApiClient);
        var controller = new JournalExplorerController(
            new JournalExplorerWindowState(),
            availabilityService,
            anchorCollector,
            inspectionService,
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => Environment.ProcessPath);
        windowSystem = new WindowSystem("ReValidation.DynamisBridge");
        window = new JournalExplorerWindow(controller, exportRoot);
        commandRegistrar = new PluginCommandRegistrar(commandManager, window);
        windowSystem.AddWindow(window);
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
    }

    public string Name => "ReValidation Dynamis Bridge";

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        commandRegistrar.Dispose();
        windowSystem.RemoveAllWindows();
        dynamisApiClient.Dispose();
    }

    private void Draw() => windowSystem.Draw();

    private void OpenWindow() => window.IsOpen = true;
}
