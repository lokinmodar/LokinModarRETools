# Dynamis Bridge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `ReValidation.DynamisBridge`, an optional Journal-focused exploration plugin that uses `Dynamis` IPC to inspect and rank pre-UI runtime candidates and export local Markdown session notes.

**Architecture:** Keep the bridge fully separate from the existing proof pipeline. The new plugin owns its own IPC client, availability state, Journal session workflow, candidate ranking, and Markdown export, while the existing `ReValidation.LocalClientStructs` and `ReValidation.OwnerSignatures` projects remain the only proof routes. The first delivery is Journal-first and read-only: bounded anchors in, ranked candidates and notes out.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), Dalamud.NET.Sdk 15.0.0, Dalamud IPC, ImGui via Dalamud bindings, xUnit in the existing unified `ReValidation.Tests` project

## Global Constraints

- New optional plugin: `plugins/ReValidation.DynamisBridge/`.
- IPC-only integration with the external `Dynamis` plugin.
- Journal-focused guided exploration session.
- Pointer inspection UI for candidate objects.
- Local Markdown export of session notes and candidate rankings.
- Unit and controller-level test coverage for IPC availability and candidate prioritization.
- No direct code reuse from the `Dynamis` repository.
- No memory patching, live mutation, or UI overrides.
- No authoritative validation evidence in the `ReValidation` proof schema.
- No generic support for every addon or agent in the game.
- No automated `old exe -> new exe` binary diff workflows.
- Keep exploration separate from proof.
- Nothing in the bridge becomes a `ClientStructs` declaration or an upstream conclusion by itself.
- Integrate through public IPC only.
- No persistent detours, no memory writes, no server traffic, and no unattended runtime behavior.
- The bridge integrates through public IPC only and must not copy implementation code, heuristics, or internal assets into `LokinModarRETools`.

---

## File Map

### Solution and Project Wiring

- `plugins/ReValidation.sln`
  Add the new plugin project.
- `plugins/tests/ReValidation.Tests/ReValidation.Tests.csproj`
  Add a project reference to the new plugin so the shared test package can cover bridge code.

### Plugin Project

- `plugins/ReValidation.DynamisBridge/ReValidation.DynamisBridge.csproj`
  New Dalamud plugin project for the bridge.
- `plugins/ReValidation.DynamisBridge/ReValidation.DynamisBridge.json`
  Plugin manifest.
- `plugins/ReValidation.DynamisBridge/Plugin.cs`
  Plugin entrypoint and composition root.
- `plugins/ReValidation.DynamisBridge/Commands/PluginCommandRegistrar.cs`
  Slash command registration.
- `plugins/ReValidation.DynamisBridge/Services/PluginServices.cs`
  Dalamud `[PluginService]` accessors used by the bridge shell.

### IPC and Availability

- `plugins/ReValidation.DynamisBridge/Ipc/IDynamisIpcGateway.cs`
  Bridge-owned abstraction over the raw `Dynamis` IPC surface.
- `plugins/ReValidation.DynamisBridge/Ipc/DalamudDynamisIpcGateway.cs`
  Dalamud-backed implementation that resolves the real IPC providers and events.
- `plugins/ReValidation.DynamisBridge/Models/BridgeAvailabilityStatus.cs`
  Availability enum and snapshot record used across controller and window state.
- `plugins/ReValidation.DynamisBridge/Services/DynamisApiClient.cs`
  High-level bridge client over the IPC gateway.
- `plugins/ReValidation.DynamisBridge/Services/DynamisAvailabilityService.cs`
  Status mapping and readiness helper for the UI/controller.

### Journal Exploration Model

- `plugins/ReValidation.DynamisBridge/Models/JournalAnchorRecord.cs`
  One captured Journal anchor pointer.
- `plugins/ReValidation.DynamisBridge/Models/JournalCandidateSeed.cs`
  Raw candidate facts before ranking.
- `plugins/ReValidation.DynamisBridge/Models/JournalCandidateClassification.cs`
  Candidate bucket enum (`UIRoot`, `AgentState`, `ProviderCacheCandidate`, etc.).
- `plugins/ReValidation.DynamisBridge/Models/JournalCandidateDisposition.cs`
  User label enum (`None`, `Discarded`, `Promising`, `HighValueForIda`).
- `plugins/ReValidation.DynamisBridge/Models/JournalCandidateRecord.cs`
  Ranked candidate row shown in the UI and exported in notes.
- `plugins/ReValidation.DynamisBridge/Models/JournalProbeSession.cs`
  Session aggregate containing anchors, ranked candidates, and timestamps.

### Journal Exploration Services

- `plugins/ReValidation.DynamisBridge/Services/IJournalAnchorCollector.cs`
  Interface for collecting live Journal anchor pointers.
- `plugins/ReValidation.DynamisBridge/Services/LiveJournalAnchorCollector.cs`
  Journal anchor collector backed by live addon pointers and optional known candidates.
- `plugins/ReValidation.DynamisBridge/Services/NeighborPointerEnumerator.cs`
  Small bounded pointer-neighborhood enumerator used to derive candidate addresses from anchors.
- `plugins/ReValidation.DynamisBridge/Services/IPointerInspectionService.cs`
  Interface for class lookup, instance checks, object inspection, region inspection, and candidate expansion.
- `plugins/ReValidation.DynamisBridge/Services/PointerInspectionService.cs`
  Runtime implementation over `DynamisApiClient` and `NeighborPointerEnumerator`.
- `plugins/ReValidation.DynamisBridge/Services/JournalCandidateRanker.cs`
  Ranking and classification logic.
- `plugins/ReValidation.DynamisBridge/Services/EvidenceNoteWriter.cs`
  Markdown exporter for Journal sessions.

### UI and Session Orchestration

- `plugins/ReValidation.DynamisBridge/UI/JournalExplorerWindowState.cs`
  Mutable UI state for availability, status text, current session, and export path.
- `plugins/ReValidation.DynamisBridge/UI/JournalExplorerController.cs`
  Main Journal session workflow and action orchestration.
- `plugins/ReValidation.DynamisBridge/Windows/JournalExplorerWindow.cs`
  ImGui view for bridge status, session controls, ranked candidates, and export actions.

