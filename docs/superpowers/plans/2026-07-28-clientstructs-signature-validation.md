# ClientStructs Signature Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add discovery-driven branch validation that finds new or changed `FFXIVClientStructs` Journal and tooltip bindings, then proves `1/2/3/4` across both `ReValidation.LocalClientStructs` and `ReValidation.OwnerSignatures`.

**Architecture:** Add a shared discovery and proof-planning layer in `ReValidation.Common`, keep both current route plugins as the runtime surfaces, and introduce a parallel branch-validation runner that executes proof groups generated from the local `FFXIVClientStructs` checkout. Tooltip groups should close `1/2/3/4` immediately, while Journal groups must close `1/2/3` and block `4` honestly until a real pre-UI effect point is implemented.

**Tech Stack:** .NET 10, Dalamud plugin services, xUnit, local `FFXIVClientStructs` git checkout parsing, shared JSON/Markdown evidence writers.

## Global Constraints

- Discovery source must be the local `FFXIVClientStructs` checkout.
- Default `baseRef` is `upstream/main`.
- Supported discovery families are `journal`, `item-tooltip`, and `action-tooltip`.
- Required proof bar for branch validation is `1/2/3/4`; lower proof levels are diagnostic only.
- No validation step may trigger server traffic.
- Tooltip proof groups must end with visible-effect restore.
- Journal proof groups must block level `4` explicitly until a real pre-UI mutation point is proven.
- Keep Windows verification sequential to avoid local build/test file-lock races.

## Planned File Map

- `plugins/ReValidation.Common/Discovery/ClientStructsDiscoveryOptions.cs`
  Holds discovery mode, base ref, family filter, and target filter.
- `plugins/ReValidation.Common/Discovery/DiscoveredTarget.cs`
  Normalized description of one changed interop binding.
- `plugins/ReValidation.Common/Discovery/DiscoveredTargetCatalog.cs`
  Aggregate result of one discovery run plus warnings.
- `plugins/ReValidation.Common/Discovery/IClientStructsDiscoveryService.cs`
  Shared discovery contract for both routes.
- `plugins/ReValidation.Common/Discovery/ClientStructsGitDiffDiscoveryService.cs`
  Uses `git diff <baseRef>...HEAD` plus source parsing to populate the catalog.
- `plugins/ReValidation.Common/Discovery/InteropBindingSourceParser.cs`
  Extracts `[MemberFunction]`, `[StaticAddress]`, and `[VirtualFunction]` bindings from approved target files.
- `plugins/ReValidation.Common/Proof/ProofGroupDefinition.cs`
  Represents one executable proof group derived from discovered targets.
- `plugins/ReValidation.Common/Proof/BranchValidationPlan.cs`
  Stores the catalog plus generated proof groups and execution parameters.
- `plugins/ReValidation.Common/Proof/ProofPlanBuilder.cs`
  Groups discovered targets by cue family and proof profile.
- `plugins/ReValidation.Common/Proof/TargetProofRecord.cs`
  Stores `resolve`, `observe`, `hook`, and `effect` outcomes per target.
- `plugins/ReValidation.Common/Proof/ProofGroupRunReport.cs`
  Stores one proof-group execution result.
- `plugins/ReValidation.Common/Proof/BranchValidationRunReport.cs`
  Aggregate run report for all proof groups in scope.
- `plugins/ReValidation.Common/Proof/IBranchValidationRunner.cs`
  Shared execution contract for discovery-driven validation.
- `plugins/ReValidation.Common/Proof/BranchValidationRunner.cs`
  Runs groups, aggregates per-target records, and exports evidence.
- `plugins/ReValidation.Common/Proof/IBranchValidationRouteAdapter.cs`
  Route-specific hook/observe/effect adapter contract.
- `plugins/ReValidation.Common/Evidence/BranchValidationEvidenceEnvelope.cs`
  Sanitized export payload for discovery-driven branch validation.
- `plugins/ReValidation.Common/Evidence/BranchJsonEvidenceWriter.cs`
  JSON artifact writer for branch validation runs.
- `plugins/ReValidation.Common/Evidence/BranchMarkdownEvidenceWriter.cs`
  Markdown artifact writer for branch validation runs.
- `plugins/ReValidation.Common/UI/ValidationWindowState.cs`
  Extended with discovery settings, discovered groups, and branch-run selection state.
- `plugins/ReValidation.Common/UI/ValidationWindowController.cs`
  Extended with discover/run-group/run-all orchestration for branch validation.
- `plugins/ReValidation.LocalClientStructs/Services/LocalBranchValidationRouteAdapter.cs`
  Local route implementation of proof execution.
- `plugins/ReValidation.OwnerSignatures/Services/OwnerBranchValidationRouteAdapter.cs`
  Owner-signature route implementation of proof execution.
