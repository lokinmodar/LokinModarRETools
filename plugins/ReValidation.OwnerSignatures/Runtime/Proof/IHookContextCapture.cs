using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public interface IHookContextCapture
{
    JsonObject Capture(JsonObject rawContext);
}