### Documentation

- `plugins/docs/setup.md`
  Add plugin build/setup instructions for the bridge and its optional `Dynamis` dependency.
- `plugins/docs/scenarios.md`
  Add a short “exploration-only” section so users do not confuse the bridge with the proof routes.
- `plugins/docs/runtime-checklist.md`
  Add a Journal bridge runtime checklist.
- `plugins/docs/dynamis-bridge.md`
  Add focused usage docs for the Journal exploration workflow.

### Tests

- `plugins/tests/ReValidation.Tests/Scaffolding/RepositoryLayoutTests.cs`
  Extend project-layout expectations for the new bridge project.
- `plugins/tests/ReValidation.Tests/TestInfrastructure/TemporaryDirectory.cs`
  Shared temp-directory helper for note-export tests.
- `plugins/tests/ReValidation.Tests/Bridge/DynamisApiClientTests.cs`
  Cover API version and IPC availability behavior.
- `plugins/tests/ReValidation.Tests/Bridge/DynamisAvailabilityServiceTests.cs`
  Cover readiness/status mapping.
- `plugins/tests/ReValidation.Tests/Bridge/JournalCandidateRankerTests.cs`
  Cover candidate bucketing and scoring.
- `plugins/tests/ReValidation.Tests/Bridge/EvidenceNoteWriterTests.cs`
  Cover Markdown session export.
- `plugins/tests/ReValidation.Tests/UI/JournalExplorerControllerTests.cs`
  Cover unavailable, ready, armed, export, and candidate-label flows.

### Task 1: Scaffold The DynamisBridge Project

**Files:**
- Modify: `plugins/ReValidation.sln`
- Modify: `plugins/tests/ReValidation.Tests/ReValidation.Tests.csproj`
- Modify: `plugins/tests/ReValidation.Tests/Scaffolding/RepositoryLayoutTests.cs`
- Create: `plugins/ReValidation.DynamisBridge/ReValidation.DynamisBridge.csproj`
- Create: `plugins/ReValidation.DynamisBridge/ReValidation.DynamisBridge.json`
- Create: `plugins/ReValidation.DynamisBridge/Plugin.cs`
- Create: `plugins/ReValidation.DynamisBridge/Commands/PluginCommandRegistrar.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/PluginServices.cs`
- Create: `plugins/ReValidation.DynamisBridge/Windows/JournalExplorerWindow.cs`

**Interfaces:**
- Consumes: existing `IDalamudPlugin`, `ICommandManager`, `IDalamudPluginInterface`, `WindowSystem`, and the current plugin-repo conventions visible in `ReValidation.LocalClientStructs` and `ReValidation.OwnerSignatures`
- Produces:
  - `public sealed class Plugin : IDalamudPlugin`
  - `public sealed class PluginCommandRegistrar : IDisposable`
  - `public sealed class JournalExplorerWindow : Window`

- [ ] **Step 1: Write the failing scaffold test**

```csharp
[Fact]
public void ReValidationSolutionContainsDynamisBridgeProject()
{
    var root = RepoRoot.Find();
    var pluginsRoot = Path.Combine(root, "plugins");

    Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.DynamisBridge", "ReValidation.DynamisBridge.csproj")));
    Assert.True(File.Exists(Path.Combine(pluginsRoot, "ReValidation.DynamisBridge", "ReValidation.DynamisBridge.json")));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~RepositoryLayoutTests"`
Expected: FAIL because the new bridge project files are missing.

- [ ] **Step 3: Write the minimal scaffold**

`ReValidation.DynamisBridge.csproj`:

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Use_DalamudPackager>false</Use_DalamudPackager>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="FFXIVClientStructs" HintPath="$(DalamudLibPath)FFXIVClientStructs.dll" Private="false" />
    <Reference Include="Dalamud.Bindings.ImGui" HintPath="$(DalamudLibPath)Dalamud.Bindings.ImGui.dll" Private="false" />
  </ItemGroup>

</Project>
```

`ReValidation.DynamisBridge.json`:

```json
{
  "Author": "lokinmodar",
  "Name": "ReValidation.DynamisBridge",
  "Punchline": "Explores Journal runtime objects through Dynamis IPC.",
  "Description": "Journal-focused runtime exploration bridge over Dynamis with Markdown session notes."
}
```

`Plugin.cs`:

```csharp
namespace ReValidation.DynamisBridge;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly WindowSystem windowSystem;
    private readonly JournalExplorerWindow window;
    private readonly PluginCommandRegistrar commandRegistrar;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        this.pluginInterface = pluginInterface;
        pluginInterface.Create<PluginServices>();
        windowSystem = new WindowSystem("ReValidation.DynamisBridge");
        window = new JournalExplorerWindow();
        commandRegistrar = new PluginCommandRegistrar(commandManager, window);
        windowSystem.AddWindow(window);
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
    }

    public string Name => "ReValidation Dynamis Bridge";

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        commandRegistrar.Dispose();
        windowSystem.RemoveAllWindows();
    }

    private void Draw() => windowSystem.Draw();

    private void OpenWindow() => window.IsOpen = true;
}
```

`JournalExplorerWindow.cs`:

```csharp
namespace ReValidation.DynamisBridge.Windows;

public sealed class JournalExplorerWindow : Window
{
    public JournalExplorerWindow() : base("ReValidation: Dynamis Bridge")
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Dynamis bridge scaffold loaded.");
    }
}
```

`PluginCommandRegistrar.cs`:

```csharp
namespace ReValidation.DynamisBridge.Commands;

public sealed class PluginCommandRegistrar : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly JournalExplorerWindow window;

    public PluginCommandRegistrar(ICommandManager commandManager, JournalExplorerWindow window)
    {
        this.commandManager = commandManager;
        this.window = window;
        commandManager.AddHandler("/revalidation-dynamis", new CommandInfo(_ => window.IsOpen = true)
        {
            HelpMessage = "Open the Dynamis bridge window.",
        });
    }

    public void Dispose()
    {
        commandManager.RemoveHandler("/revalidation-dynamis");
    }
}
```

`PluginServices.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public static class PluginServices
{
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
}
```

Also update `ReValidation.sln`, add the new project reference to `ReValidation.Tests.csproj`, and extend `RepositoryLayoutTests` to assert the new project and manifest exist.

- [ ] **Step 4: Run tests and build to verify the scaffold passes**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~RepositoryLayoutTests"`
Expected: PASS

