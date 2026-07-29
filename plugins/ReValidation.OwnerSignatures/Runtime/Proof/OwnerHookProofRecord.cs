using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed record OwnerHookProofRecord(
    OwnerHookProofStage Stage,
    OwnerHookProofStatus Status,
    string Summary,
    JsonObject Data)
{
    public JsonObject ToJson() => new()
    {
        ["stage"] = Stage.ToString(),
        ["status"] = Status.ToString(),
        ["summary"] = Summary,
        ["data"] = Data.DeepClone(),
    };
}
