using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class OwnerHookTargetRegistryTests
{
    [Fact]
    public void Registry_Get_ThrowsForUnknownTarget()
    {
        var registry = new OwnerHookTargetRegistry([]);

        Assert.Throws<KeyNotFoundException>(() => registry.Get("unknown-target"));
    }

    [Fact]
    public void Installer_UsesBindingDeclaredByTarget()
    {
        var binding = new RecordingOwnerHookBinding();
        var target = new OwnerHookTargetDefinition(
            "journalProvider",
            "journalProvider",
            "Open the Journal list.",
            new PassthroughContextCapture(),
            new NoOpHookMutationStrategy("not used"),
            binding);
        var installer = new DalamudOwnerHookInstaller(moduleBase: 0x140000000);

        installer.Install(target, new SignatureResolution("journalProvider", 1, 0x1234, null));

        Assert.Equal(unchecked((nint)0x140001234UL), binding.InstalledAddress);
    }

    private sealed class RecordingOwnerHookBinding : IOwnerHookBinding
    {
        public nint InstalledAddress { get; private set; }

        public IOwnerHook Install(nint targetAddress)
        {
            InstalledAddress = targetAddress;
            return new StubOwnerHook();
        }
    }

    private sealed class PassthroughContextCapture : IHookContextCapture
    {
        public System.Text.Json.Nodes.JsonObject Capture(System.Text.Json.Nodes.JsonObject rawContext) => rawContext;
    }

    private sealed class StubOwnerHook : IOwnerHook
    {
        public int ObservedHitCount => 0;
        public IReadOnlyList<System.Text.Json.Nodes.JsonObject> DrainObservedContexts() => [];
        public void Enable() { }
        public void Dispose() { }
    }
}
