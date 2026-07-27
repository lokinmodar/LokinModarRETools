using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.UI;
using ReValidation.OwnerSignatures.Commands;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Services;
using ReValidation.OwnerSignatures.Windows;
using Lumina.Excel.Sheets;

namespace ReValidation.OwnerSignatures;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem;
    private readonly ValidationWindow window;
    private readonly PluginCommandRegistrar commandRegistrar;
    private readonly ValidationWindowController controller;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.pluginInterface = pluginInterface;
        pluginInterface.Create<PluginServices>();
        windowSystem = new WindowSystem("ReValidation.OwnerSignatures");
        controller = BuildController(pluginInterface);
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
        controller.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private static ValidationWindowController BuildController(IDalamudPluginInterface pluginInterface)
    {
        var evidenceRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "evidence");
        var quests = PluginServices.DataManager.GetExcelSheet<Quest>().ToArray();
        var journalProbe = new CompletedJournalCapture(quests, "owner");
        var requirements = new[]
        {
            new SignatureRequirement("journalProvider", "E8 ?? ?? ?? ?? 41 88 84 2E", mustBeUnique: true),
            new SignatureRequirement("itemTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA", mustBeUnique: true),
            new SignatureRequirement("actionTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 48 8B 42 28 4C 8B FA 48 8B F1 49 8B E8", mustBeUnique: true),
        };
        var resolutions = new SignatureScannerResolver(new DalamudSignatureScanner(PluginServices.SigScanner)).ResolveAll(requirements);
        var registry = OwnerSignaturesScenarioComposition.CreateRegistry(
            new OwnerSignaturesScenarioDependencies(
                journalProbe,
                new ItemDetailTooltipProbe(GetItemDetailAddonAddress, GetItemDetailAgentAddress),
                new ActionDetailTooltipProbe(GetActionDetailAddonAddress, GetActionDetailAgentAddress),
                resolutions,
                SupportsJournalMutationProof: false,
                JournalMutationBlockingReason: "Journal override proof is not configured."));
        var runner = new ValidationScenarioRunner(
            new OwnerSignatureMetadataProvider(resolutions),
            new JsonEvidenceWriter(new EvidencePathBuilder()),
            new MarkdownEvidenceWriter(new EvidencePathBuilder()));

        return new ValidationWindowController(
            new ValidationWindowState(),
            registry,
            runner,
            new ValidationScenarioContextFactory(evidenceRoot));
    }

    private void Draw() => windowSystem.Draw();

    private void OpenWindow()
    {
        window.IsOpen = true;
    }

    private static nint GetItemDetailAddonAddress() => PluginServices.GameGui.GetAddonByName("ItemDetail", 1).Address;

    private static unsafe nint GetItemDetailAgentAddress()
    {
        var agent = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        return (nint)agent;
    }

    private static nint GetActionDetailAddonAddress() => PluginServices.GameGui.GetAddonByName("ActionDetail", 1).Address;

    private static unsafe nint GetActionDetailAgentAddress()
    {
        var agent = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ActionDetail);
        return (nint)agent;
    }
}
