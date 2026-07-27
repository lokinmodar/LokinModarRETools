using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.UI;
using ReValidation.OwnerSignatures.Commands;
using ReValidation.OwnerSignatures.Services;
using ReValidation.OwnerSignatures.Windows;

namespace ReValidation.OwnerSignatures;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem;
    private readonly ValidationWindow window;
    private readonly PluginCommandRegistrar commandRegistrar;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.pluginInterface = pluginInterface;
        windowSystem = new WindowSystem("ReValidation.OwnerSignatures");
        var controller = BuildController(pluginInterface);
        window = new ValidationWindow(controller);
        commandRegistrar = new PluginCommandRegistrar(commandManager, window);
        windowSystem.AddWindow(window);
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
    }

    public string Name => "ReValidation Owner Signatures";

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        commandRegistrar.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private static ValidationWindowController BuildController(IDalamudPluginInterface pluginInterface)
    {
        var evidenceRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "evidence");
        var runner = new ValidationScenarioRunner(
            new OwnerSignatureMetadataProvider([]),
            new JsonEvidenceWriter(new EvidencePathBuilder()),
            new MarkdownEvidenceWriter(new EvidencePathBuilder()));

        return new ValidationWindowController(
            new ValidationWindowState(),
            OwnerSignaturesScenarioComposition.CreateRegistry(),
            runner,
            new ValidationScenarioContextFactory(evidenceRoot));
    }

    private void Draw() => windowSystem.Draw();

    private void OpenWindow()
    {
        window.IsOpen = true;
    }
}
