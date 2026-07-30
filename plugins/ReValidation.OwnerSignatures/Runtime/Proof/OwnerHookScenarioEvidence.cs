using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed record OwnerHookScenarioEvidence(
    string TargetId,
    IReadOnlyList<OwnerHookProofRecord> Stages)
{
    public static OwnerHookScenarioEvidence? FromCapture(JsonObject? captureData)
    {
        if (captureData?["ownerHook"] is not JsonObject ownerHook
            || !TryGetSafeId(ownerHook["targetId"], out var targetId)
            || ownerHook["stages"] is not JsonArray stages)
            return null;

        var records = new List<OwnerHookProofRecord>();
        foreach (var stageNode in stages)
        {
            if (stageNode is not JsonObject stage
                || !TryGetString(stage["stage"], out var stageName)
                || !Enum.TryParse<OwnerHookProofStage>(stageName, out var proofStage)
                || !Enum.IsDefined(proofStage)
                || !TryGetString(stage["status"], out var statusName)
                || !Enum.TryParse<OwnerHookProofStatus>(statusName, out var proofStatus)
                || !Enum.IsDefined(proofStatus)
                || !TryGetSafeSummary(stage["summary"], out var summary))
                return null;

            records.Add(new OwnerHookProofRecord(
                proofStage,
                proofStatus,
                summary,
                FilterData(proofStage, stage["data"] as JsonObject)));
        }

        return records.Count == 0 ? null : new OwnerHookScenarioEvidence(targetId, records);
    }

    public JsonObject ToJson() => new()
    {
        ["targetId"] = TargetId,
        ["stages"] = new JsonArray(Stages.Select(stage => stage.ToJson()).ToArray()),
    };

    private static JsonObject FilterData(OwnerHookProofStage stage, JsonObject? data)
    {
        var filtered = new JsonObject();
        if (data is null)
            return filtered;

        if (stage is OwnerHookProofStage.SignatureResolved && TryGetSafeId(data["signatureId"], out var signatureId))
            filtered["signatureId"] = signatureId;
        else if (stage is OwnerHookProofStage.HookInstalled && TryGetSafeId(data["targetId"], out var targetId))
            filtered["targetId"] = targetId;
        else if (stage is OwnerHookProofStage.HitObserved
                 && data["observedHitCount"] is JsonValue observedHitCount
                 && observedHitCount.TryGetValue<int>(out var count)
                 && count >= 0)
            filtered["observedHitCount"] = count;
        else if (stage is OwnerHookProofStage.ContextCaptured)
        {
            if (data["questId"] is JsonValue questId)
            {
                if (questId.TryGetValue<uint>(out var unsignedQuestId) && unsignedQuestId > 0)
                    filtered["questId"] = unsignedQuestId;
                else if (questId.TryGetValue<int>(out var signedQuestId) && signedQuestId > 0)
                    filtered["questId"] = signedQuestId;
            }

            if (data["detailKind"] is JsonValue detailKind
                && detailKind.TryGetValue<string>(out var kind)
                && kind is "item" or "action")
                filtered["detailKind"] = kind;
        }

        return filtered;
    }

    private static bool TryGetSafeId(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (!TryGetString(node, out var candidate) || candidate.Length is 0 or > 128)
            return false;

        if (candidate.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            return false;

        value = candidate;
        return true;
    }

    private static bool TryGetSafeSummary(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (!TryGetString(node, out var candidate)
            || candidate.Length is 0 or > 256
            || candidate.Any(character => character is < ' ' or > '~'))
            return false;

        value = candidate;
        return true;
    }

    private static bool TryGetString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var candidate)
            || candidate is null)
            return false;

        value = candidate;
        return true;
    }
}