- `plugins/ReValidation.LocalClientStructs/Plugin.cs`
  Wires discovery service, local route adapter, and branch evidence writers.
- `plugins/ReValidation.OwnerSignatures/Plugin.cs`
  Wires discovery service, owner route adapter, and branch evidence writers.
- `plugins/ReValidation.LocalClientStructs/Windows/ValidationWindow.cs`
  Adds discovery-driven controls to the local route UI.
- `plugins/ReValidation.OwnerSignatures/Windows/ValidationWindow.cs`
  Adds discovery-driven controls to the owner route UI.
- `plugins/tests/ReValidation.Tests/Discovery/*.cs`
  Discovery and parsing tests.
- `plugins/tests/ReValidation.Tests/Proof/*.cs`
  Proof grouping and aggregate runner tests.
- `plugins/tests/ReValidation.Tests/Routes/*.cs`
  Route adapter and composition tests.
- `plugins/tests/ReValidation.Tests/UI/*.cs`
  Controller/window-state tests for discovery-driven UX.
- `plugins/tests/ReValidation.Tests/Evidence/*.cs`
  JSON/Markdown evidence serialization tests.

---

### Task 1: Add Discovery Options And Target Catalog Models

**Files:**
- Create: `plugins/ReValidation.Common/Discovery/ClientStructsDiscoveryOptions.cs`
- Create: `plugins/ReValidation.Common/Discovery/DiscoveredTarget.cs`
- Create: `plugins/ReValidation.Common/Discovery/DiscoveredTargetCatalog.cs`
- Create: `plugins/ReValidation.Common/Discovery/DiscoveryEnums.cs`
- Test: `plugins/tests/ReValidation.Tests/Discovery/DiscoveredTargetCatalogTests.cs`

