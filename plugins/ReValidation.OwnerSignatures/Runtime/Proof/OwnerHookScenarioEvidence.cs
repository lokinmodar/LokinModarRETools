using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed record OwnerHookScenarioEvidence(
    string TargetId,
    IReadOnlyList<OwnerHookProofRecord> Stages)
{
    public static OwnerHookScenarioEvidence? FromCapture(JsonObject? captureData)
    {
        if (captureData?["ownerHook"] is not JsonObject ownerHook
            || ownerHook["targetId"]?.GetValue<string>() is not { Length: > 0 } targetId
            || ownerHook["stages"] is not JsonArray stages)
            return null;

        var records = new List<OwnerHookProofRecord>();
        foreach (var stage in stages.OfType<JsonObject>())
        {
            if (stage["stage"]?.GetValue<string>() is not { } stageName
                || !Enum.TryParse<OwnerHookProofStage>(stageName, out var proofStage)
                || stage["status"]?.GetValue<string>() is not { } statusName
                || !Enum.TryParse<OwnerHookProofStatus>(statusName, out var proofStatus)
                || stage["summary"]?.GetValue<string>() is not { } summary)
                continue;

            records.Add(new OwnerHookProofRecord(
                proofStage,
                proofStatus,
                summary,
                stage["data"] is JsonObject data ? (JsonObject)data.DeepClone() : new JsonObject()));
        }

        return new OwnerHookScenarioEvidence(targetId, records);
    }

    public JsonObject ToJson() => new()
    {
        ["targetId"] = TargetId,
        ["stages"] = new JsonArray(Stages.Select(stage => stage.ToJson()).ToArray()),
    };
}