Run: `dotnet build .\plugins\ReValidation.DynamisBridge\ReValidation.DynamisBridge.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add .\plugins\ReValidation.sln .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj .\plugins\tests\ReValidation.Tests\Scaffolding\RepositoryLayoutTests.cs .\plugins\ReValidation.DynamisBridge
git commit -m "feat: scaffold dynamis bridge plugin"
```

### Task 2: Add Dynamis IPC And Availability Foundation

**Files:**
- Create: `plugins/ReValidation.DynamisBridge/Models/BridgeAvailabilityStatus.cs`
- Create: `plugins/ReValidation.DynamisBridge/Ipc/IDynamisIpcGateway.cs`
- Create: `plugins/ReValidation.DynamisBridge/Ipc/DalamudDynamisIpcGateway.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/DynamisApiClient.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/DynamisAvailabilityService.cs`
- Modify: `plugins/ReValidation.DynamisBridge/Plugin.cs`
- Modify: `plugins/ReValidation.DynamisBridge/Windows/JournalExplorerWindow.cs`
- Create: `plugins/tests/ReValidation.Tests/Bridge/DynamisApiClientTests.cs`
- Create: `plugins/tests/ReValidation.Tests/Bridge/DynamisAvailabilityServiceTests.cs`

**Interfaces:**
- Consumes:
  - `public sealed class JournalExplorerWindow : Window`
  - `public sealed class Plugin : IDalamudPlugin`
- Produces:
  - `public enum BridgeAvailabilityStatus { Unavailable, Incompatible, Ready, SessionActive }`
  - `public sealed record DynamisAvailabilitySnapshot(BridgeAvailabilityStatus Status, int? ApiVersion, string StatusText);`
  - `public interface IDynamisIpcGateway : IDisposable`
  - `public interface IDynamisApiClient : IDisposable`
  - `public interface IDynamisAvailabilityService`
  - `public sealed class DynamisAvailabilityService : IDynamisAvailabilityService`

- [ ] **Step 1: Write the failing IPC tests**

```csharp
public sealed class DynamisApiClientTests
{
    [Fact]
    public void Refresh_WhenGatewayHasSupportedApiVersion_PublishesReady()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: 4);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Ready, client.Current.Status);
        Assert.Equal(4, client.Current.ApiVersion);
    }

    [Fact]
    public void Refresh_WhenGatewayHasNoApiVersion_PublishesUnavailable()
    {
        var gateway = new FakeDynamisIpcGateway(apiVersion: null);
        using var client = new DynamisApiClient(gateway, minimumApiVersion: 4);

        client.Refresh();

        Assert.Equal(BridgeAvailabilityStatus.Unavailable, client.Current.Status);
    }

    private sealed class FakeDynamisIpcGateway(int? apiVersion) : IDynamisIpcGateway
    {
        public int? TryGetApiVersion() => apiVersion;
        public IDisposable SubscribeApiInitialized(Action handler) => new NullSubscription();
        public IDisposable SubscribeApiDisposing(Action handler) => new NullSubscription();
        public bool TryInspectObject(nint address) => true;
        public bool TryInspectRegion(nint address, nuint size) => true;
        public string? TryGetClassName(nint address) => null;
        public bool TryIsInstanceOf(nint address, string className) => false;
        public bool TryDrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }

    private sealed class NullSubscription : IDisposable
    {
        public void Dispose() { }
    }
}
```

```csharp
public sealed class DynamisAvailabilityServiceTests
{
    [Fact]
    public void Snapshot_WhenClientIsIncompatible_UsesBlockedStatusText()
    {
        var client = new StubDynamisApiClient(
            new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Incompatible, 3, "Dynamis API 3 is too old."));
        var service = new DynamisAvailabilityService(client);

        Assert.False(service.IsReady);
        Assert.Equal("Dynamis API 3 is too old.", service.Current.StatusText);
    }

    private sealed class StubDynamisApiClient(DynamisAvailabilitySnapshot snapshot) : IDynamisApiClient
    {
        public event Action? AvailabilityChanged;
        public DynamisAvailabilitySnapshot Current { get; private set; } = snapshot;
        public void Refresh() => AvailabilityChanged?.Invoke();
        public bool InspectObject(nint address) => true;
        public bool InspectRegion(nint address, nuint size) => true;
        public string? GetClassName(nint address) => null;
        public bool IsInstanceOf(nint address, string className) => false;
        public bool DrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~DynamisApiClientTests|FullyQualifiedName~DynamisAvailabilityServiceTests"`
Expected: FAIL because the IPC gateway, API client, and availability types do not exist yet.

- [ ] **Step 3: Write the minimal IPC and availability implementation**

`BridgeAvailabilityStatus.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public enum BridgeAvailabilityStatus
{
    Unavailable,
    Incompatible,
    Ready,
    SessionActive,
}

public sealed record DynamisAvailabilitySnapshot(
    BridgeAvailabilityStatus Status,
    int? ApiVersion,
    string StatusText);
```

`IDynamisIpcGateway.cs`:

```csharp
namespace ReValidation.DynamisBridge.Ipc;

public interface IDynamisIpcGateway : IDisposable
{
    int? TryGetApiVersion();
    IDisposable SubscribeApiInitialized(Action handler);
    IDisposable SubscribeApiDisposing(Action handler);
    bool TryInspectObject(nint address);
    bool TryInspectRegion(nint address, nuint size);
    string? TryGetClassName(nint address);
    bool TryIsInstanceOf(nint address, string className);
    bool TryDrawPointer(string label, nint address);
}
```

`DalamudDynamisIpcGateway.cs`:

