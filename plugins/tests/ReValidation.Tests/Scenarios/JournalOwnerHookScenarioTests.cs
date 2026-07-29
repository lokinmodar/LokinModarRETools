using System.Text.Json.Nodes;
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
    public async Task JournalHookValidation_Capture_ExportsExplicitOwnerHookStages()
    {
        var scenario = new JournalHookValidationOwnerScenario(
            new OwnerHookProofExecutor(
                new OwnerHookTargetRegistry(
                [
                    new OwnerHookTargetDefinition(
                        JournalHookTargetIds.JournalProvider,
                        "journalProvider",
                        "Open the Journal list.",
                        new PassthroughHookContextCapture(),
                        new NoOpHookMutationStrategy("Mutation proof is not configured.")),
                ]),
                new FakeOwnerHookInstaller(new FakeOwnerHook(1, [new JsonObject { ["questId"] = 42 }])),
                new StaticResolutionProvider(new SignatureResolution("journalProvider", 1, 0x1234, null))),
            JournalHookTargetIds.JournalProvider);
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        var capture = await scenario.CaptureAsync(context, CancellationToken.None);

        Assert.Equal("journalProvider", capture.Data["ownerHook"]!["targetId"]!.GetValue<string>());
        Assert.Equal("Passed", capture.Data["ownerHook"]!["stages"]![0]!["status"]!.GetValue<string>());
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
