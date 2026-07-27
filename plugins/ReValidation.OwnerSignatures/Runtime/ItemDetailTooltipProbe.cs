using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed unsafe class ItemDetailTooltipProbe : ITooltipProbe
{
    private readonly Func<nint> addonAddressAccessor;
    private readonly Func<nint> agentAddressAccessor;
    private string? originalVisibleText;

    public ItemDetailTooltipProbe(Func<nint> addonAddressAccessor, Func<nint> agentAddressAccessor)
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
        var resolvedId = agent->ItemId != 0 ? agent->ItemId : agent->TypeOrId;
        return ValueTask.FromResult(new TooltipSnapshot("item", resolvedId, payloadLines, visibleText));
    }

    public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken)
    {
        var addon = GetAddon();
        originalVisibleText = addon->ItemNameText is null ? string.Empty : addon->ItemNameText->NodeText.ToString();
        if (addon->ItemNameText is null)
            throw new InvalidOperationException("ItemDetail item-name node is unavailable.");

        addon->ItemName.SetString(sentinel);
        addon->ItemNameText->SetText(sentinel);
        return ValueTask.FromResult<ScenarioOverrideTicket?>(new ScenarioOverrideTicket($"Applied {sentinel}", new System.Text.Json.Nodes.JsonObject()));
    }

    public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken)
    {
        var addon = GetAddon();
        var current = addon->ItemNameText is null ? string.Empty : addon->ItemNameText->NodeText.ToString();
        return ValueTask.FromResult<ScenarioAssertResult?>(new ScenarioAssertResult(
            string.Equals(current, sentinel, StringComparison.Ordinal),
            "Tooltip sentinel asserted",
            []));
    }

    public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken)
    {
        var addon = GetAddon();
        if (addon->ItemNameText is not null && originalVisibleText is not null)
        {
            addon->ItemName.SetString(originalVisibleText);
            addon->ItemNameText->SetText(originalVisibleText);
        }

        return ValueTask.FromResult(new ScenarioRestoreResult(true, "Tooltip text restored", []));
    }

    private static IReadOnlyList<string> CollectPayloadLines(AddonItemDetail* addon)
    {
        var lines = new List<string>();
        Append(lines, addon->ItemNameText);
        Append(lines, addon->CategoryText);
        Append(lines, addon->DescriptionText);
        Append(lines, addon->EffectsDescription);
        Append(lines, addon->LevelText);
        return lines;
    }

    private AddonItemDetail* GetAddon()
    {
        var address = addonAddressAccessor();
        return address == nint.Zero
            ? throw new InvalidOperationException("ItemDetail addon is not visible.")
            : (AddonItemDetail*)address;
    }

    private AgentItemDetail* GetAgent()
    {
        var address = agentAddressAccessor();
        return address == nint.Zero
            ? throw new InvalidOperationException("ItemDetail agent is unavailable.")
            : (AgentItemDetail*)address;
    }

    private static unsafe void Append(List<string> lines, FFXIVClientStructs.FFXIV.Component.GUI.AtkTextNode* node)
    {
        if (node is null)
            return;

        var text = node->NodeText.ToString();
        if (!string.IsNullOrWhiteSpace(text))
            lines.Add(text);
    }
}