```csharp
namespace ReValidation.DynamisBridge.Ipc;

public sealed class DalamudDynamisIpcGateway : IDynamisIpcGateway
{
    private readonly IDalamudPluginInterface pluginInterface;

    public DalamudDynamisIpcGateway(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public int? TryGetApiVersion() =>
        pluginInterface.GetIpcSubscriber<int>("Dynamis.GetApiVersion").InvokeFunc();

    public IDisposable SubscribeApiInitialized(Action handler) =>
        pluginInterface.GetIpcSubscriber("Dynamis.ApiInitialized").Subscribe(handler);

    public IDisposable SubscribeApiDisposing(Action handler) =>
        pluginInterface.GetIpcSubscriber("Dynamis.ApiDisposing").Subscribe(handler);

    public bool TryInspectObject(nint address)
    {
        pluginInterface.GetIpcSubscriber<nint, object?>("Dynamis.InspectObject.V3").InvokeAction(address);
        return true;
    }

    public bool TryInspectRegion(nint address, nuint size)
    {
        pluginInterface.GetIpcSubscriber<nint, nuint, object?>("Dynamis.InspectRegion.V2").InvokeAction(address, size);
        return true;
    }

    public string? TryGetClassName(nint address) =>
        pluginInterface.GetIpcSubscriber<nint, string?>("Dynamis.GetClass.V1").InvokeFunc(address);

    public bool TryIsInstanceOf(nint address, string className) =>
        pluginInterface.GetIpcSubscriber<nint, string, bool>("Dynamis.IsInstanceOf.V1").InvokeFunc(address, className);

    public bool TryDrawPointer(string label, nint address)
    {
        pluginInterface.GetIpcSubscriber<string, nint, bool>("Dynamis.ImGuiDrawPointer.V4").InvokeFunc(label, address);
        return true;
    }

    public void Dispose()
    {
    }
}
```

`DynamisApiClient.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public interface IDynamisApiClient : IDisposable
{
    event Action? AvailabilityChanged;
    DynamisAvailabilitySnapshot Current { get; }
    void Refresh();
    bool InspectObject(nint address);
    bool InspectRegion(nint address, nuint size);
    string? GetClassName(nint address);
    bool IsInstanceOf(nint address, string className);
    bool DrawPointer(string label, nint address);
}

public interface IDynamisAvailabilityService
{
    DynamisAvailabilitySnapshot Current { get; }
    bool IsReady { get; }
}

public sealed class DynamisApiClient : IDynamisApiClient
{
    private readonly IDynamisIpcGateway gateway;
    private readonly int minimumApiVersion;
    private readonly IDisposable initializedSubscription;
    private readonly IDisposable disposingSubscription;

    public DynamisApiClient(IDynamisIpcGateway gateway, int minimumApiVersion)
    {
        this.gateway = gateway;
        this.minimumApiVersion = minimumApiVersion;
        initializedSubscription = gateway.SubscribeApiInitialized(Refresh);
        disposingSubscription = gateway.SubscribeApiDisposing(Refresh);
        Current = new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.");
    }

    public event Action? AvailabilityChanged;
    public DynamisAvailabilitySnapshot Current { get; private set; }

    public void Refresh()
    {
        var version = gateway.TryGetApiVersion();
        Current = version switch
        {
            null => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable."),
            var resolved when resolved < minimumApiVersion => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Incompatible, version, $"Dynamis API {version} is too old."),
            _ => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, version, $"Dynamis API {version} is ready."),
        };
        AvailabilityChanged?.Invoke();
    }

    public bool InspectObject(nint address) => gateway.TryInspectObject(address);
    public bool InspectRegion(nint address, nuint size) => gateway.TryInspectRegion(address, size);
    public string? GetClassName(nint address) => gateway.TryGetClassName(address);
    public bool IsInstanceOf(nint address, string className) => gateway.TryIsInstanceOf(address, className);
    public bool DrawPointer(string label, nint address) => gateway.TryDrawPointer(label, address);

    public void Dispose()
    {
        initializedSubscription.Dispose();
        disposingSubscription.Dispose();
        gateway.Dispose();
    }
}
```

`DynamisAvailabilityService.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class DynamisAvailabilityService(IDynamisApiClient apiClient) : IDynamisAvailabilityService
{
    public DynamisAvailabilitySnapshot Current => apiClient.Current;

    public bool IsReady =>
        apiClient.Current.Status is BridgeAvailabilityStatus.Ready or BridgeAvailabilityStatus.SessionActive;
}
```

Update `Plugin.cs` to compose `DalamudDynamisIpcGateway`, `DynamisApiClient`, and `DynamisAvailabilityService`, and update `JournalExplorerWindow.cs` to display `Current.StatusText` instead of the scaffold-only message.

- [ ] **Step 4: Run focused tests and build**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~DynamisApiClientTests|FullyQualifiedName~DynamisAvailabilityServiceTests"`
Expected: PASS

Run: `dotnet build .\plugins\ReValidation.DynamisBridge\ReValidation.DynamisBridge.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add .\plugins\ReValidation.DynamisBridge .\plugins\tests\ReValidation.Tests\Bridge
git commit -m "feat: add dynamis ipc availability foundation"
```

### Task 3: Add Journal Candidate Ranking And Markdown Note Export

