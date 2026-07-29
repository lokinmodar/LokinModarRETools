using System.Text.Json.Nodes;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed class JournalProviderHookContextCapture : IHookContextCapture
{
    public JsonObject Capture(JsonObject rawContext)
    {
        var captured = new JsonObject();
        if (rawContext["questId"] is not JsonValue questId)
            return captured;

        if (questId.TryGetValue<uint>(out var value) && value > 0)
            captured["questId"] = value;

        return captured;
    }
}
