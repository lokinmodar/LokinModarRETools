using System.Text.Json.Nodes;
using ReValidation.Common.Discovery;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class OwnerHookProofExecutorTests
{
    [Fact]
    public async Task CaptureAsync_WhenHitIsObserved_RecordsExplicitStages()
    {
        var registry = new OwnerHookTargetRegistry(
        [
            new OwnerHookTargetDefinition(
                "journalProvider",
                "journalProvider",
                "Open the Journal list.",
                new PassthroughHookContextCapture(),
                new NoOpHookMutationStrategy("Mutation proof is not configured.")),
        ]);
        var installer = new FakeOwnerHookInstaller(
            new FakeOwnerHook(observedHitCount: 1, [new JsonObject { ["questId"] = 1337 }]));
        var executor = new OwnerHookProofExecutor(registry, installer, new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)));

        using var session = await executor.CaptureAsync("journalProvider", CancellationToken.None);

        Assert.Collection(
            session.StageRecords,
            stage => Assert.Equal(OwnerHookProofStage.SignatureResolved, stage.Stage),
            stage => Assert.Equal(OwnerHookProofStage.HookInstalled, stage.Stage),
            stage => Assert.Equal(OwnerHookProofStage.HitObserved, stage.Stage),
            stage => Assert.Equal(OwnerHookProofStage.ContextCaptured, stage.Stage));
    }

    private sealed class PassthroughHookContextCapture : IHookContextCapture
    {
        public JsonObject Capture(JsonObject rawContext) => rawContext;
    }

    private sealed class FakeOwnerHookInstaller(IOwnerHook hook) : IOwnerHookInstaller
    {
        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution) => hook;
    }

    private sealed class FakeOwnerHook(int observedHitCount, IReadOnlyList<JsonObject> contexts) : IOwnerHook
    {
        public int ObservedHitCount { get; } = observedHitCount;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => contexts;

        public void Enable()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class StaticResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) => resolution;
    }
}