**Files:**
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalAnchorRecord.cs`
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalCandidateSeed.cs`
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalCandidateClassification.cs`
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalCandidateDisposition.cs`
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalCandidateRecord.cs`
- Create: `plugins/ReValidation.DynamisBridge/Models/JournalProbeSession.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/JournalCandidateRanker.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/EvidenceNoteWriter.cs`
- Create: `plugins/tests/ReValidation.Tests/TestInfrastructure/TemporaryDirectory.cs`
- Create: `plugins/tests/ReValidation.Tests/Bridge/JournalCandidateRankerTests.cs`
- Create: `plugins/tests/ReValidation.Tests/Bridge/EvidenceNoteWriterTests.cs`

**Interfaces:**
- Consumes:
  - `public sealed record DynamisAvailabilitySnapshot(...)`
  - `public sealed class DynamisAvailabilityService`
- Produces:
  - `public sealed record JournalAnchorRecord(string AnchorId, nint Address, string Source, string Role);`
  - `public sealed record JournalCandidateSeed(...)`
  - `public sealed record JournalCandidateRecord(...)`
  - `public sealed record JournalProbeSession(...)`
  - `public sealed class JournalCandidateRanker`
  - `public sealed class EvidenceNoteWriter`

- [ ] **Step 1: Write the failing ranking and export tests**

```csharp
public sealed class JournalCandidateRankerTests
{
    [Fact]
    public void Rank_PrioritizesProviderLikeObjectsAboveLeafTextNodes()
    {
        var ranker = new JournalCandidateRanker();
        var ranked = ranker.Rank(
            [
                new JournalCandidateSeed("provider", (nint)0x1000, "addon", "candidate", "SomeProvider", "size=0x80", null, false, 4),
                new JournalCandidateSeed("text-node", (nint)0x2000, "addon", "candidate", "AtkTextNode", "size=0x30", "The Company You Keep", true, 0),
            ]);

        Assert.Equal("provider", ranked[0].CandidateId);
        Assert.Equal(JournalCandidateClassification.ProviderCacheCandidate, ranked[0].Classification);
        Assert.Equal(JournalCandidateClassification.StringBearingCandidate, ranked[1].Classification);
    }
}
```

```csharp
public sealed class EvidenceNoteWriterTests
{
    [Fact]
    public async Task WriteAsync_ExportsPromisingCandidatesAndNextSteps()
    {
        var session = new JournalProbeSession(
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero),
            "ReValidation.DynamisBridge/0.1.0",
            4,
            "ffxiv_dx11.exe sha256=example",
            [
                new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root"),
            ],
            [
                new JournalCandidateRecord("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", JournalCandidateClassification.ProviderCacheCandidate, 95, JournalCandidateDisposition.HighValueForIda, "Likely pre-UI container"),
            ]);

        using var temp = new TemporaryDirectory();
        var writer = new EvidenceNoteWriter(TimeProvider.System);

        var outputPath = await writer.WriteAsync(session, temp.Path, CancellationToken.None);
        var markdown = File.ReadAllText(outputPath);

