using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using System.Runtime.CompilerServices;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed unsafe class ActionDetailTooltipProbe : ITooltipProbe
{
    private readonly Func<nint> addonAddressAccessor;
    private readonly Func<nint> agentAddressAccessor;
    private AtkTextNode* overriddenNode;
    private string? originalVisibleText;

    public ActionDetailTooltipProbe(Func<nint> addonAddressAccessor, Func<nint> agentAddressAccessor)
    {
        this.addonAddressAccessor = addonAddressAccessor ?? throw new ArgumentNullException(nameof(addonAddressAccessor));
        this.agentAddressAccessor = agentAddressAccessor ?? throw new ArgumentNullException(nameof(agentAddressAccessor));
    }

    public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        var addon = GetAddon();
        var agent = GetAgent();
        var payloadLines = CollectPayloadLines(addon);
        var visibleText = payloadLines.Count == 0 ? string.Empty : string.Join('\n', payloadLines);
        var resolvedId = agent->OriginalId != 0 ? agent->OriginalId : agent->ActionId;
        return ValueTask.FromResult(new TooltipSnapshot("action", resolvedId, payloadLines, visibleText));
    }

    public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken)
    {
        var addon = GetAddon();
        overriddenNode = FindFirstVisibleTextNode(addon);
        if (overriddenNode is null)
            throw new InvalidOperationException("ActionDetail text node is unavailable.");

        originalVisibleText = overriddenNode->NodeText.ToString();
        overriddenNode->SetText(sentinel);
        return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket($"Applied {sentinel}", new System.Text.Json.Nodes.JsonObject()));
    }

    public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken)
    {
        var current = overriddenNode is null ? string.Empty : overriddenNode->NodeText.ToString();
        return ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(
            string.Equals(current, sentinel, StringComparison.Ordinal),
            "Tooltip sentinel asserted",
            []));
    }

    public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
    {
        if (overriddenNode is not null && originalVisibleText is not null)
            overriddenNode->SetText(originalVisibleText);

        return ValueTask.FromResult(new ScenarioRestoreResult(true, "Tooltip text restored", []));
    }

    private static unsafe IReadOnlyList<string> CollectPayloadLines(AtkUnitBase* addon)
    {
        var lines = new List<string>();
        if (addon->UldManager.NodeList is null)
            return lines;

        for (var index = 0; index < addon->UldManager.NodeListCount; index++)
        {
            var node = addon->UldManager.NodeList[index];
            if (node is null || node->Type != NodeType.Text)
                continue;

            var textNode = (AtkTextNode*)node;
            var text = textNode->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                lines.Add(text);
        }

        return lines;
    }

    private static unsafe AtkTextNode* FindFirstVisibleTextNode(AtkUnitBase* addon)
    {
        if (addon->UldManager.NodeList is null)
            return null;

        for (var index = 0; index < addon->UldManager.NodeListCount; index++)
        {
            var node = addon->UldManager.NodeList[index];
            if (node is null || node->Type != NodeType.Text)
                continue;

            var textNode = (AtkTextNode*)node;
            if (!string.IsNullOrWhiteSpace(textNode->NodeText.ToString()))
                return textNode;
        }

        return null;
    }

    private AtkUnitBase* GetAddon()
    {
        return TooltipAddonGuard.RequireVisibleAndReady(addonAddressAccessor(), "ActionDetail");
    }

    private AgentActionDetail* GetAgent()
    {
        var address = agentAddressAccessor();
        return address == nint.Zero
            ? throw new InvalidOperationException("ActionDetail agent is unavailable.")
            : (AgentActionDetail*)address;
    }
}
