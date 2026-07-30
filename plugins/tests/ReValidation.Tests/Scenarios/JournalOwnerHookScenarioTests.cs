using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Execution;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;
using Xunit;

namespace ReValidation.Tests.Scenarios;

public sealed class JournalOwnerHookScenarioTests
{
    [Fact]
    public async Task JournalHookValidation_ArmsBeforeCueThenCapturesAndDisposes()
    {
        var hook = new FakeOwnerHook();
        var scenario = new JournalHookValidationOwnerScenario(
            new OwnerHookProofExecutor(
                new OwnerHookTargetRegistry(
                [
                    new OwnerHookTargetDefinition(
                        JournalHookTargetIds.JournalProvider,
                        "journalProvider",
                        "Open the Journal list.",
                        new PassthroughHookContextCapture(),
                        new NoOpHookMutationStrategy("Mutation proof is not configured."),
                        TestOwnerHookBinding.Instance),
                ]),
                new FakeOwnerHookInstaller(hook),
                new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null))),
            JournalHookTargetIds.JournalProvider);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);
        var armable = Assert.IsAssignableFrom<IArmableValidationScenario>(scenario);

        await armable.ArmAsync(CancellationToken.None);
        Assert.False((await armable.PollArmCueAsync(CancellationToken.None)).IsReady);

        hook.Observe(new JsonObject { ["questId"] = 42 });
        Assert.True((await armable.PollArmCueAsync(CancellationToken.None)).IsReady);

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.True(report.IsSuccess);
        Assert.Equal("journalProvider", report.Capture!.Data["ownerHook"]!["targetId"]!.GetValue<string>());
        Assert.Equal("Passed", report.Capture.Data["ownerHook"]!["stages"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("RestoreAttempted", GetFinalStageName(report.Capture.Data));
        Assert.True(hook.IsDisposed);
    }

    [Theory]
    [InlineData(ValidationMode.CaptureOnly)]
    [InlineData(ValidationMode.Compare)]
    public async Task JournalMutationProof_NonMutationModes_DisposeArmedHook(ValidationMode mode)
    {
        var hook = new FakeOwnerHook();
        var scenario = new JournalMutationProofOwnerScenario(
            new OwnerHookProofExecutor(
                new OwnerHookTargetRegistry(
                [
                    new OwnerHookTargetDefinition(
                        JournalHookTargetIds.JournalProvider,
                        "journalProvider",
                        "Open the Journal list.",
                        new PassthroughHookContextCapture(),
                        new NoOpHookMutationStrategy("Mutation proof is not configured."),
                        TestOwnerHookBinding.Instance),
                ]),
                new FakeOwnerHookInstaller(hook),
                new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null))),
            JournalHookTargetIds.JournalProvider);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, mode);

        await ((IArmableValidationScenario)scenario).ArmAsync(CancellationToken.None);
        hook.Observe(new JsonObject { ["questId"] = 42 });

        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.NotNull(report.Capture);
        Assert.Equal("RestoreAttempted", GetFinalStageName(report.Capture!.Data));
        Assert.True(hook.IsDisposed);
    }

    [Fact]
    public async Task JournalHookValidation_CaptureWithoutArm_RejectsImmediateDrain()
    {
        var scenario = CreateHookValidationScenario(new FakeOwnerHook());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.CaptureAsync(context, CancellationToken.None).AsTask());

        Assert.Contains("armed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JournalHookValidation_NonUniqueSignature_IsBlockedBeforeArming()
    {
        var scenario = CreateHookValidationScenario(
            new FakeOwnerHook(),
            new StaticResolutionProvider(new SignatureResolution("journalProvider", 2, null, "multiple matches")));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var precondition = await scenario.ValidateAsync(context, CancellationToken.None);

        Assert.False(precondition.CanRun);
        Assert.Equal("Signature 'journalProvider' was not uniquely resolved.", precondition.BlockingReason);
    }

    [Fact]
    public async Task JournalHookValidation_CaptureOnly_DoesNotPassWhenContextWasNotObserved()
    {
        var hook = new FakeOwnerHook();
        var scenario = CreateHookValidationScenario(
            hook,
            new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)),
            new JournalProviderHookContextCapture());
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        await scenario.ArmAsync(CancellationToken.None);
        hook.Observe(new JsonObject { ["questId"] = 0 });
        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.False(report.IsSuccess);
        Assert.Equal("capture", report.FailedPhase);
        Assert.Contains("context", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JournalMutationProof_FullProof_DisposesHookWhenContextWasNotObserved()
    {
        var hook = new FakeOwnerHook();
        var scenario = new JournalMutationProofOwnerScenario(
            new OwnerHookProofExecutor(
                new OwnerHookTargetRegistry(
                [
                    new OwnerHookTargetDefinition(
                        JournalHookTargetIds.JournalProvider,
                        "journalProvider",
                        "Open the Journal list.",
                        new JournalProviderHookContextCapture(),
                        new NoOpHookMutationStrategy("Mutation proof is not configured."),
                        TestOwnerHookBinding.Instance),
                ]),
                new FakeOwnerHookInstaller(hook),
                new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null))),
            JournalHookTargetIds.JournalProvider);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.FullProof);

        await scenario.ArmAsync(CancellationToken.None);
        hook.Observe(new JsonObject { ["questId"] = 0 });
        var report = await new ValidationScenarioRunner(new NullRouteMetadataProvider(context.Route), new NullEvidenceWriter())
            .RunAsync(scenario, context, CancellationToken.None);

        Assert.Equal("capture", report.FailedPhase);
        Assert.True(hook.IsDisposed);
    }

    private static JournalHookValidationOwnerScenario CreateHookValidationScenario(FakeOwnerHook hook) =>
        CreateHookValidationScenario(
            hook,
            new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null)),
            new PassthroughHookContextCapture());

    private static JournalHookValidationOwnerScenario CreateHookValidationScenario(
        FakeOwnerHook hook,
        ISignatureResolutionProvider resolutionProvider,
        IHookContextCapture? contextCapture = null) =>
        new(
            new OwnerHookProofExecutor(
                new OwnerHookTargetRegistry(
                [
                    new OwnerHookTargetDefinition(
                        JournalHookTargetIds.JournalProvider,
                        "journalProvider",
                        "Open the Journal list.",
                        contextCapture ?? new PassthroughHookContextCapture(),
                        new NoOpHookMutationStrategy("Mutation proof is not configured."),
                        TestOwnerHookBinding.Instance),
                ]),
                new FakeOwnerHookInstaller(hook),
                resolutionProvider),
            JournalHookTargetIds.JournalProvider);

    private static string GetFinalStageName(JsonObject data)
    {
        var stages = data["ownerHook"]!["stages"]!.AsArray();
        return stages[stages.Count - 1]!["stage"]!.GetValue<string>();
    }

    private sealed class PassthroughHookContextCapture : IHookContextCapture
    {
        public JsonObject Capture(JsonObject rawContext) => rawContext;
    }

    private sealed class FakeOwnerHookInstaller(IOwnerHook hook) : IOwnerHookInstaller
    {
        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution) => hook;
    }

    private sealed class FakeOwnerHook : IOwnerHook
    {
        private readonly Queue<JsonObject> contexts = new();

        public int ObservedHitCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public void Observe(JsonObject context)
        {
            contexts.Enqueue(context);
            ObservedHitCount++;
        }

        public IReadOnlyList<JsonObject> DrainObservedContexts()
        {
            var observed = contexts.ToArray();
            contexts.Clear();
            return observed;
        }

        public void Enable()
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class StaticResolutionProvider(SignatureResolution resolution) : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) => resolution;
    }

    private sealed class NullRouteMetadataProvider(ValidationRoute route) : IRouteMetadataProvider
    {
        public ValidationRoute Route => route;

        public ValueTask<IReadOnlyDictionary<string, string?>> GetMetadataAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    }

    private sealed class NullEvidenceWriter : IEvidenceWriter
    {
        public string Kind => "null";

        public ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new EvidenceWriteResult("null", string.Empty));
    }
}
