using System.Text.Json.Nodes;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs;
using ReValidation.LocalClientStructs.Scenarios;
using ReValidation.LocalClientStructs.Services;
using ReValidation.OwnerSignatures;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Scenarios;
using ReValidation.OwnerSignatures.Services;
using Xunit;

public sealed class RouteScenarioCompositionTests
{
    [Theory]
    [InlineData(ValidationRoute.LocalClientStructs)]
    [InlineData(ValidationRoute.OwnerSignatures)]
    public void CreateRegistry_RegistersJournalAndTooltipScenarios(ValidationRoute route)
    {
        var registry = route is ValidationRoute.LocalClientStructs
            ? LocalClientStructsScenarioComposition.CreateRegistry()
            : OwnerSignaturesScenarioComposition.CreateRegistry();

        Assert.Equal(3, registry.Scenarios.Count);
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "journal.completed-entries");
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.item-detail");
        Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.action-detail");
        Assert.All(registry.Scenarios, scenario => Assert.Contains(route, scenario.Definition.SupportedRoutes));
        Assert.DoesNotContain(registry.Scenarios, scenario => scenario is NotConfiguredValidationScenario);

        if (route is ValidationRoute.LocalClientStructs)
        {
            Assert.Contains(registry.Scenarios, scenario => scenario is JournalCompletedEntriesLocalScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipItemDetailLocalScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipActionDetailLocalScenario);
        }
        else
        {
            Assert.Contains(registry.Scenarios, scenario => scenario is JournalCompletedEntriesOwnerScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipItemDetailOwnerScenario);
            Assert.Contains(registry.Scenarios, scenario => scenario is TooltipActionDetailOwnerScenario);
        }
    }

    [Theory]
    [InlineData(ValidationRoute.LocalClientStructs)]
    [InlineData(ValidationRoute.OwnerSignatures)]
    public async Task ComposedScenarios_BlockOnMissingRuntimeAdapters(ValidationRoute route)
    {
        var registry = route is ValidationRoute.LocalClientStructs
            ? LocalClientStructsScenarioComposition.CreateRegistry()
            : OwnerSignaturesScenarioComposition.CreateRegistry();
        var context = ScenarioExecutionContext.CreateForTests(route, ValidationMode.CaptureOnly);

        foreach (var scenario in registry.Scenarios)
        {
            var precondition = await scenario.ValidateAsync(context, CancellationToken.None);

            Assert.False(precondition.CanRun);
            Assert.Contains("runtime", precondition.BlockingReason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LocalComposition_CanRunCaptureOnly_WhenDependenciesAreConfigured()
    {
        var registry = LocalClientStructsScenarioComposition.CreateRegistry(
            new LocalClientStructsScenarioDependencies(
                new FakeJournalProbe(),
                new FakeTooltipProbe("item"),
                new FakeTooltipProbe("action"),
                new LocalClientStructsAvailabilityDetector(hasLocalConfiguration: true, projectPath: "C:\\Dante\\_dalamud\\FFXIVClientStructs\\FFXIVClientStructs.csproj")));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.LocalClientStructs, ValidationMode.CaptureOnly);

        foreach (var scenario in registry.Scenarios)
        {
            var precondition = await scenario.ValidateAsync(context, CancellationToken.None);
            Assert.True(precondition.CanRun);
            Assert.Null(precondition.BlockingReason);
        }
    }

    [Fact]
    public async Task OwnerComposition_CanRunCaptureOnly_WhenDependenciesAreConfigured()
    {
        var resolutions = new[]
        {
            new SignatureResolution("journalProvider", 1, 0x1234, null),
            new SignatureResolution("itemTooltip", 1, 0x2234, null),
            new SignatureResolution("actionTooltip", 1, 0x3234, null),
        };

        var registry = OwnerSignaturesScenarioComposition.CreateRegistry(
            new OwnerSignaturesScenarioDependencies(
                new FakeJournalProbe(),
                new FakeTooltipProbe("item"),
                new FakeTooltipProbe("action"),
                resolutions,
                HookProofExecutor: CreateOwnerHookProofExecutor()));
        var context = ScenarioExecutionContext.CreateForTests(ValidationRoute.OwnerSignatures, ValidationMode.CaptureOnly);

        foreach (var scenario in registry.Scenarios)
        {
            var precondition = await scenario.ValidateAsync(context, CancellationToken.None);
            Assert.True(precondition.CanRun);
            Assert.Null(precondition.BlockingReason);
        }
    }

    [Theory]
    [InlineData(ValidationRoute.LocalClientStructs)]
    [InlineData(ValidationRoute.OwnerSignatures)]
    public async Task ComposedJournalScenario_BlocksMutationModes_WhenMutationProofIsDisabled(ValidationRoute route)
    {
        var journalScenario = route is ValidationRoute.LocalClientStructs
            ? LocalClientStructsScenarioComposition.CreateRegistry(
                new LocalClientStructsScenarioDependencies(
                    new FakeJournalProbe(),
                    new FakeTooltipProbe("item"),
                    new FakeTooltipProbe("action"),
                    new LocalClientStructsAvailabilityDetector(hasLocalConfiguration: true, projectPath: Path.Combine(RepoRoot.Find(), "README.md")),
                    new FakeJournalComparisonSource(),
                    SupportsJournalMutationProof: false,
                    JournalMutationBlockingReason: "Journal override proof is not configured."))
                .Scenarios.Single(scenario => scenario.Definition.Id == "journal.completed-entries")
            : OwnerSignaturesScenarioComposition.CreateRegistry(
                new OwnerSignaturesScenarioDependencies(
                    new FakeJournalProbe(),
                    new FakeTooltipProbe("item"),
                    new FakeTooltipProbe("action"),
                    [
                        new SignatureResolution("journalProvider", 1, 0x1234, null),
                        new SignatureResolution("itemTooltip", 1, 0x2234, null),
                        new SignatureResolution("actionTooltip", 1, 0x3234, null),
                    ],
                    new FakeJournalComparisonSource(),
                    SupportsJournalMutationProof: false,
                    JournalMutationBlockingReason: "Journal override proof is not configured."))
                .Scenarios.Single(scenario => scenario.Definition.Id == "journal.completed-entries");
        var context = ScenarioExecutionContext.CreateForTests(route, ValidationMode.FullProof);

        var precondition = await journalScenario.ValidateAsync(context, CancellationToken.None);

        Assert.False(precondition.CanRun);
        Assert.Equal("Journal override proof is not configured.", precondition.BlockingReason);
    }

    private sealed class FakeJournalProbe : IJournalCompletedEntriesProbe
    {
        public ValueTask<JournalCompletedEntriesSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new JournalCompletedEntriesSnapshot(
                [new JournalEntryRecord(1, 0, "The Company You Keep", "65632", "test")],
                "1 entry"));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioOverrideTicket?>(null);

        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioAssertResult?>(null);

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioRestoreResult(true, "noop", []));
    }

    private sealed class FakeJournalComparisonSource : IJournalCompletedEntriesComparisonSource
    {
        public ValueTask<JournalCompletedEntriesSnapshot> CaptureReferenceAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new JournalCompletedEntriesSnapshot(
                [new JournalEntryRecord(1, 0, "The Company You Keep", "65632", "reference")],
                "1 entry"));
    }

    private sealed class FakeTooltipProbe(string detailKind) : ITooltipProbe
    {
        public ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TooltipSnapshot(detailKind, 42, ["Line 1"], "Visible text"));

        public ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioOverrideTicket?>(null);

        public ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ScenarioAssertResult?>(null);

        public ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ScenarioRestoreResult(true, "noop", []));
    }

    private static OwnerHookProofExecutor CreateOwnerHookProofExecutor() =>
        new(
            new OwnerHookTargetRegistry(
            [
                new OwnerHookTargetDefinition("itemTooltip", "itemTooltip", "Open an item tooltip.", new TooltipHookContextCapture("item"), new NoOpHookMutationStrategy("not used"), TestOwnerHookBinding.Instance),
                new OwnerHookTargetDefinition("actionTooltip", "actionTooltip", "Open an action tooltip.", new TooltipHookContextCapture("action"), new NoOpHookMutationStrategy("not used"), TestOwnerHookBinding.Instance),
            ]),
            new FakeOwnerHookInstaller(),
            new FakeResolutionProvider());

    private sealed class FakeOwnerHookInstaller : IOwnerHookInstaller
    {
        public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution) => new FakeOwnerHook();
    }

    private sealed class FakeOwnerHook : IOwnerHook
    {
        public int ObservedHitCount => 0;

        public IReadOnlyList<JsonObject> DrainObservedContexts() => [];

        public void Enable() { }

        public void Dispose() { }
    }

    private sealed class FakeResolutionProvider : ISignatureResolutionProvider
    {
        public SignatureResolution GetResolution(string signatureId) => new(signatureId, 1, 0x1234, null);
    }
}