        Assert.Contains("provider", markdown, StringComparison.Ordinal);
        Assert.Contains("HighValueForIda", markdown, StringComparison.Ordinal);
        Assert.Contains("Likely pre-UI container", markdown, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~JournalCandidateRankerTests|FullyQualifiedName~EvidenceNoteWriterTests"`
Expected: FAIL because the Journal candidate model, ranker, and note writer do not exist yet.

- [ ] **Step 3: Write the minimal ranking and export implementation**

`JournalAnchorRecord.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public sealed record JournalAnchorRecord(
    string AnchorId,
    nint Address,
    string Source,
    string Role);
```

`JournalCandidateClassification.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public enum JournalCandidateClassification
{
    Unknown,
    UIRoot,
    AgentState,
    ProviderCacheCandidate,
    EntryArrayCandidate,
    StringBearingCandidate,
}
```

`JournalCandidateDisposition.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public enum JournalCandidateDisposition
{
    None,
    Discarded,
    Promising,
    HighValueForIda,
}
```

`JournalCandidateSeed.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public sealed record JournalCandidateSeed(
    string CandidateId,
    nint Address,
    string AnchorId,
    string LogicalRoleGuess,
    string? ClassName,
    string? RegionSummary,
    string? NearbyStringSample,
    bool LooksLikeLeafTextNode,
    int ChildPointerCount);
```

`JournalCandidateRecord.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public sealed record JournalCandidateRecord(
    string CandidateId,
    nint Address,
    string AnchorId,
    string LogicalRoleGuess,
    string? ClassName,
    string? RegionSummary,
    JournalCandidateClassification Classification,
    int Confidence,
    JournalCandidateDisposition Disposition,
    string Notes);
```

`JournalProbeSession.cs`:

```csharp
namespace ReValidation.DynamisBridge.Models;

public sealed record JournalProbeSession(
    DateTimeOffset StartedAtUtc,
    string PluginVersion,
    int? DynamisApiVersion,
    string? ExecutableIdentity,
    IReadOnlyList<JournalAnchorRecord> Anchors,
    IReadOnlyList<JournalCandidateRecord> Candidates);
```

`JournalCandidateRanker.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class JournalCandidateRanker
{
    public IReadOnlyList<JournalCandidateRecord> Rank(IReadOnlyList<JournalCandidateSeed> seeds) =>
        seeds
            .Select(seed => new JournalCandidateRecord(
                seed.CandidateId,
                seed.Address,
                seed.AnchorId,
                seed.LogicalRoleGuess,
                seed.ClassName,
                seed.RegionSummary,
                Classify(seed),
                Score(seed),
                JournalCandidateDisposition.None,
                BuildNotes(seed)))
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();

    private static JournalCandidateClassification Classify(JournalCandidateSeed seed)
    {
        if (seed.ClassName?.Contains("TextNode", StringComparison.OrdinalIgnoreCase) == true)
            return JournalCandidateClassification.StringBearingCandidate;
        if (seed.ClassName?.Contains("Agent", StringComparison.OrdinalIgnoreCase) == true)
            return JournalCandidateClassification.AgentState;
        if (seed.ChildPointerCount >= 3)
            return JournalCandidateClassification.ProviderCacheCandidate;
        if (seed.NearbyStringSample is { Length: > 0 })
            return JournalCandidateClassification.StringBearingCandidate;
        return JournalCandidateClassification.Unknown;
    }

    private static int Score(JournalCandidateSeed seed)
    {
        var score = 10;
        if (!seed.LooksLikeLeafTextNode) score += 20;
        if (seed.ChildPointerCount >= 3) score += 40;
        if (!string.IsNullOrWhiteSpace(seed.ClassName)) score += 20;
        if (!string.IsNullOrWhiteSpace(seed.NearbyStringSample)) score += 10;
        return score;
    }

    private static string BuildNotes(JournalCandidateSeed seed) =>
        seed.LooksLikeLeafTextNode
            ? "Looks like a final UI leaf."
            : $"Derived from anchor '{seed.AnchorId}' with {seed.ChildPointerCount} child pointer candidates.";
}
```

`EvidenceNoteWriter.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class EvidenceNoteWriter(TimeProvider timeProvider)
{
    public async Task<string> WriteAsync(JournalProbeSession session, string outputRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, $"journal-session-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.md");
        var lines = new List<string>
        {
            "# Journal Session Note",
            string.Empty,
            $"Started: {session.StartedAtUtc:O}",
            $"Plugin: {session.PluginVersion}",
            $"Dynamis API: {session.DynamisApiVersion?.ToString() ?? "unknown"}",
            $"Executable: {session.ExecutableIdentity ?? "unknown"}",
            string.Empty,
            "## Anchors",
        };

        lines.AddRange(session.Anchors.Select(anchor => $"- `{anchor.AnchorId}` `{anchor.Address:X}` {anchor.Role}"));
        lines.Add(string.Empty);
        lines.Add("## Candidates");
        lines.AddRange(session.Candidates.Select(candidate =>
            $"- `{candidate.CandidateId}` `{candidate.Address:X}` {candidate.Classification} {candidate.Disposition}: {candidate.Notes}"));

        await File.WriteAllLinesAsync(path, lines, cancellationToken);
        return path;
    }
}
```

`TemporaryDirectory.cs`:

```csharp
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"revalidation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~JournalCandidateRankerTests|FullyQualifiedName~EvidenceNoteWriterTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add .\plugins\ReValidation.DynamisBridge .\plugins\tests\ReValidation.Tests\Bridge .\plugins\tests\ReValidation.Tests\TestInfrastructure\TemporaryDirectory.cs
git commit -m "feat: add journal candidate ranking and note export"
```

### Task 4: Add The Journal Session Workflow, Window, And Runtime Docs

**Files:**
- Create: `plugins/ReValidation.DynamisBridge/Services/IJournalAnchorCollector.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/LiveJournalAnchorCollector.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/NeighborPointerEnumerator.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/IPointerInspectionService.cs`
- Create: `plugins/ReValidation.DynamisBridge/Services/PointerInspectionService.cs`
- Create: `plugins/ReValidation.DynamisBridge/UI/JournalExplorerWindowState.cs`
- Create: `plugins/ReValidation.DynamisBridge/UI/JournalExplorerController.cs`
- Modify: `plugins/ReValidation.DynamisBridge/Plugin.cs`
- Modify: `plugins/ReValidation.DynamisBridge/Windows/JournalExplorerWindow.cs`
- Modify: `plugins/docs/setup.md`
- Modify: `plugins/docs/scenarios.md`
- Modify: `plugins/docs/runtime-checklist.md`
- Create: `plugins/docs/dynamis-bridge.md`
- Create: `plugins/tests/ReValidation.Tests/UI/JournalExplorerControllerTests.cs`

**Interfaces:**
- Consumes:
  - `public interface IDynamisApiClient : IDisposable`
  - `public interface IDynamisAvailabilityService`
  - `public sealed class JournalCandidateRanker`
  - `public sealed class EvidenceNoteWriter`
  - `public sealed record JournalProbeSession(...)`
- Produces:
  - `public interface IJournalAnchorCollector`
  - `public interface IPointerInspectionService`
  - `public sealed class JournalExplorerWindowState`
  - `public sealed class JournalExplorerController`

- [ ] **Step 1: Write the failing controller tests**

```csharp
public sealed class JournalExplorerControllerTests
{
    [Fact]
    public async Task ArmJournalSessionAsync_WhenDynamisIsUnavailable_PublishesBlockedStatus()
    {
        var state = new JournalExplorerWindowState();
        var controller = new JournalExplorerController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.")),
            new FakeJournalAnchorCollector(),
            new FakePointerInspectionService(),
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => "ffxiv_dx11.exe sha256=example");

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Blocked", state.StatusText);
        Assert.Equal("Dynamis is unavailable.", state.StatusDetailText);
    }

    [Fact]
    public async Task ArmJournalSessionAsync_WhenReady_PopulatesRankedCandidates()
    {
        var state = new JournalExplorerWindowState();
        var controller = new JournalExplorerController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, 4, "Dynamis API 4 is ready.")),
            new FakeJournalAnchorCollector(
                new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(
                new JournalCandidateSeed("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", null, false, 4)),
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => "ffxiv_dx11.exe sha256=example");

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Armed", state.StatusText);
        Assert.Single(state.Candidates);
        Assert.Equal("provider", state.Candidates[0].CandidateId);
    }

    [Fact]
    public async Task MarkCandidateDisposition_UpdatesTheSelectedCandidate()
    {
        var state = new JournalExplorerWindowState();
        var controller = new JournalExplorerController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, 4, "Dynamis API 4 is ready.")),
            new FakeJournalAnchorCollector(
                new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(
                new JournalCandidateSeed("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", null, false, 4)),
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => "ffxiv_dx11.exe sha256=example");

        await controller.ArmJournalSessionAsync(CancellationToken.None);
        controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.HighValueForIda);

        Assert.Equal(JournalCandidateDisposition.HighValueForIda, Assert.Single(state.Candidates).Disposition);
    }

    private sealed class FakeAvailabilityService(DynamisAvailabilitySnapshot snapshot) : IDynamisAvailabilityService
    {
        public DynamisAvailabilitySnapshot Current => snapshot;
        public bool IsReady => snapshot.Status is BridgeAvailabilityStatus.Ready or BridgeAvailabilityStatus.SessionActive;
    }

    private sealed class FakeJournalAnchorCollector(params JournalAnchorRecord[] anchors) : IJournalAnchorCollector
    {
        public IReadOnlyList<JournalAnchorRecord> CaptureAnchors() => anchors;
    }

    private sealed class FakePointerInspectionService(params JournalCandidateSeed[] seeds) : IPointerInspectionService
    {
        public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors) => seeds;
        public bool InspectObject(nint address) => true;
        public bool InspectRegion(nint address, nuint size) => true;
        public bool DrawPointer(string label, nint address) => true;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~JournalExplorerControllerTests"`
Expected: FAIL because the controller, state model, anchor collector, and pointer inspection service do not exist yet.

- [ ] **Step 3: Write the Journal session and UI implementation**

`IJournalAnchorCollector.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public interface IJournalAnchorCollector
{
    IReadOnlyList<JournalAnchorRecord> CaptureAnchors();
}
```

`NeighborPointerEnumerator.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class NeighborPointerEnumerator
{
    public IReadOnlyList<nint> Enumerate(nint baseAddress, int pointerSlots)
    {
        if (baseAddress == 0 || pointerSlots <= 0)
            return Array.Empty<nint>();

        var pointers = new List<nint>();
        for (var slot = 0; slot < pointerSlots; slot++)
        {
            var pointer = Marshal.ReadIntPtr(baseAddress, slot * IntPtr.Size);
            if (pointer != 0)
                pointers.Add(pointer);
        }

        return pointers.Distinct().ToArray();
    }
}
```

`IPointerInspectionService.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public interface IPointerInspectionService
{
    IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors);
    bool InspectObject(nint address);
    bool InspectRegion(nint address, nuint size);
    bool DrawPointer(string label, nint address);
}
```

`JournalExplorerWindowState.cs`:

```csharp
namespace ReValidation.DynamisBridge.UI;

public sealed class JournalExplorerWindowState
{
    public string StatusText { get; private set; } = "Idle";
    public string StatusDetailText { get; private set; } = string.Empty;
    public IReadOnlyList<JournalAnchorRecord> Anchors { get; private set; } = Array.Empty<JournalAnchorRecord>();
    public IReadOnlyList<JournalCandidateRecord> Candidates { get; private set; } = Array.Empty<JournalCandidateRecord>();
    public string? SelectedCandidateId { get; private set; }
    public string? LastExportPath { get; private set; }

    public void SetBlocked(string detail)
    {
        StatusText = "Blocked";
        StatusDetailText = detail;
        Anchors = Array.Empty<JournalAnchorRecord>();
        Candidates = Array.Empty<JournalCandidateRecord>();
    }

    public void SetSession(IReadOnlyList<JournalAnchorRecord> anchors, IReadOnlyList<JournalCandidateRecord> candidates)
    {
        StatusText = "Armed";
        StatusDetailText = "Journal session captured.";
        Anchors = anchors;
        Candidates = candidates;
        SelectedCandidateId = candidates.FirstOrDefault()?.CandidateId;
    }

    public void SetExport(string path)
    {
        LastExportPath = path;
        StatusText = "Exported";
        StatusDetailText = path;
    }

    public void SetSelectedCandidate(string candidateId)
    {
        SelectedCandidateId = candidateId;
    }

    public void SetCandidateDisposition(JournalCandidateDisposition disposition)
    {
        if (SelectedCandidateId is null)
            return;

        Candidates = Candidates
            .Select(candidate => candidate.CandidateId == SelectedCandidateId ? candidate with { Disposition = disposition } : candidate)
            .ToArray();
    }
}
```

`JournalExplorerController.cs`:

```csharp
namespace ReValidation.DynamisBridge.UI;

public sealed class JournalExplorerController
{
    private readonly JournalExplorerWindowState state;
    private readonly IDynamisAvailabilityService availabilityService;
    private readonly IJournalAnchorCollector anchorCollector;
    private readonly IPointerInspectionService inspectionService;
    private readonly JournalCandidateRanker ranker;
    private readonly EvidenceNoteWriter noteWriter;
    private readonly string pluginVersion;
    private readonly Func<string?> executableIdentityProvider;
    private readonly TimeProvider timeProvider;

    public JournalExplorerController(
        JournalExplorerWindowState state,
        IDynamisAvailabilityService availabilityService,
        IJournalAnchorCollector anchorCollector,
        IPointerInspectionService inspectionService,
        JournalCandidateRanker ranker,
        EvidenceNoteWriter noteWriter,
        string pluginVersion,
        Func<string?> executableIdentityProvider,
        TimeProvider? timeProvider = null)
    {
        this.state = state;
        this.availabilityService = availabilityService;
        this.anchorCollector = anchorCollector;
        this.inspectionService = inspectionService;
        this.ranker = ranker;
        this.noteWriter = noteWriter;
        this.pluginVersion = pluginVersion;
        this.executableIdentityProvider = executableIdentityProvider;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public JournalExplorerWindowState State => state;

    public Task ArmJournalSessionAsync(CancellationToken cancellationToken)
    {
        if (!availabilityService.IsReady)
        {
            state.SetBlocked(availabilityService.Current.StatusText);
            return Task.CompletedTask;
        }

        var anchors = anchorCollector.CaptureAnchors();
        var seeds = inspectionService.ExpandCandidates(anchors);
        var candidates = ranker.Rank(seeds);
        state.SetSession(anchors, candidates);
        return Task.CompletedTask;
    }

    public async Task ExportSessionNoteAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var session = new JournalProbeSession(
            timeProvider.GetUtcNow(),
            pluginVersion,
            availabilityService.Current.ApiVersion,
            executableIdentityProvider(),
            state.Anchors,
            state.Candidates);
        var path = await noteWriter.WriteAsync(session, outputRoot, cancellationToken);
        state.SetExport(path);
    }

    public void MarkSelectedCandidateDisposition(JournalCandidateDisposition disposition) =>
        state.SetCandidateDisposition(disposition);

    public bool InspectSelectedCandidateObject()
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.InspectObject(candidate.Address);
    }

    public bool InspectSelectedCandidateRegion(nuint size)
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.InspectRegion(candidate.Address, size);
    }
}
```

`LiveJournalAnchorCollector.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class LiveJournalAnchorCollector(
    Func<nint> getAddonAddress,
    Func<IReadOnlyList<nint>> getKnownCandidatePointers) : IJournalAnchorCollector
{
    public IReadOnlyList<JournalAnchorRecord> CaptureAnchors()
    {
        var anchors = new List<JournalAnchorRecord>();
        var addonAddress = getAddonAddress();
        if (addonAddress != 0)
            anchors.Add(new JournalAnchorRecord("journal-addon", addonAddress, "GameGui", "UI root"));

        foreach (var pointer in getKnownCandidatePointers())
            if (pointer != 0)
                anchors.Add(new JournalAnchorRecord($"known-{pointer:X}", pointer, "KnownCandidate", "Known pointer"));

        return anchors;
    }
}
```

`PointerInspectionService.cs`:

```csharp
namespace ReValidation.DynamisBridge.Services;

public sealed class PointerInspectionService(
    IDynamisApiClient apiClient,
    NeighborPointerEnumerator neighborPointerEnumerator) : IPointerInspectionService
{
    public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors)
    {
        var seeds = new List<JournalCandidateSeed>();
        foreach (var anchor in anchors)
        {
            seeds.Add(CreateSeed(anchor.AnchorId, anchor.Address, anchor.Role));
            foreach (var neighbor in neighborPointerEnumerator.Enumerate(anchor.Address, pointerSlots: 8))
                seeds.Add(CreateSeed(anchor.AnchorId, neighbor, "neighbor"));
        }

        return seeds
            .GroupBy(seed => seed.Address)
            .Select(group => group.First())
            .ToArray();
    }

    public bool InspectObject(nint address) => apiClient.InspectObject(address);
    public bool InspectRegion(nint address, nuint size) => apiClient.InspectRegion(address, size);
    public bool DrawPointer(string label, nint address) => apiClient.DrawPointer(label, address);

    private JournalCandidateSeed CreateSeed(string anchorId, nint address, string role)
    {
        var className = apiClient.GetClassName(address);
        var looksLikeLeafTextNode = className?.Contains("TextNode", StringComparison.OrdinalIgnoreCase) == true;
        var childPointers = neighborPointerEnumerator.Enumerate(address, pointerSlots: 4).Count;
        return new JournalCandidateSeed(
            $"candidate-{address:X}",
            address,
            anchorId,
            role,
            className,
            $"neighbors={childPointers}",
            null,
            looksLikeLeafTextNode,
            childPointers);
    }
}
```

Update `JournalExplorerWindow.cs` to accept the controller and export root explicitly:

```csharp
public sealed class JournalExplorerWindow : Window
{
    private readonly JournalExplorerController controller;
    private readonly string exportRoot;

    public JournalExplorerWindow(JournalExplorerController controller, string exportRoot)
        : base("ReValidation: Dynamis Bridge")
    {
        this.controller = controller;
        this.exportRoot = exportRoot;
    }
}
```

Update `JournalExplorerWindow.cs` to render:

```csharp
ImGui.TextUnformatted($"Status: {controller.State.StatusText}");
if (ImGui.Button("Arm Journal Session"))
    _ = controller.ArmJournalSessionAsync(CancellationToken.None);
if (ImGui.Button("Inspect Object"))
    controller.InspectSelectedCandidateObject();
if (ImGui.Button("Mark High Value For IDA"))
    controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.HighValueForIda);
if (ImGui.Button("Export Session Note"))
    _ = controller.ExportSessionNoteAsync(exportRoot, CancellationToken.None);
foreach (var candidate in controller.State.Candidates)
    ImGui.TextUnformatted($"{candidate.CandidateId} {candidate.Classification} {candidate.Confidence}");
```

`Plugin.cs` should compose the live services:

```csharp
var gateway = new DalamudDynamisIpcGateway(pluginInterface);
var apiClient = new DynamisApiClient(gateway, minimumApiVersion: 4);
apiClient.Refresh();
var availabilityService = new DynamisAvailabilityService(apiClient);
var exportRoot = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "dynamis-bridge");
var anchorCollector = new LiveJournalAnchorCollector(
    () => PluginServices.GameGui.GetAddonByName("Journal", 1).Address,
    () => Array.Empty<nint>());
var inspectionService = new PointerInspectionService(apiClient, new NeighborPointerEnumerator());
var controller = new JournalExplorerController(
    new JournalExplorerWindowState(),
    availabilityService,
    anchorCollector,
    inspectionService,
    new JournalCandidateRanker(),
    new EvidenceNoteWriter(TimeProvider.System),
    "ReValidation.DynamisBridge/0.1.0",
    () => Environment.ProcessPath);
var window = new JournalExplorerWindow(controller, exportRoot);
```

Add the runtime docs:

- `plugins/docs/setup.md`: install bridge, install/enable `Dynamis`, open plugin config, verify the status line reaches `Ready`.
- `plugins/docs/scenarios.md`: add a short “Journal exploration via Dynamis bridge” section marked as exploration-only.
- `plugins/docs/runtime-checklist.md`: add a Journal session checklist with `Open Journal -> Arm Journal Session -> inspect promising candidates -> export note`.
- `plugins/docs/dynamis-bridge.md`: add focused usage docs, status meanings, and note-export location.

- [ ] **Step 4: Run focused tests, then the full suite, then the plugin build**

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj --filter "FullyQualifiedName~JournalExplorerControllerTests"`
Expected: PASS

Run: `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj`
Expected: PASS

Run: `dotnet build .\plugins\ReValidation.DynamisBridge\ReValidation.DynamisBridge.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add .\plugins\ReValidation.DynamisBridge .\plugins\docs\setup.md .\plugins\docs\scenarios.md .\plugins\docs\runtime-checklist.md .\plugins\docs\dynamis-bridge.md .\plugins\tests\ReValidation.Tests
git commit -m "feat: add journal dynamis exploration workflow"
```

## Self-Review

### Spec Coverage

- Optional plugin project: covered by Task 1.
- IPC-only integration with `Dynamis`: covered by Task 2.
- Journal-focused guided exploration session: covered by Tasks 3 and 4.
- Pointer inspection UI: covered by Task 4.
- Local Markdown export: covered by Task 3 and surfaced in Task 4.
- Unit and controller-level tests: covered by Tasks 1 through 4.
- Proof separation and no authoritative evidence coupling: enforced by Global Constraints and preserved by the plugin-local architecture in Tasks 2 through 4.
- No direct code reuse or runtime mutation: enforced by Global Constraints and by the service boundaries in Tasks 2 through 4.
- Future `old exe -> new exe` diff support is intentionally left out: preserved by scope and no task introduces it.

No spec requirement is left without a task.

### Placeholder Scan

- No `TBD`, `TODO`, or “implement later” markers remain.
- Every task includes concrete files, test names, commands, and code blocks.
- Later tasks only consume interfaces defined in earlier tasks within this plan.

### Type Consistency

- `BridgeAvailabilityStatus`, `DynamisAvailabilitySnapshot`, `IDynamisApiClient`, `JournalCandidateSeed`, `JournalCandidateRecord`, `JournalProbeSession`, and `JournalExplorerController` use the same names across all tasks.
- The controller in Task 4 consumes the ranker and note writer produced in Task 3 and the API client/availability service produced in Task 2.
