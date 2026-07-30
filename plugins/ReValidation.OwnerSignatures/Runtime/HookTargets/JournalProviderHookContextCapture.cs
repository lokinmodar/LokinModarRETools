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

        if (questId.TryGetValue<uint>(out var unsignedValue) && unsignedValue > 0)
            captured["questId"] = unsignedValue;
        else if (questId.TryGetValue<ushort>(out var nativeValue) && nativeValue > 0)
            captured["questId"] = (uint)nativeValue;
        else if (questId.TryGetValue<int>(out var signedValue) && signedValue > 0)
            captured["questId"] = (uint)signedValue;

        return captured;
    }
}
