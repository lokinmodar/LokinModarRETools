# Dynamis Bridge

`ReValidation.DynamisBridge` is a Journal-focused, read-only exploration aid. It uses the optional Dynamis IPC API; it does not detour game code, mutate client memory, send server traffic, or contribute evidence to proof schemas.

## Usage

1. Install and enable Dynamis, then load the bridge plugin.
2. Open `/revalidation-dynamis` and confirm the status is `Ready`.
3. Open the Journal UI and select `Arm Journal Session`.
4. Select candidates to inspect with Dynamis, mark promising entries for IDA, and export a session note.

## Status

- `Idle`: no Journal session is captured.
- `Blocked`: Dynamis is unavailable or incompatible; inspection is not attempted.
- `Armed`: Journal anchors and ranked candidates were captured.
- `Exported`: the local Markdown note was written.

## Notes

Exported Markdown notes are stored under the plugin configuration directory at `dynamis-bridge`. They contain the captured anchors and candidate metadata for subsequent static review.
