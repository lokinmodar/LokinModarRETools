# ReValidation Plugins Design

Date: 2026-07-27

## Goal

Add a versioned, reproducible validation package to `LokinModarRETools` that proves reverse-engineering findings in a live Dalamud environment through two complementary routes:

1. `LocalClientStructs`
   Proves that a specific local `FFXIVClientStructs` worktree/build behaves correctly in game.
2. `OwnerSignatures`
   Proves the same target behavior through plugin-owned signatures, hooks, and wrappers while consuming the `FFXIVClientStructs` already provided by Dalamud for baseline types.

The package must keep proof artifacts locally, support controlled live overrides for fail-proof validation, and remain reusable for future RE targets beyond the initial Journal and Tooltip scenarios.

## Scope

Initial scenarios:

- `Journal.CompletedEntries`
- `Tooltip.ItemDetail`
- `Tooltip.ActionDetail`

Initial capabilities per scenario:

- Capture pre-UI and UI-visible state
- Compare `LocalClientStructs` and `OwnerSignatures` routes where both apply
- Apply a controlled live override
- Assert behavior change and restore behavior
- Export reproducible evidence as both machine-readable JSON and a human-readable Markdown report

Out of scope for the first delivery:

- Gameplay automation
- Broad unattended tracing
- Persistent always-on detours outside explicit scenario execution
- Generic support for every existing LokinModarRETools script target

## Repository Layout

The package will live inside `LokinModarRETools` as a separate plugin solution modeled after `goatcorp/SamplePlugin`.

```text
plugins/
  ReValidation.sln
  ReValidation.Common/
  ReValidation.LocalClientStructs/
  ReValidation.OwnerSignatures/
  tests/
    ReValidation.Tests/
  docs/
    setup.md
    scenarios.md
  local/
    LocalClientStructs.props.example
  artifacts/
    evidence/
```

Notes:

- `plugins/local/` contains local-only configuration inputs. Real machine-specific files stay untracked.
- `plugins/artifacts/evidence/` contains local validation outputs and must remain gitignored because the files include machine and session-specific proof artifacts.

## Project Responsibilities

### ReValidation.Common

Shared harness code used by both plugins.

Responsibilities:

- Scenario registration and discovery
- Execution pipeline
- Shared data models and snapshot normalization
- Compare, assert, override, restore, and export orchestration
- Common UI, commands, and execution state
- Shared abstractions for signatures, hooks, and evidence writing

Core interfaces:

- `IValidationScenario`
- `ICaptureStep`
- `ICompareStep`
- `IOverrideStep`
- `IAssertStep`
- `IRestoreStep`
- `IExportStep`

### ReValidation.LocalClientStructs

Plugin that validates a specific local `FFXIVClientStructs` worktree/build.

Responsibilities:

- Use a real local `ProjectReference` to the configured `FFXIVClientStructs.csproj`
- Consume newly mapped structs and methods directly from that build
- Record the exact ClientStructs branch, commit, dirty state, and loaded assembly hash in each proof artifact

This plugin is the answer to "does this exact ClientStructs branch/worktree actually work in game?"

### ReValidation.OwnerSignatures

Plugin that validates the same targets through plugin-owned RE logic.

Responsibilities:

- Use Dalamud-provided `FFXIVClientStructs` only for baseline shared types
- Own the new signatures, detours, and wrappers for findings that have not yet landed upstream or that must be independently verified
- Prove that the plugin-owned route reaches the same behavior and data as the local-ClientStructs route

This plugin is the answer to "can we independently prove and operate the same path without depending on the new ClientStructs mapping?"

### ReValidation.Tests

Single test project covering both pure harness logic and DalaMock-backed plugin behavior.

Responsibilities:

- Scenario registry tests
- Snapshot normalization and diff tests
- Evidence serialization tests
- Restore/cleanup flow tests
- Command and UI state tests
- Local-route gating tests
- DalaMock coverage for plugin shell behavior where possible

## Build and Dependency Model

### LocalClientStructs Route

`ReValidation.LocalClientStructs` must use a real local `ProjectReference`, not a stale DLL-based authoritative path.

Local configuration file:

- `plugins/local/LocalClientStructs.props` (untracked)

Example contents:

```xml
<Project>
  <PropertyGroup>
    <ClientStructsProjectPath>C:\Dante\_dalamud\FFXIVClientStructs-journal-list-provider\FFXIVClientStructs\FFXIVClientStructs.csproj</ClientStructsProjectPath>
  </PropertyGroup>
</Project>
```

Behavior:

- `full proof` is blocked if the local props file is missing
- `full proof` is blocked if the referenced project cannot be resolved
- the plugin must record the local repo branch, commit, dirty state, and loaded assembly hash before starting the scenario

Fallback by direct DLL path may exist only as a development convenience mode. It is never accepted as the authoritative route for `full proof`.

### OwnerSignatures Route

`ReValidation.OwnerSignatures` builds without any dependency on the user's local ClientStructs fork.

Behavior:

- Scenario execution is blocked if a required signature resolves to zero matches
- Scenario execution is blocked if a required signature resolves to multiple matches when the scenario requires uniqueness
- Resolved RVAs and signature metadata are captured into the evidence output

## Scenario Model

Each RE target is represented as a versioned scenario with the same pipeline:

1. `capture`
2. `compare`
3. `override`
4. `assert`
5. `restore`
6. `export`

### Scenario Contract

Each scenario declares:

- Human name
- Stable scenario id
- Supported route(s)
- Required signatures and hooks
- Required ClientStructs members, if any
- Trigger description
- Capture schema
- Override strategy
- Restore strategy
- Assert criteria
- Evidence schema additions

