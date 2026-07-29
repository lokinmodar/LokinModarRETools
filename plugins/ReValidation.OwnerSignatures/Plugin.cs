using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ReValidation.Common.Evidence;
using ReValidation.Common.Discovery;
using ReValidation.Common.Execution;
using ReValidation.Common.Proof;
using ReValidation.Common.UI;
using ReValidation.OwnerSignatures.Commands;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
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
    private readonly IBranchValidationRouteAdapter branchRouteAdapter;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(commandManager);

        this.pluginInterface = pluginInterface;
        pluginInterface.Create<PluginServices>();
        windowSystem = new WindowSystem("ReValidation.OwnerSignatures");
        controller = BuildController(pluginInterface, out branchRouteAdapter);
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

    private static ValidationWindowController BuildController(IDalamudPluginInterface pluginInterface, out IBranchValidationRouteAdapter branchRouteAdapter)
    {
        var evidenceRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "evidence");
        var diagnosticsSink = new PluginLogDiagnosticsSink(PluginServices.PluginLog);
        var quests = PluginServices.DataManager.GetExcelSheet<Quest>().ToArray();
        var journalProbe = new CompletedJournalCapture(quests, "owner");
        var requirements = new[]
        {
            new SignatureRequirement("journalProvider", "E8 ?? ?? ?? ?? 41 88 84 2E", mustBeUnique: true),
            new SignatureRequirement("itemTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA", mustBeUnique: true),
            new SignatureRequirement("actionTooltip", "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 48 8B 42 28 4C 8B FA 48 8B F1 49 8B E8", mustBeUnique: true),
        };
        var signatureResolver = new SignatureScannerResolver(new DalamudSignatureScanner(PluginServices.SigScanner));
        var resolutions = signatureResolver.ResolveAll(requirements);
        var itemTooltipProbe = new ItemDetailTooltipProbe(GetItemDetailAddonAddress, GetItemDetailAgentAddress);
        var actionTooltipProbe = new ActionDetailTooltipProbe(GetActionDetailAddonAddress, GetActionDetailAgentAddress);
        var resolutionProvider = new ResolvedSignatureProvider(resolutions);
        var hookTargets = new OwnerHookTargetRegistry(
        [
            new OwnerHookTargetDefinition(
                JournalHookTargetIds.JournalProvider,
                "journalProvider",
                "Open the Journal list.",
                new JournalProviderHookContextCapture(),
                new JournalProviderMutationStrategy()),
        ]);
        var hookProofExecutor = new OwnerHookProofExecutor(
            hookTargets,
            new DalamudOwnerHookInstaller(PluginServices.GameInteropProvider, (ulong)PluginServices.SigScanner.SearchBase),
            resolutionProvider);
        var hookFactory = new DalamudTooltipProofHookFactory(PluginServices.GameInteropProvider, resolutionProvider, (ulong)PluginServices.SigScanner.SearchBase);
        branchRouteAdapter = new OwnerBranchValidationRouteAdapter(
            resolutionProvider,
            new OwnerTooltipProofExecutor(itemTooltipProbe, hookFactory),
            new OwnerTooltipProofExecutor(actionTooltipProbe, hookFactory));
        var registry = OwnerSignaturesScenarioComposition.CreateRegistry(
            new OwnerSignaturesScenarioDependencies(
                journalProbe,
                itemTooltipProbe,
                actionTooltipProbe,
                resolutions,
                SupportsJournalMutationProof: false,
                JournalMutationBlockingReason: "Journal override proof is not configured.",
                HookProofExecutor: hookProofExecutor,
                HookTargets: hookTargets));
        var runner = new ValidationScenarioRunner(
            new OwnerSignatureMetadataProvider(resolutions),
            diagnosticsSink,
            new JsonEvidenceWriter(new EvidencePathBuilder()),
            new MarkdownEvidenceWriter(new EvidencePathBuilder()));

        return new ValidationWindowController(
            new ValidationWindowState(),
            registry,
            runner,
            new ValidationScenarioContextFactory(evidenceRoot),
            diagnosticsSink,
            branchWorkflow: CreateBranchValidationWorkflow(evidenceRoot, branchRouteAdapter, GetClientStructsProjectPath()));
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

    private static string? GetClientStructsProjectPath() =>
        typeof(Plugin).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "ClientStructsProjectPath", StringComparison.Ordinal))
            ?.Value;

    private static string? ResolveCheckoutRoot(string projectPath)
    {
        var projectFile = new FileInfo(projectPath);
        return projectFile.Directory?.Parent?.FullName;
    }

    private sealed class ResolvedSignatureProvider(IReadOnlyList<SignatureResolution> resolutions) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) =>
            resolutions.FirstOrDefault(resolution => string.Equals(resolution.Id, signatureId, StringComparison.Ordinal))
            ?? new SignatureResolution(signatureId, 0, null, "resolution missing");
    }
}
