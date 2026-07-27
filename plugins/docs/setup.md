# ReValidation Setup

Task 1 provides the initial solution and project scaffold.

## Local ClientStructs

Copy `plugins/local/LocalClientStructs.props.example` to `plugins/local/LocalClientStructs.props` and set `ClientStructsProjectPath` to the local ClientStructs project file.

Regular builds remain permissive when this file is absent. A `FullProof` local ClientStructs route requires the props file and a project path that resolves to an existing file. Its evidence metadata is limited to the ClientStructs branch, commit, dirty state, assembly SHA-256, and an allowlisted unavailable reason when the route is blocked.

## Plugin Shells

Build the route plugins with `dotnet build .\plugins\ReValidation.sln`. Both plugin projects use the
real `Dalamud.NET.Sdk` and require a local Dalamud development installation at the SDK's standard
location (or `DALAMUD_HOME`).

Load one route plugin at a time and open its window with the corresponding command:

- `/revalidate-local` opens the Local ClientStructs route.
- `/revalidate-owner` opens the owner-signature route.

The window fixes the route, lets the operator select a registered scenario and validation mode, shows
`Idle`, `Running`, `Passed`, or `Failed`, and lists the JSON and Markdown evidence artifact paths after
a run. Artifacts are written beneath the plugin configuration directory in `evidence`.

The route shells register the concrete Journal and tooltip scenario implementations but fail closed until
route-specific runtime probes, comparison sources, and the required owner-signature inputs are configured.
Only one scenario run can be active per loaded plugin. Plugin disposal cancels the active run while the runner
still performs bounded restore and evidence-export handling.
