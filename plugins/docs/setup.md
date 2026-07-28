# ReValidation Setup

`ReValidation` now has two real validation routes:

- `ReValidation.LocalClientStructs` proves behavior against a local build of `FFXIVClientStructs`.
- `ReValidation.OwnerSignatures` proves behavior against the `FFXIVClientStructs` shipped by Dalamud while keeping our own signature ownership and runtime probes.

Build both plugins and the shared test project with:

```powershell
dotnet build .\plugins\ReValidation.sln
dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj
```

Both plugins use the real `Dalamud.NET.Sdk` and require a local Dalamud development installation at the SDK default location or a valid `DALAMUD_HOME`.

## Dynamis Bridge

Install the `ReValidation.DynamisBridge` plugin, then install and enable `Dynamis`. Open the plugin configuration with `/revalidation-dynamis` and verify that its status line reaches `Ready` with Dynamis API `1.7` or newer within major `1` before arming a Journal session. The bridge captures only the explicit Journal addon and `QuestJournal` agent anchors; it does not scan or expand neighboring memory.

## Local ClientStructs Route

Copy `plugins/local/LocalClientStructs.props.example` to `plugins/local/LocalClientStructs.props` and set `ClientStructsProjectPath` to your local `FFXIVClientStructs\FFXIVClientStructs\FFXIVClientStructs.csproj`.

When this props file exists, the local route switches from the SDK-provided `FFXIVClientStructs.dll` to a direct `ProjectReference` against that local project. This is the route to use when the validation target is "does the exact local ClientStructs branch I intend to merge really behave this way in game?"

Regular builds stay permissive when the props file is absent, but the route reports a blocked state instead of pretending local validation is available. Route metadata exports only:

- ClientStructs branch
- ClientStructs commit
- dirty state
- loaded assembly SHA-256
- an allowlisted blocking reason when local configuration is missing

## Owner Signatures Route

The owner route keeps the runtime on top of the `FFXIVClientStructs` assembly provided by Dalamud. It resolves and records our owned signatures, then drives the same validation scenarios with route-local runtime probes.

Use this route when the validation target is "can we reproduce the same behavior safely without depending on a custom ClientStructs build?"

The current route requires unique matches for:

- `journalProvider`
- `itemTooltip`
- `actionTooltip`

If any required signature resolves to zero or multiple matches, the route blocks the scenario rather than downgrading silently.

## Loading The Plugins

Load one route plugin at a time and open its window with the corresponding command:

- `/revalidate-local` opens the Local ClientStructs route.
- `/revalidate-owner` opens the owner-signature route.

The window fixes the route, lets the operator select a scenario and validation mode, shows `Idle`, `Running`, `Passed`, or `Failed`, and lists the JSON and Markdown evidence paths after a run. Artifacts are written beneath the plugin configuration directory in `evidence`.

Only one scenario run can be active per loaded plugin. Plugin disposal cancels the active run while the runner still performs bounded restore and evidence export handling.