**Interfaces:**
- Consumes: existing `ValidationRoute` and `ValidationMode` enums only for later proof planning
- Produces: `ClientStructsDiscoveryOptions`, `DiscoveredTarget`, `DiscoveredTargetCatalog`, `DiscoveryMode`, `TargetFamily`, `CueFamily`, `BindingKind`, `ProofProfile`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void FilterTargets_ReturnsOnlyRequestedFamilyAndPrefix()
{
    var options = new ClientStructsDiscoveryOptions(
        repositoryPath: @"C:\Dante\_dalamud\FFXIVClientStructs-journal-tooltip-unified",
        baseRef: "upstream/main",
        mode: DiscoveryMode.Diff,
        families: [TargetFamily.ItemTooltip, TargetFamily.ActionTooltip],
        targetFilter: "AddonItemDetail.");
    var catalog = new DiscoveredTargetCatalog(
        options,
        [
            new DiscoveredTarget("AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89", @"FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs", true, TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4),
            new DiscoveredTarget("Journal.Instance", "Journal", "Instance", BindingKind.StaticAddress, "48 8D 0D", @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.StaticAddressConsumer, 4),
        ],
        warnings: []);

    var filtered = catalog.Filter(TargetFamily.ItemTooltip, "AddonItemDetail.");

    Assert.Single(filtered);
    Assert.Equal("AddonItemDetail.GenerateTooltip", filtered[0].TargetId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~DiscoveredTargetCatalogTests"`
Expected: compile failure because the discovery enums and catalog types do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed record ClientStructsDiscoveryOptions(
    string RepositoryPath,
    string BaseRef,
    DiscoveryMode Mode,
    IReadOnlyList<TargetFamily> Families,
    string? TargetFilter);

public sealed record DiscoveredTarget(
    string TargetId,
    string DeclaringType,
    string MemberName,
    BindingKind BindingKind,
    string PatternOrAddress,
    string SourceFile,
    bool ChangedAgainstBaseRef,
    TargetFamily Family,
    CueFamily CueFamily,
    ProofProfile ProofProfile,
    int RequiredProofLevel);

public sealed record DiscoveredTargetCatalog(
    ClientStructsDiscoveryOptions Options,
    IReadOnlyList<DiscoveredTarget> Targets,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<DiscoveredTarget> Filter(TargetFamily family, string? targetPrefix) =>
        Targets
            .Where(target => target.Family == family)
            .Where(target => string.IsNullOrWhiteSpace(targetPrefix) || target.TargetId.StartsWith(targetPrefix, StringComparison.Ordinal))
            .ToArray();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~DiscoveredTargetCatalogTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/Discovery/ClientStructsDiscoveryOptions.cs plugins/ReValidation.Common/Discovery/DiscoveredTarget.cs plugins/ReValidation.Common/Discovery/DiscoveredTargetCatalog.cs plugins/ReValidation.Common/Discovery/DiscoveryEnums.cs plugins/tests/ReValidation.Tests/Discovery/DiscoveredTargetCatalogTests.cs
git commit -m "feat: add discovery target catalog models"
```

### Task 2: Implement Git-Backed ClientStructs Discovery And Source Parsing

**Files:**
- Create: `plugins/ReValidation.Common/Discovery/IClientStructsDiscoveryService.cs`
- Create: `plugins/ReValidation.Common/Discovery/IGitDiffReader.cs`
- Create: `plugins/ReValidation.Common/Discovery/GitProcessDiffReader.cs`
- Create: `plugins/ReValidation.Common/Discovery/InteropBindingSourceParser.cs`
- Create: `plugins/ReValidation.Common/Discovery/ClientStructsGitDiffDiscoveryService.cs`
- Test: `plugins/tests/ReValidation.Tests/Discovery/InteropBindingSourceParserTests.cs`
- Test: `plugins/tests/ReValidation.Tests/Discovery/ClientStructsGitDiffDiscoveryServiceTests.cs`

**Interfaces:**
- Consumes: `ClientStructsDiscoveryOptions`, `DiscoveredTargetCatalog`
- Produces: `IClientStructsDiscoveryService.DiscoverAsync(ClientStructsDiscoveryOptions options, CancellationToken cancellationToken)`, `IGitDiffReader.ReadChangedFilesAsync(string repositoryPath, string baseRef, CancellationToken cancellationToken)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void ParseBindings_ExtractsJournalAndTooltipInteropMembers()
{
    var parser = new InteropBindingSourceParser();
    var source = """
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace FFXIVClientStructs.FFXIV.Client.Game.UI;
public partial struct Journal {
    [StaticAddress("48 8D 0D ?? ?? ?? ?? 66 89 83", 3)]
    public static partial Journal* Instance();
    [MemberFunction("48 89 5C 24 ?? 48 89 74 24 ??")]
    public partial bool IsEntryComplete(uint questId);
}
""";

    var bindings = parser.Parse(
        @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs",
        source,
        changedAgainstBaseRef: true);

    Assert.Contains(bindings, x => x.TargetId == "Journal.Instance" && x.BindingKind == BindingKind.StaticAddress);
    Assert.Contains(bindings, x => x.TargetId == "Journal.IsEntryComplete" && x.BindingKind == BindingKind.MemberFunction);
}

[Fact]
public async Task DiscoverAsync_ReturnsOnlyChangedBindingsFromApprovedFamilies()
{
    var diffReader = new FakeGitDiffReader(
        [
            @"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs",
            @"FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs",
            @"FFXIVClientStructs\FFXIV\Client\UI\Agent\AgentMap.cs",
        ]);
    var parser = new InteropBindingSourceParser();
    var discovery = new ClientStructsGitDiffDiscoveryService(diffReader, parser, new FakeFileReader(new Dictionary<string, string>
    {
        [@"FFXIVClientStructs\FFXIV\Client\Game\UI\Journal.cs"] = journalSource,
        [@"FFXIVClientStructs\FFXIV\Client\UI\AddonItemDetail.cs"] = itemSource,
        [@"FFXIVClientStructs\FFXIV\Client\UI\Agent\AgentMap.cs"] = unrelatedSource,
    }));

    var catalog = await discovery.DiscoverAsync(
        new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.Journal, TargetFamily.ItemTooltip], null),
        CancellationToken.None);

    Assert.Contains(catalog.Targets, x => x.TargetId == "Journal.Instance");
    Assert.Contains(catalog.Targets, x => x.TargetId == "AddonItemDetail.GenerateTooltip");
    Assert.DoesNotContain(catalog.Targets, x => x.DeclaringType == "AgentMap");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~InteropBindingSourceParserTests|FullyQualifiedName~ClientStructsGitDiffDiscoveryServiceTests"`
Expected: compile failure because the discovery service, parser, and git diff reader contracts do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public interface IClientStructsDiscoveryService
{
    ValueTask<DiscoveredTargetCatalog> DiscoverAsync(ClientStructsDiscoveryOptions options, CancellationToken cancellationToken);
}

public interface IGitDiffReader
{
    ValueTask<IReadOnlyList<string>> ReadChangedFilesAsync(string repositoryPath, string baseRef, CancellationToken cancellationToken);
}

public sealed class ClientStructsGitDiffDiscoveryService : IClientStructsDiscoveryService
{
    public async ValueTask<DiscoveredTargetCatalog> DiscoverAsync(ClientStructsDiscoveryOptions options, CancellationToken cancellationToken)
    {
        var changedFiles = await diffReader.ReadChangedFilesAsync(options.RepositoryPath, options.BaseRef, cancellationToken);
        var approvedFiles = changedFiles.Where(IsApprovedFamilyPath);
        var targets = approvedFiles.SelectMany(ReadAndParseTargets).ToArray();
        return new DiscoveredTargetCatalog(options, targets, []);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~InteropBindingSourceParserTests|FullyQualifiedName~ClientStructsGitDiffDiscoveryServiceTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/Discovery/IClientStructsDiscoveryService.cs plugins/ReValidation.Common/Discovery/IGitDiffReader.cs plugins/ReValidation.Common/Discovery/GitProcessDiffReader.cs plugins/ReValidation.Common/Discovery/InteropBindingSourceParser.cs plugins/ReValidation.Common/Discovery/ClientStructsGitDiffDiscoveryService.cs plugins/tests/ReValidation.Tests/Discovery/InteropBindingSourceParserTests.cs plugins/tests/ReValidation.Tests/Discovery/ClientStructsGitDiffDiscoveryServiceTests.cs
git commit -m "feat: add git-backed clientstructs discovery"
```

### Task 3: Build Proof Planning And Group Generation

**Files:**
- Create: `plugins/ReValidation.Common/Proof/ProofGroupDefinition.cs`
- Create: `plugins/ReValidation.Common/Proof/BranchValidationPlan.cs`
- Create: `plugins/ReValidation.Common/Proof/ProofPlanBuilder.cs`
- Test: `plugins/tests/ReValidation.Tests/Proof/ProofPlanBuilderTests.cs`

**Interfaces:**
- Consumes: `DiscoveredTargetCatalog`, `DiscoveredTarget`
- Produces: `BranchValidationPlan`, `ProofGroupDefinition`, `ProofPlanBuilder.Build(DiscoveredTargetCatalog catalog, int requiredProofLevel)`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Build_GroupsTargetsByCueFamilyAndProofProfile()
{
    var options = new ClientStructsDiscoveryOptions(@"C:\repo", "upstream/main", DiscoveryMode.Diff, [TargetFamily.Journal, TargetFamily.ItemTooltip], null);
    var catalog = new DiscoveredTargetCatalog(
        options,
        [
            new DiscoveredTarget("Journal.Instance", "Journal", "Instance", BindingKind.StaticAddress, "48 8D 0D", @"Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.StaticAddressConsumer, 4),
            new DiscoveredTarget("Journal.GetQuestData", "Journal", "GetQuestData", BindingKind.MemberFunction, "48 89 5C 24", @"Journal.cs", true, TargetFamily.Journal, CueFamily.JournalCompletedList, ProofProfile.ConsumerChain, 4),
            new DiscoveredTarget("AddonItemDetail.GenerateTooltip", "AddonItemDetail", "GenerateTooltip", BindingKind.MemberFunction, "48 89 5C 24", @"AddonItemDetail.cs", true, TargetFamily.ItemTooltip, CueFamily.TooltipItemDetail, ProofProfile.DetourFunction, 4),
        ],
        []);

    var plan = new ProofPlanBuilder().Build(catalog, requiredProofLevel: 4);

    Assert.Equal(2, plan.Groups.Count);
    Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.JournalCompletedList && x.Targets.Count == 2);
    Assert.Contains(plan.Groups, x => x.CueFamily == CueFamily.TooltipItemDetail && x.Targets.Count == 1);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ProofPlanBuilderTests"`
Expected: compile failure because the proof plan types do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed record ProofGroupDefinition(
    string GroupId,
    CueFamily CueFamily,
    ProofProfile ProofProfile,
    IReadOnlyList<DiscoveredTarget> Targets);

public sealed record BranchValidationPlan(
    ClientStructsDiscoveryOptions DiscoveryOptions,
    IReadOnlyList<DiscoveredTarget> Targets,
    IReadOnlyList<ProofGroupDefinition> Groups,
    int RequiredProofLevel);

public sealed class ProofPlanBuilder
{
    public BranchValidationPlan Build(DiscoveredTargetCatalog catalog, int requiredProofLevel)
    {
        var groups = catalog.Targets
            .GroupBy(x => (x.CueFamily, x.ProofProfile))
            .Select(group => new ProofGroupDefinition(
                $"{group.Key.CueFamily}:{group.Key.ProofProfile}",
                group.Key.CueFamily,
                group.Key.ProofProfile,
                group.ToArray()))
            .ToArray();

        return new BranchValidationPlan(catalog.Options, catalog.Targets, groups, requiredProofLevel);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ProofPlanBuilderTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/Proof/ProofGroupDefinition.cs plugins/ReValidation.Common/Proof/BranchValidationPlan.cs plugins/ReValidation.Common/Proof/ProofPlanBuilder.cs plugins/tests/ReValidation.Tests/Proof/ProofPlanBuilderTests.cs
git commit -m "feat: add branch proof planning"
```

### Task 4: Add Branch Validation Runner And Per-Target Evidence

**Files:**
- Create: `plugins/ReValidation.Common/Proof/TargetProofRecord.cs`
- Create: `plugins/ReValidation.Common/Proof/ProofGroupRunReport.cs`
- Create: `plugins/ReValidation.Common/Proof/BranchValidationRunReport.cs`
- Create: `plugins/ReValidation.Common/Proof/IBranchValidationRunner.cs`
- Create: `plugins/ReValidation.Common/Proof/IBranchValidationRouteAdapter.cs`
- Create: `plugins/ReValidation.Common/Proof/BranchValidationRunner.cs`
- Create: `plugins/ReValidation.Common/Evidence/BranchValidationEvidenceEnvelope.cs`
- Create: `plugins/ReValidation.Common/Evidence/BranchJsonEvidenceWriter.cs`
- Create: `plugins/ReValidation.Common/Evidence/BranchMarkdownEvidenceWriter.cs`
- Test: `plugins/tests/ReValidation.Tests/Proof/BranchValidationRunnerTests.cs`
- Test: `plugins/tests/ReValidation.Tests/Evidence/BranchValidationEvidenceWriterTests.cs`

**Interfaces:**
- Consumes: `BranchValidationPlan`, `ProofGroupDefinition`
- Produces: `IBranchValidationRunner.RunAsync(BranchValidationPlan plan, IBranchValidationRouteAdapter routeAdapter, string evidenceRoot, CancellationToken cancellationToken)`, `TargetProofRecord`, `ProofGroupRunReport`, `BranchValidationRunReport`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task RunAsync_FailsAggregateWhenAnyTargetIsEffectNotProven()
{
    var plan = BranchValidationPlanFactory.CreateSingleTooltipPlan();
    var routeAdapter = new FakeBranchValidationRouteAdapter(
        proofReports:
        [
            new ProofGroupRunReport(
                "tooltip.item-detail:detour-function",
                [
                    new TargetProofRecord("AddonItemDetail.GenerateTooltip", "passed", matchCount: 1, rva: 0x1234, observedHitCount: 1, hookInstalled: true, effectApplied: true, effectRestored: true, blockingReason: null),
                    new TargetProofRecord("AgentItemDetail.ReceiveEvent", "effect-not-proven", matchCount: 1, rva: 0x2234, observedHitCount: 1, hookInstalled: true, effectApplied: false, effectRestored: false, blockingReason: "No reversible effect was verified."),
                ],
                artifactSummary: "tooltip group"));

    var report = await new BranchValidationRunner(new BranchJsonEvidenceWriter(new EvidencePathBuilder()), new BranchMarkdownEvidenceWriter(new EvidencePathBuilder()))
        .RunAsync(plan, routeAdapter, Path.GetTempPath(), CancellationToken.None);

    Assert.False(report.IsSuccess);
    Assert.Contains(report.Targets, x => x.TargetId == "AgentItemDetail.ReceiveEvent" && x.Verdict == "effect-not-proven");
}

[Fact]
public async Task WriteAsync_ExportsPerTargetProofRecords()
{
    var report = BranchValidationRunReportFactory.CreatePassed();
    var writer = new BranchJsonEvidenceWriter(new EvidencePathBuilder());

    var result = await writer.WriteAsync(report, @"C:\evidence", CancellationToken.None);
    var json = await File.ReadAllTextAsync(result.OutputPath!);

    Assert.Contains("\"targetId\":\"AddonItemDetail.GenerateTooltip\"", json, StringComparison.Ordinal);
    Assert.Contains("\"hookInstalled\":true", json, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~BranchValidationRunnerTests|FullyQualifiedName~BranchValidationEvidenceWriterTests"`
Expected: compile failure because the branch validation runner and branch evidence writers do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public interface IBranchValidationRouteAdapter
{
    ValidationRoute Route { get; }
    ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken);
}

public interface IBranchValidationRunner
{
    ValueTask<BranchValidationRunReport> RunAsync(BranchValidationPlan plan, IBranchValidationRouteAdapter routeAdapter, string evidenceRoot, CancellationToken cancellationToken);
}

public sealed class BranchValidationRunner : IBranchValidationRunner
{
    public async ValueTask<BranchValidationRunReport> RunAsync(BranchValidationPlan plan, IBranchValidationRouteAdapter routeAdapter, string evidenceRoot, CancellationToken cancellationToken)
    {
        var groupReports = new List<ProofGroupRunReport>();
        foreach (var group in plan.Groups)
            groupReports.Add(await routeAdapter.RunProofGroupAsync(group, plan.RequiredProofLevel, cancellationToken));

        return BranchValidationRunReport.From(plan, routeAdapter.Route, groupReports);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~BranchValidationRunnerTests|FullyQualifiedName~BranchValidationEvidenceWriterTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/Proof/TargetProofRecord.cs plugins/ReValidation.Common/Proof/ProofGroupRunReport.cs plugins/ReValidation.Common/Proof/BranchValidationRunReport.cs plugins/ReValidation.Common/Proof/IBranchValidationRunner.cs plugins/ReValidation.Common/Proof/IBranchValidationRouteAdapter.cs plugins/ReValidation.Common/Proof/BranchValidationRunner.cs plugins/ReValidation.Common/Evidence/BranchValidationEvidenceEnvelope.cs plugins/ReValidation.Common/Evidence/BranchJsonEvidenceWriter.cs plugins/ReValidation.Common/Evidence/BranchMarkdownEvidenceWriter.cs plugins/tests/ReValidation.Tests/Proof/BranchValidationRunnerTests.cs plugins/tests/ReValidation.Tests/Evidence/BranchValidationEvidenceWriterTests.cs
git commit -m "feat: add branch validation runner and evidence"
```

### Task 5: Implement Tooltip Proof Execution For Both Routes

**Files:**
- Create: `plugins/ReValidation.LocalClientStructs/Services/LocalBranchValidationRouteAdapter.cs`
- Create: `plugins/ReValidation.OwnerSignatures/Services/OwnerBranchValidationRouteAdapter.cs`
- Create: `plugins/ReValidation.LocalClientStructs/Runtime/LocalTooltipProofExecutor.cs`
- Create: `plugins/ReValidation.OwnerSignatures/Runtime/OwnerTooltipProofExecutor.cs`
- Modify: `plugins/ReValidation.LocalClientStructs/Plugin.cs`
- Modify: `plugins/ReValidation.OwnerSignatures/Plugin.cs`
- Test: `plugins/tests/ReValidation.Tests/Routes/TooltipBranchValidationRouteAdapterTests.cs`

**Interfaces:**
- Consumes: `IBranchValidationRouteAdapter`, existing `ItemDetailTooltipProbe`, existing `ActionDetailTooltipProbe`, `SignatureScannerResolver`, `LocalClientStructsBuildMetadataLoader`
- Produces: route adapters that return `ProofGroupRunReport` for `CueFamily.TooltipItemDetail` and `CueFamily.TooltipActionDetail`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task LocalRoute_RunsTooltipProofGroup_EndToEnd()
{
    var group = ProofGroupFactory.CreateItemTooltipGroup();
    var adapter = new LocalBranchValidationRouteAdapter(
        new FakeLocalTooltipProofExecutor(TargetProofRecordFactory.PassedTooltip("AddonItemDetail.GenerateTooltip")),
        new FakeLocalTooltipProofExecutor(TargetProofRecordFactory.PassedTooltip("AgentItemDetail.ReceiveEvent")));

    var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

    Assert.All(report.Targets, target => Assert.Equal("passed", target.Verdict));
}

[Fact]
public async Task OwnerRoute_BlocksTooltipProofGroup_WhenSignatureResolutionIsNotUnique()
{
    var group = ProofGroupFactory.CreateActionTooltipGroup();
    var adapter = new OwnerBranchValidationRouteAdapter(
        new FakeSignatureResolutionProvider(new SignatureResolution("AddonActionDetail.GenerateTooltip", 2, null, "multiple matches")));

    var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

    Assert.Contains(report.Targets, target => target.Verdict == "blocked" && target.BlockingReason == "Signature resolution was not unique.");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~TooltipBranchValidationRouteAdapterTests"`
Expected: compile failure because the branch route adapters and tooltip proof executors do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class LocalBranchValidationRouteAdapter : IBranchValidationRouteAdapter
{
    public ValidationRoute Route => ValidationRoute.LocalClientStructs;

    public ValueTask<ProofGroupRunReport> RunProofGroupAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken) =>
        group.CueFamily switch
        {
            CueFamily.TooltipItemDetail => itemTooltipExecutor.RunAsync(group, requiredProofLevel, cancellationToken),
            CueFamily.TooltipActionDetail => actionTooltipExecutor.RunAsync(group, requiredProofLevel, cancellationToken),
            _ => ValueTask.FromResult(ProofGroupRunReport.Blocked(group.GroupId, group.Targets, "Cue family is not supported by the local route yet.")),
        };
}

public sealed class OwnerBranchValidationRouteAdapter : IBranchValidationRouteAdapter
{
    public ValidationRoute Route => ValidationRoute.OwnerSignatures;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~TooltipBranchValidationRouteAdapterTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.LocalClientStructs/Services/LocalBranchValidationRouteAdapter.cs plugins/ReValidation.OwnerSignatures/Services/OwnerBranchValidationRouteAdapter.cs plugins/ReValidation.LocalClientStructs/Runtime/LocalTooltipProofExecutor.cs plugins/ReValidation.OwnerSignatures/Runtime/OwnerTooltipProofExecutor.cs plugins/ReValidation.LocalClientStructs/Plugin.cs plugins/ReValidation.OwnerSignatures/Plugin.cs plugins/tests/ReValidation.Tests/Routes/TooltipBranchValidationRouteAdapterTests.cs
git commit -m "feat: add tooltip branch validation routes"
```

### Task 6: Implement Journal Proof Execution With Honest Level-4 Blocking

**Files:**
- Create: `plugins/ReValidation.LocalClientStructs/Runtime/LocalJournalProofExecutor.cs`
- Create: `plugins/ReValidation.OwnerSignatures/Runtime/OwnerJournalProofExecutor.cs`
- Modify: `plugins/ReValidation.LocalClientStructs/Services/LocalBranchValidationRouteAdapter.cs`
- Modify: `plugins/ReValidation.OwnerSignatures/Services/OwnerBranchValidationRouteAdapter.cs`
- Modify: `plugins/ReValidation.LocalClientStructs/Plugin.cs`
- Modify: `plugins/ReValidation.OwnerSignatures/Plugin.cs`
- Test: `plugins/tests/ReValidation.Tests/Routes/JournalBranchValidationRouteAdapterTests.cs`

**Interfaces:**
- Consumes: `Journal`, `AgentQuestJournal`, existing journal probes, existing signature resolution services
- Produces: Journal proof-group execution that closes `1/2/3` and blocks `4` with an explicit reason until a pre-UI mutation point is configured

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task LocalRoute_BlocksJournalEffectProof_WhenNoPreUiMutationPointIsConfigured()
{
    var group = ProofGroupFactory.CreateJournalGroup();
    var adapter = new LocalBranchValidationRouteAdapter(
        journalExecutor: new LocalJournalProofExecutor(effectPoint: null, mutationBlockingReason: "Journal pre-UI effect point is not configured."),
        itemTooltipExecutor: FakeExecutors.NoopTooltip(),
        actionTooltipExecutor: FakeExecutors.NoopTooltip());

    var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

    Assert.Contains(report.Targets, target => target.TargetId == "Journal.Instance" && target.Verdict == "blocked");
    Assert.Contains(report.Targets, target => target.BlockingReason == "Journal pre-UI effect point is not configured.");
}

[Fact]
public async Task OwnerRoute_StillReportsResolvedAndObservedJournalTargets_WhenEffectProofIsBlocked()
{
    var group = ProofGroupFactory.CreateJournalGroup();
    var adapter = new OwnerBranchValidationRouteAdapter(
        journalExecutor: new OwnerJournalProofExecutor(TargetProofRecordFactory.BlockedJournalEffect("Journal.GetQuestData")),
        itemTooltipExecutor: FakeExecutors.NoopTooltip(),
        actionTooltipExecutor: FakeExecutors.NoopTooltip());

    var report = await adapter.RunProofGroupAsync(group, requiredProofLevel: 4, CancellationToken.None);

    Assert.Contains(report.Targets, target => target.TargetId == "Journal.GetQuestData" && target.MatchCount == 1 && target.ObservedHitCount == 1);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~JournalBranchValidationRouteAdapterTests"`
Expected: compile failure because the Journal proof executors do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class LocalJournalProofExecutor
{
    public ValueTask<ProofGroupRunReport> RunAsync(ProofGroupDefinition group, int requiredProofLevel, CancellationToken cancellationToken)
    {
        var targets = group.Targets
            .Select(target => new TargetProofRecord(
                target.TargetId,
                verdict: "blocked",
                matchCount: 1,
                rva: 0x0,
                observedHitCount: 1,
                hookInstalled: true,
                effectApplied: false,
                effectRestored: false,
                blockingReason: "Journal pre-UI effect point is not configured."))
            .ToArray();

        return ValueTask.FromResult(new ProofGroupRunReport(group.GroupId, targets, "Journal proof blocked at effect stage."));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~JournalBranchValidationRouteAdapterTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.LocalClientStructs/Runtime/LocalJournalProofExecutor.cs plugins/ReValidation.OwnerSignatures/Runtime/OwnerJournalProofExecutor.cs plugins/ReValidation.LocalClientStructs/Services/LocalBranchValidationRouteAdapter.cs plugins/ReValidation.OwnerSignatures/Services/OwnerBranchValidationRouteAdapter.cs plugins/ReValidation.LocalClientStructs/Plugin.cs plugins/ReValidation.OwnerSignatures/Plugin.cs plugins/tests/ReValidation.Tests/Routes/JournalBranchValidationRouteAdapterTests.cs
git commit -m "feat: add journal branch validation routes"
```

### Task 7: Add Discovery-Driven UI State And Controller Workflow

**Files:**
- Modify: `plugins/ReValidation.Common/UI/ValidationWindowState.cs`
- Modify: `plugins/ReValidation.Common/UI/ValidationWindowController.cs`
- Modify: `plugins/ReValidation.LocalClientStructs/Windows/ValidationWindow.cs`
- Modify: `plugins/ReValidation.OwnerSignatures/Windows/ValidationWindow.cs`
- Test: `plugins/tests/ReValidation.Tests/UI/ValidationWindowControllerBranchValidationTests.cs`

**Interfaces:**
- Consumes: `IClientStructsDiscoveryService`, `ProofPlanBuilder`, `IBranchValidationRunner`, `IBranchValidationRouteAdapter`
- Produces: `DiscoverTargetsAsync`, `RunSelectedProofGroupAsync`, `RunAllProofGroupsAsync`, selected base ref and family state in `ValidationWindowState`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task DiscoverTargetsAsync_PopulatesGroupsAndLeavesControllerIdle()
{
    var state = new ValidationWindowState();
    var discovery = new FakeClientStructsDiscoveryService(DiscoveryCatalogFactory.SingleTooltipTarget());
    var runner = new FakeBranchValidationRunner();
    var controller = new ValidationWindowController(
        state,
        ValidationScenarioRegistry.ForTests(),
        new FakeScenarioRunner(),
        branchDiscoveryService: discovery,
        proofPlanBuilder: new ProofPlanBuilder(),
        branchValidationRunner: runner,
        routeAdapterFactory: new FakeBranchValidationRouteAdapterFactory());

    await controller.DiscoverTargetsAsync(CancellationToken.None);

    Assert.Equal("Idle", state.StatusText);
    Assert.Single(state.DiscoveredProofGroups);
    Assert.Equal("tooltip.item-detail:detour-function", state.SelectedProofGroupId);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationWindowControllerBranchValidationTests"`
Expected: compile failure because the controller does not expose branch discovery workflow yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public async Task DiscoverTargetsAsync(CancellationToken cancellationToken)
{
    var catalog = await branchDiscoveryService.DiscoverAsync(State.CreateDiscoveryOptions(), cancellationToken);
    var plan = proofPlanBuilder.Build(catalog, State.RequiredProofLevel);
    State.SetDiscoveredPlan(plan);
}

public async Task RunAllProofGroupsAsync(CancellationToken cancellationToken)
{
    State.SetRunning();
    var report = await branchValidationRunner.RunAsync(State.RequirePlan(), routeAdapterFactory.Create(State.SelectedRoute), contextFactory.CreateEvidenceRoot(), cancellationToken);
    State.SetCompleted(report.IsSuccess ? "Passed" : "Failed", report.Summary, report.ArtifactPaths);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationWindowControllerBranchValidationTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/UI/ValidationWindowState.cs plugins/ReValidation.Common/UI/ValidationWindowController.cs plugins/ReValidation.LocalClientStructs/Windows/ValidationWindow.cs plugins/ReValidation.OwnerSignatures/Windows/ValidationWindow.cs plugins/tests/ReValidation.Tests/UI/ValidationWindowControllerBranchValidationTests.cs
git commit -m "feat: add discovery-driven validation workflow"
```

### Task 8: Update Docs, Route Composition Tests, And Full Verification

**Files:**
- Modify: `plugins/docs/setup.md`
- Modify: `plugins/docs/scenarios.md`
- Modify: `plugins/docs/runtime-checklist.md`
- Modify: `plugins/tests/ReValidation.Tests/UI/RouteScenarioCompositionTests.cs`
- Modify: `plugins/tests/ReValidation.Tests/Execution/ValidationScenarioRunnerTests.cs`

**Interfaces:**
- Consumes: final discovery-driven route wiring and branch evidence outputs
- Produces: updated operator docs and regression coverage for both legacy and discovery-driven flows

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void CreateRegistry_StillRegistersLegacyScenariosWhileBranchValidationServicesExist()
{
    var registry = OwnerSignaturesScenarioComposition.CreateRegistry();

    Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "journal.completed-entries");
    Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.item-detail");
    Assert.Contains(registry.Scenarios, scenario => scenario.Definition.Id == "tooltip.action-detail");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~RouteScenarioCompositionTests|FullyQualifiedName~ValidationScenarioRunnerTests"`
Expected: at least one failing assertion or compile error because the final route composition and evidence paths are not wired consistently yet.

- [ ] **Step 3: Write minimal implementation**

```markdown
- Add a "Branch validation" section to `plugins/docs/setup.md` describing `baseRef`, discovery mode, and route selection.
- Add a "Discovery-driven proof groups" section to `plugins/docs/scenarios.md` describing `passed`, `blocked`, and `effect-not-proven`.
- Add a runtime checklist section that tells the operator to preserve the branch JSON and Markdown artifacts after `Run all in scope`.
```

- [ ] **Step 4: Run full verification**

Run: `dotnet build .\plugins\ReValidation.sln --no-restore -m:1`
Expected: PASS

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-build`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add plugins/docs/setup.md plugins/docs/scenarios.md plugins/docs/runtime-checklist.md plugins/tests/ReValidation.Tests/UI/RouteScenarioCompositionTests.cs plugins/tests/ReValidation.Tests/Execution/ValidationScenarioRunnerTests.cs
git commit -m "docs: document branch signature validation"
```
