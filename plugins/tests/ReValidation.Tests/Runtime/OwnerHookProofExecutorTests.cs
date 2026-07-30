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
                new NoOpHookMutationStrategy("Mutation proof is not configured."),
                TestOwnerHookBinding.Instance),
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

    [Fact]
    public async Task CaptureAsync_WhenCancellationIsAlreadyRequested_DoesNotInstallHook()
    {
        var hook = new FakeOwnerHook(observedHitCount: 0, []);
        var installer = new FakeOwnerHookInstaller(hook);
        var executor = new OwnerHookProofExecutor(CreateRegistry(new PassthroughHookContextCapture()), installer, new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.CaptureAsync("journalProvider", cancellation.Token).AsTask());

        Assert.Equal(0, installer.InstallCount);
        Assert.False(hook.IsDisposed);
    }

    [Fact]
    public async Task CaptureAsync_WhenCancellationIsRequestedAfterInstall_DisposesHook()
    {
        using var cancellation = new CancellationTokenSource();
        var hook = new FakeOwnerHook(observedHitCount: 0, [], cancellation.Cancel);
        var installer = new FakeOwnerHookInstaller(hook);
        var executor = new OwnerHookProofExecutor(CreateRegistry(new PassthroughHookContextCapture()), installer, new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)));

        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.CaptureAsync("journalProvider", cancellation.Token).AsTask());

        Assert.True(hook.IsDisposed);
    }

    [Fact]
    public async Task CaptureAsync_WhenContextCaptureThrows_DisposesHook()
    {
        var hook = new FakeOwnerHook(observedHitCount: 1, [new JsonObject()]);
        var installer = new FakeOwnerHookInstaller(hook);
        var executor = new OwnerHookProofExecutor(CreateRegistry(new ThrowingHookContextCapture()), installer, new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.CaptureAsync("journalProvider", CancellationToken.None).AsTask());

        Assert.True(hook.IsDisposed);
    }

    [Fact]
    public async Task CaptureAsync_WhenJournalContextIsRejected_MarksContextNotObserved()
    {
        var hook = new FakeOwnerHook(observedHitCount: 1, [new JsonObject { ["questId"] = 0 }]);
        var executor = new OwnerHookProofExecutor(
            CreateRegistry(new JournalProviderHookContextCapture()),
            new FakeOwnerHookInstaller(hook),
            new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)));

        using var session = await executor.CaptureAsync("journalProvider", CancellationToken.None);

        var contextStage = Assert.Single(session.StageRecords, stage => stage.Stage is OwnerHookProofStage.ContextCaptured);
        Assert.Equal(OwnerHookProofStatus.NotObserved, contextStage.Status);
        Assert.Empty(contextStage.Data);
    }

    [Fact]
    public void JournalContextCapture_NormalizesNativeUShortQuestId()
    {
        var capture = new JournalProviderHookContextCapture();

        var context = capture.Capture(new JsonObject { ["questId"] = (ushort)42 });

        Assert.Equal(42U, context["questId"]!.GetValue<uint>());
    }

    private static OwnerHookTargetRegistry CreateRegistry(IHookContextCapture contextCapture) =>
        new(
        [
            new OwnerHookTargetDefinition(
                "journalProvider",
                "journalProvider",
                "Open the Journal list.",
                contextCapture,
                new NoOpHookMutationStrategy("Mutation proof is not configured."),
                TestOwnerHookBinding.Instance),
        ]);

    private sealed class PassthroughHookContextCapture : IHookContextCapture
    {
        public JsonObject Capture(JsonObject rawContext) => rawContext;
    }

    private sealed class ThrowingHookContextCapture : IHookContextCapture
    {
        public JsonObject Capture(JsonObject rawContext) => throw new InvalidOperationException("capture failed");
    }

    private sealed class FakeOwnerHookInstaller(IOwnerHook hook) : IOwnerHookInstaller
    {
        public int InstallCount { get; private set; }

        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution)
        {
            InstallCount++;
            return hook;
        }
    }

    private sealed class FakeOwnerHook : IOwnerHook
    {
        private readonly IReadOnlyList<JsonObject> contexts;
        private readonly Action? onEnable;

        public FakeOwnerHook(int observedHitCount, IReadOnlyList<JsonObject> contexts, Action? onEnable = null)
        {
            ObservedHitCount = observedHitCount;
            this.contexts = contexts;
            this.onEnable = onEnable;
        }

        public int ObservedHitCount { get; }

        public bool IsDisposed { get; private set; }

        public IReadOnlyList<JsonObject> DrainObservedContexts() => contexts;

        public void Enable() => onEnable?.Invoke();

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StaticResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) => resolution;
    }
}
