using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ReValidation.Common.Evidence;
using ReValidation.Common.Discovery;
using ReValidation.Common.Execution;
using ReValidation.Common.Proof;
using ReValidation.Common.UI;
using ReValidation.LocalClientStructs.Commands;
using ReValidation.LocalClientStructs.Runtime;
using ReValidation.LocalClientStructs.Services;
using ReValidation.LocalClientStructs.Windows;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using InteropGenerator.Runtime;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lumina.Excel.Sheets;

namespace ReValidation.LocalClientStructs;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem;
    private readonly ValidationWindow window;
    private readonly PluginCommandRegistrar commandRegistrar;
    private readonly ValidationWindowController controller;
    private readonly IBranchValidationRouteAdapter branchRouteAdapter;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.pluginInterface = pluginInterface;
        pluginInterface.Create<PluginServices>();
        windowSystem = new WindowSystem("ReValidation.LocalClientStructs");
        controller = BuildController(pluginInterface, out branchRouteAdapter);
        window = new ValidationWindow(controller);
        commandRegistrar = new PluginCommandRegistrar(commandManager, window);
        windowSystem.AddWindow(window);
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
    }

    public string Name => "ReValidation Local ClientStructs";

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        commandRegistrar.Dispose();
        controller.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private static ValidationWindowController BuildController(IDalamudPluginInterface pluginInterface, out IBranchValidationRouteAdapter branchRouteAdapter)
    {
        var evidenceRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "evidence");
        var diagnosticsSink = new PluginLogDiagnosticsSink(PluginServices.PluginLog);
        var clientStructsAssembly = typeof(Framework).Assembly;
        var buildMetadata = LocalClientStructsBuildMetadataLoader.Load(typeof(Plugin).Assembly, clientStructsAssembly);
        InitializeLocalClientStructsRuntime(buildMetadata);
        var availabilityDetector = new LocalClientStructsAvailabilityDetector(buildMetadata.HasLocalConfiguration, buildMetadata.ProjectPath);
        var quests = PluginServices.DataManager.GetExcelSheet<Quest>().ToArray();
        var journalProbe = new CompletedJournalCapture(quests, new JournalSheetSnapshotBuilder(), "local");
        var itemTooltipProbe = new ItemDetailTooltipProbe(GetItemDetailAddonAddress, GetItemDetailAgentAddress);
        var actionTooltipProbe = new ActionDetailTooltipProbe(GetActionDetailAddonAddress, GetActionDetailAgentAddress);
        branchRouteAdapter = new LocalBranchValidationRouteAdapter(
            new LocalTooltipProofExecutor(itemTooltipProbe, new DalamudTooltipProofHookFactory(PluginServices.GameInteropProvider)),
            new LocalTooltipProofExecutor(actionTooltipProbe, new DalamudTooltipProofHookFactory(PluginServices.GameInteropProvider)));
        var registry = LocalClientStructsScenarioComposition.CreateRegistry(
            new LocalClientStructsScenarioDependencies(
                journalProbe,
                itemTooltipProbe,
                actionTooltipProbe,
                availabilityDetector,
                SupportsJournalMutationProof: false,
                JournalMutationBlockingReason: "Journal override proof is not configured."));
        var runner = new ValidationScenarioRunner(
            LocalClientStructsBuildMetadataLoader.CreateMetadataProvider(buildMetadata),
            diagnosticsSink,
            new JsonEvidenceWriter(new EvidencePathBuilder()),
            new MarkdownEvidenceWriter(new EvidencePathBuilder()));

        return new ValidationWindowController(
            new ValidationWindowState(),
            registry,
            runner,
            new ValidationScenarioContextFactory(evidenceRoot),
            diagnosticsSink,
            branchWorkflow: CreateBranchValidationWorkflow(evidenceRoot, branchRouteAdapter, buildMetadata.ProjectPath));
    }

    private static void InitializeLocalClientStructsRuntime(LocalClientStructsBuildMetadata buildMetadata)
    {
        if (!buildMetadata.HasLocalConfiguration)
            return;

        Resolver.GetInstance.Setup(PluginServices.SigScanner.SearchBase);
        FFXIVClientStructs.Interop.Generated.Addresses.Register();
        Resolver.GetInstance.Resolve();
        PluginServices.PluginLog.Information("Initialized local ClientStructs resolver.");
    }

    private static BranchValidationWorkflow? CreateBranchValidationWorkflow(
        string evidenceRoot,
        IBranchValidationRouteAdapter routeAdapter,
        string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
            return null;

        var checkoutRoot = ResolveCheckoutRoot(projectPath);
        return checkoutRoot is null
            ? null
            : new BranchValidationWorkflow(
                new ClientStructsGitDiffDiscoveryService(new GitProcessDiffReader(), new InteropBindingSourceParser()),
                new ProofPlanBuilder(),
                new BranchValidationRunner(new BranchJsonEvidenceWriter(new EvidencePathBuilder()), new BranchMarkdownEvidenceWriter(new EvidencePathBuilder())),
                routeAdapter,
                new ClientStructsDiscoveryOptions(checkoutRoot, DiscoveryMode.Diff, [TargetFamily.ItemTooltip, TargetFamily.ActionTooltip]),
                evidenceRoot);
    }

    private static string? ResolveCheckoutRoot(string projectPath)
    {
        var projectFile = new FileInfo(projectPath);
        return projectFile.Directory?.Parent?.FullName;
    }

    private void Draw() => windowSystem.Draw();

    private void OpenWindow()
    {
        window.IsOpen = true;
    }

    private static unsafe nint GetItemDetailAddonAddress()
    {
        return PluginServices.GameGui.GetAddonByName("ItemDetail", 1).Address;
    }

    private static unsafe nint GetItemDetailAgentAddress()
    {
        var agent = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
        return (nint)agent;
    }

    private static unsafe nint GetActionDetailAddonAddress()
    {
        return PluginServices.GameGui.GetAddonByName("ActionDetail", 1).Address;
    }

    private static unsafe nint GetActionDetailAgentAddress()
    {
        var agent = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.ActionDetail);
        return (nint)agent;
    }
}