### Execution Modes

From the plugin UI:

- `capture only`
- `compare`
- `override + assert`
- `full proof`

`full proof` runs the full pipeline and produces the authoritative evidence artifact.

### Failure Handling

If any scenario step fails:

- The scenario status becomes `failed`
- The plugin attempts immediate restore in `finally`
- All hooks and detours for that scenario are torn down
- The export marks the exact failed phase and restore result

## Initial Scenarios

### Journal.CompletedEntries

Purpose:

- Prove that the Journal completed-entry path is correctly mapped and that pre-UI Journal data drives visible UI behavior

Capture targets:

- `Journal.Instance()`
- `PrimaryEntryContext`
- `EntryGroups`
- Each `Entry.Text`
- Group keys and in-group ordering fields
- Relevant `AgentQuestJournal` state

Compare behavior:

- `LocalClientStructs` reads the mapped Journal structs directly
- `OwnerSignatures` resolves the same state through plugin-owned signatures and detours
- The compare step asserts equal counts, groups, texts, and key identifiers

Override behavior:

- Apply a controlled text substitution at the pre-UI Journal path used by completed entries
- Use a visible sentinel string
- Assert both internal state change and visible Journal UI change
- Restore the original text path and assert rollback

### Tooltip.ItemDetail

Purpose:

- Prove the tooltip RE work related to item detail generation

Capture targets:

- Hover trigger context
- Resolved item and source parameters
- Tooltip payload / array state
- Final visible text

Compare behavior:

- Compare local-ClientStructs route and owner-signature route for resolved item identity, source, and generated text baseline

Override behavior:

- Inject a controlled visible tooltip mutation through the most reliable generation hook
- Assert visible change
- Restore and re-assert normal tooltip behavior

### Tooltip.ActionDetail

Purpose:

- Prove the tooltip RE work related to action detail generation

Capture targets:

- Trigger parameters
- Action tooltip payload
- Final visible text

Compare behavior:

- Same route comparison model as item tooltip

Override behavior:

- Controlled action-tooltip mutation via detour or pre-UI payload interception
- Assert visible change and successful restore

## UI and Operator Workflow

Each plugin exposes:

- Command to open the validation window
- Scenario list
- Route selector
- Execution mode selector
- Live status panel
- Capture and compare preview
- Override preview and active state
- Export location display

Execution flow:

1. Operator selects scenario
2. Operator selects route
3. Operator selects mode
4. Plugin validates prerequisites
5. Plugin arms only the hooks/detours required by that scenario
6. Operator performs the in-game trigger
7. Plugin completes the pipeline
8. Plugin tears down all scenario-specific hooks
9. Plugin displays pass/fail and export location

The plugin must not remain in a broadly intercepting state after the run completes.

## Evidence Output

Each authoritative run writes:

- JSON artifact for machine consumption
- Markdown artifact for human review

### JSON

Location pattern:

- `plugins/artifacts/evidence/YYYY-MM-DD/<scenario>/<timestamp>-<route>.json`

Fields:

- Game executable path, version, and SHA-256
- Plugin id, scenario id, route, mode, timestamp
- Local ClientStructs metadata when applicable
- Owner-signature metadata when applicable
- Capture snapshot
- Compare result
- Override details
- Assert result
- Restore result
- Failure details, if any

### Human Markdown

Location pattern:

- `plugins/artifacts/evidence/YYYY-MM-DD/<scenario>/<timestamp>-<route>.md`

Contents:

- Short header with scenario, route, timestamp, pass/fail
- Tool and game metadata
- Local ClientStructs metadata or owner-signature metadata
- Summary of what was captured
- Summary of compare result
- Summary of override behavior
- Summary of restore behavior
- Final verdict
- Any warnings or cleanup notes

The Markdown file is the "MD humano" companion to the JSON artifact and is required for the first delivery.

## Tests

`plugins/tests/ReValidation.Tests` covers:

- Scenario discovery and registration
- Snapshot normalization
- Compare diff logic
- Evidence JSON serialization
- Evidence Markdown rendering
- Restore-on-failure behavior
- Local route gating
- Owner-signature gating
- UI and command behavior through DalaMock where applicable

What tests do not attempt to prove:

- Actual live native memory correctness
- Real in-game pointer validity
- Visual tooltip or Journal mutations in a game process

Those are proven by scenario runs and their exported evidence.

## Guardrails

- No scenario may keep hooks active beyond explicit execution scope
- Every detour must have guaranteed teardown in `finally`
- Every override must be reversible
- `restore` is mandatory before success
- `full proof` is blocked when prerequisites are missing
- Evidence must not include unrelated chat, account, or character-sensitive data
- The package is a validation harness, not a gameplay automation system

## Delivery Plan for First Implementation

1. Create plugin solution and shared harness
2. Add local ClientStructs configuration input and gating
3. Add owner-signature resolver and gating
4. Implement evidence JSON + Markdown writers
5. Implement `Journal.CompletedEntries`
6. Implement `Tooltip.ItemDetail`
7. Implement `Tooltip.ActionDetail`
8. Add tests for harness and plugin shell behavior
9. Add setup and scenario documentation

## Success Criteria

The first delivery is successful when:

- Both plugins build inside `LokinModarRETools`
- `LocalClientStructs` proves against an exact local ClientStructs worktree via real `ProjectReference`
- `OwnerSignatures` proves the same target behavior independently
- `Journal.CompletedEntries`, `Tooltip.ItemDetail`, and `Tooltip.ActionDetail` each support `full proof`
- Each `full proof` run emits both `.json` and human-readable `.md` evidence
- Tests pass for the harness and plugin shell behavior
