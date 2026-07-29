using System.Text.Json.Nodes;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public interface IOwnerHook : IDisposable
{
    int ObservedHitCount { get; }

    IReadOnlyList<JsonObject> DrainObservedContexts();

    void Enable();
}
