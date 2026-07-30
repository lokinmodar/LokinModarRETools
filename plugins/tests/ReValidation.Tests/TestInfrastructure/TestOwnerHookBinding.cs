using System.Text.Json.Nodes;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.Tests.TestInfrastructure;

internal sealed class TestOwnerHookBinding : IOwnerHookBinding
{
    public static TestOwnerHookBinding Instance { get; } = new();

    public IOwnerHook Install(nint targetAddress) => new TestOwnerHook();

    private sealed class TestOwnerHook : IOwnerHook
    {
        public int ObservedHitCount => 0;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => [];

        public void Enable()
        {
        }

        public void Dispose()
        {
        }
    }
}
