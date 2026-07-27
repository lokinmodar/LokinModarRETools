# Tooltip Armed Validation Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an armed capture window for hover-driven tooltip validation while keeping `Journal.CompletedEntries` on the direct run path.

**Architecture:** Introduce an optional armable-scenario contract in `ReValidation.Common`, implement it in the shared tooltip scenario base by polling the tooltip probe, and let `ValidationWindowController` own armed state plus timeout handling. The two route windows expose the same arm/disarm UX, while `Journal` remains a direct execution flow because its cue is persistent.

**Tech Stack:** .NET 10, Dalamud ImGui windows, xUnit, shared `ReValidation.Common` controller/state.

## Global Constraints

- Only tooltip scenarios use armed polling.
- `Journal.CompletedEntries` keeps the direct `Run selected scenario` path.
- Do not claim a Journal mutation proof that is not actually implemented.
- Keep Windows verification sequential to avoid local build/test file-lock races.

---

### Task 1: Add The Armable Scenario Contract

**Files:**
- Create: `plugins/ReValidation.Common/Abstractions/IArmableValidationScenario.cs`
- Modify: `plugins/ReValidation.Common/Models/ScenarioStepModels.cs`
- Modify: `plugins/ReValidation.Common/Scenarios/TooltipSnapshot.cs`
- Test: `plugins/tests/ReValidation.Tests/Scenarios/TooltipScenarioTests.cs`

**Interfaces:**
- Consumes: `IValidationScenario`, `ITooltipProbe`
- Produces: `IArmableValidationScenario.ArmPrompt`, `IArmableValidationScenario.PollArmCueAsync(CancellationToken)`, `ScenarioArmState`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task ArmCuePolling_IsReady_WhenTooltipCaptureSucceeds()
{
    var scenario = new TooltipItemDetailLocalScenario(
        new FakeTooltipProbe("item", "Potion"),
        new LocalClientStructsAvailabilityDetector(propsPath: null, projectPath: null));

    var armable = Assert.IsAssignableFrom<IArmableValidationScenario>(scenario);
    var cue = await armable.PollArmCueAsync(CancellationToken.None);

    Assert.True(cue.IsReady);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~TooltipScenarioTests"`
Expected: compile failure because `IArmableValidationScenario` and `ScenarioArmState` do not exist yet.

- [x] **Step 3: Write minimal implementation**

```csharp
public interface IArmableValidationScenario
{
    string ArmPrompt { get; }
    ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken);
}

public sealed record ScenarioArmState(bool IsReady, string StatusText);
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~TooltipScenarioTests"`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/Abstractions/IArmableValidationScenario.cs plugins/ReValidation.Common/Models/ScenarioStepModels.cs plugins/ReValidation.Common/Scenarios/TooltipSnapshot.cs plugins/tests/ReValidation.Tests/Scenarios/TooltipScenarioTests.cs
git commit -m "feat: add tooltip armable scenario contract"
```

### Task 2: Add Armed State To The Shared Controller

**Files:**
- Modify: `plugins/ReValidation.Common/UI/ValidationWindowState.cs`
- Modify: `plugins/ReValidation.Common/UI/ValidationWindowController.cs`
- Test: `plugins/tests/ReValidation.Tests/UI/ValidationWindowControllerTests.cs`

**Interfaces:**
- Consumes: `IArmableValidationScenario`, `ScenarioExecutionContext Create(ValidationRoute route, ValidationMode mode)`
- Produces: `ValidationWindowController.CanArmSelectedScenario()`, `ArmSelectedScenario(TimeSpan timeout)`, `DisarmSelectedScenario()`, `PulseArmedScenarioAsync(CancellationToken)`

- [x] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task ArmSelectedScenario_ReadyCue_RunsScenarioAndPublishesArtifacts()
{
    controller.ArmSelectedScenario(TimeSpan.FromSeconds(10));
    await controller.PulseArmedScenarioAsync(CancellationToken.None);
    await controller.PulseArmedScenarioAsync(CancellationToken.None);

    Assert.Equal("Passed", controller.State.StatusText);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationWindowControllerTests"`
Expected: compile failure because the arm controller members do not exist yet.

- [x] **Step 3: Write minimal implementation**

```csharp
public bool CanArmSelectedScenario() => TryGetSelectedArmableScenario(out _);
public void ArmSelectedScenario(TimeSpan timeout) { ... }
public void DisarmSelectedScenario() { ... }
public Task PulseArmedScenarioAsync(CancellationToken cancellationToken) { ... }
```

- [x] **Step 4: Run test to verify it passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidationWindowControllerTests"`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add plugins/ReValidation.Common/UI/ValidationWindowState.cs plugins/ReValidation.Common/UI/ValidationWindowController.cs plugins/tests/ReValidation.Tests/UI/ValidationWindowControllerTests.cs
git commit -m "feat: add armed validation controller flow"
```

### Task 3: Expose Armed Tooltip UX In Both Route Windows

**Files:**
- Modify: `plugins/ReValidation.LocalClientStructs/Windows/ValidationWindow.cs`
- Modify: `plugins/ReValidation.OwnerSignatures/Windows/ValidationWindow.cs`
- Modify: `plugins/docs/scenarios.md`
- Modify: `plugins/docs/runtime-checklist.md`

**Interfaces:**
- Consumes: `ValidationWindowController.RunSelectedScenarioAsync(CancellationToken)`, `ValidationWindowController.PulseArmedScenarioAsync(CancellationToken)`, `ValidationWindowController.ArmSelectedScenario(TimeSpan timeout)`
- Produces: route-window arm/disarm controls and operator docs for tooltip execution

- [x] **Step 1: Write the failing test**

```text
Manual validation target: a hover-driven tooltip should no longer disappear when the user clicks the window to start capture.
```

- [x] **Step 2: Run test to verify it fails**

Run: in-game with the previous window behavior
Expected: hover tooltip closes before capture and evidence reports addon not visible.

- [x] **Step 3: Write minimal implementation**

```csharp
if (controller.State.IsArmed)
    _ = ObserveArmPulseAsync();

if (armableScenario is not null)
{
    if (ImGui.Button("Arm selected scenario"))
        controller.ArmSelectedScenario(TimeSpan.FromSeconds(armDurationSeconds));
}
```

- [x] **Step 4: Run test to verify it passes**

Run: arm the tooltip scenario, hover during the configured window, and confirm status changes `Armed -> Running -> Passed/Failed`.
Expected: the hover is captured without needing to click back into the plugin window at the moment of capture.

- [x] **Step 5: Commit**

```bash
git add plugins/ReValidation.LocalClientStructs/Windows/ValidationWindow.cs plugins/ReValidation.OwnerSignatures/Windows/ValidationWindow.cs plugins/docs/scenarios.md plugins/docs/runtime-checklist.md
git commit -m "feat: add armed tooltip window flow"
```
