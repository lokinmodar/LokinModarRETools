# Dynamis Bridge

`ReValidation.DynamisBridge` is a Journal-focused, read-only exploration aid. It uses the optional Dynamis IPC API; it does not detour game code, mutate client memory, send server traffic, or contribute evidence to proof schemas.

## Usage

1. Install and enable Dynamis, then load the bridge plugin.
2. Open `/revalidation-dynamis` and confirm the status is `Ready`.
3. Open the Journal UI and select `Arm Journal Session`.
4. Review the explicit Journal addon and `QuestJournal` agent anchors.
5. Select a candidate to render its pointer, inspect its object or a bounded `0x100` region, apply a label, and export a session note.

The first delivery does not scan memory or expand neighboring pointers. Public Dynamis IPC does not expose candidate expansion, so candidates are limited to the two known anchors above.

## Status

- `Ready`: Dynamis API `1.7` or newer within major `1` is available and no session is armed.
- `Blocked`: Dynamis is unavailable or incompatible; inspection is not attempted.
- `Armed`: Journal anchors and ranked candidates were captured.
- `Exported`: the local Markdown note was written.
- `Export Failed`: no active session exists or the local note could not be written.

If Dynamis unloads or becomes incompatible, an armed session is cleared immediately and the window returns to `Blocked`.

## Notes

Exported Markdown notes are stored under the plugin configuration directory at `dynamis-bridge`. They contain the arm timestamp, captured anchors, raw addresses, labels, and candidate metadata for subsequent static review.
