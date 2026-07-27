# ReValidation Setup

Task 1 provides the initial solution and project scaffold.

## Local ClientStructs

Copy `plugins/local/LocalClientStructs.props.example` to `plugins/local/LocalClientStructs.props` and set `ClientStructsProjectPath` to the local ClientStructs project file.

Regular builds remain permissive when this file is absent. A `FullProof` local ClientStructs route requires the props file and a project path that resolves to an existing file. Its evidence metadata is limited to the ClientStructs branch, commit, dirty state, assembly SHA-256, and an allowlisted unavailable reason when the route is blocked.
