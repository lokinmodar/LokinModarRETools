---
name: ffxiv-guided-runtime-re
description: Use when assessing whether a static FFXIV reverse-engineering hypothesis needs live confirmation, a user-authorized UI cue, register capture, unknown native argument, or runtime layout question.
---

# FFXIV Guided Runtime RE

Use runtime observation only to answer one concrete question left by static RE. A runtime capture is evidence, not a C# mapping.

## Entry gate

Start only when all of these are present:

1. A named function/question and the unresolved fact (for example, the fourth argument to `AgentItemDetail::OnItemHovered`).
2. Current executable SHA-256, signature/match count, and target RVA from static analysis.
3. explicit user authorization to attach in this session and to perform the stated UI cue.
4. A report path under the ignored local `docs/runtime-evidence/` directory.

If any item is absent, gather static evidence or ask for the missing item. Do not attach broadly “to see what is useful.”

## Incident guard

On 2026-07-22, a CDB hardware execute breakpoint at
`AgentItemDetail::OnItemHovered` remained active after the controller timeout
path. A later hover crashed the client with `0x80000004` (`STATUS_SINGLE_STEP`)
at the armed RVA. The temporary-process lifecycle test was therefore not a
valid safety proof for the live client.

Do not use CDB for live FFXIV breakpoint capture. Do not use `ba e`, `qd`,
`-pd`, timers, controllers, or forced termination as breakpoint cleanup. The
CDB timeout path is a failed experiment, not a valid lifecycle model for a
live client.

x64dbg is permitted only in a supervised interactive session after a
disposable-target cleanup validation and a new explicit user authorization for
the named FFXIV question. Use one address and one user cue. At the breakpoint,
record only the requested state, clear all breakpoints, resume, and only then
detach. Do not turn the experiment into an unattended capture or use a timer
to terminate the debugger.

Static signature/Ghidra work and `Get-GameRuntimeContext.ps1` metadata remain
allowed. If the unresolved fact cannot be settled statically, record it as
pending rather than attempting a live debugger capture.

## Boundaries

- Use one named function/question, one breakpoint, and one single user-authorized UI cue per observation.
- Never inject, patch, write target memory, alter breakpoints outside the stated target, evade protection, or launch the game under a debugger.
- Never automate movement, combat, farming, repeated gameplay, login, or account actions.
- Stop if the debugger cannot attach normally, the target process exits, the captured state disagrees with the static target, or unrelated player/chat/account data appears.
- Do not infer a field name/type from one hit. Preserve unknown native arguments until static and runtime evidence agree.

## Workflow

| Stage | Required result |
| --- | --- |
| Static proof | Binary identity, selective match, RVA, owner/ABI hypothesis. Use `investigating-game-signatures`. |
| Runtime context | Run `scripts/Get-GameRuntimeContext.ps1` with the exact executable, PID, and optional RVA. Treat its base and VA as candidates; confirm the module path and validate the module-relative expression in x64dbg. |
| Calibration | With the game in windowed/borderless mode, identify the target window and a harmless visual anchor. Before using Computer Use, read its `guidance` and `confirmations` documentation. |
| Capture | After the entry gate and disposable-target validation, use x64dbg interactively for one address and one cue. Clear all breakpoints, resume, and only then detach. |
| Evidence | Review the transcript for unrelated data and call `scripts/New-RuntimeEvidence.ps1`. |
| Mapping gate | Require a repeatable observation plus static ABI/layout proof before proposing a ClientStructs declaration. |

## Capture contract

Do not use CDB for live FFXIV breakpoint capture. x64dbg is permitted only in
a supervised interactive session after a disposable-target cleanup validation
and new explicit authorization for the named question. The permitted runtime
context before the attach is limited to binary hash, PID, module base, RVA, and
computed runtime address through `Get-GameRuntimeContext.ps1`.

Use `Get-GameRuntimeContext.ps1` before configuring the breakpoint. It reads process metadata only; it does not attach or change the process. Its candidate module base and candidate runtime address are diagnostic metadata, not proof of the address accepted by the debugger. Do not rely on a preferred image base after ASLR.

Do not arm a breakpoint from a candidate absolute VA. While x64dbg is paused,
evaluate `mem.valid(ffxiv_dx11.exe:$<RVA>)` in the x64dbg Calculator. Proceed
only when it returns `1`. Then arm the one permitted hardware breakpoint with
`bph ffxiv_dx11.exe:$<RVA>,x,1`; do not substitute a calculated absolute VA.

For FFXIV UI, request one live hover/click only after the user authorizes that
single cue for the current x64dbg session. Treat UI Automation failure as
expected for a game window; use a screenshot-confirmed visual anchor only for
harmless visual calibration. At the hit, inspect only the requested registers,
stack locations, and call stack; clear all breakpoints, resume, and only then
detach.

## Evidence record

Put the reviewed debugger transcript in a local file containing only the requested facts, then generate the report:

```powershell
$context = & .\scripts\Get-GameRuntimeContext.ps1 `
    -Executable 'C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\ffxiv_dx11.exe' `
    -ProcessId <PID> -Rva '0x12D8850'

& .\scripts\New-RuntimeEvidence.ps1 `
    -Context $context -Question '<specific unresolved fact>' `
    -FunctionRva '0x12D8850' -TranscriptPath .\capture.txt `
    -OutputPath .\docs\runtime-evidence\<topic>.md `
    -Debugger x64dbg -Interaction '<single UI cue>' -ExpectedField RCX,RDX,R8,R9 `
    -ObservedModuleExpression 'ffxiv_dx11.exe:$12D8850' `
    -ObservedValidationResult 1 -ObservedRuntimeAddress '<RIP observed at hit>'
```

The report generator refuses to overwrite evidence without `-Overwrite`. Keep reports local and scrub character names, account identifiers, chat, and unrelated memory before sharing.

## Common mistakes

| Mistake | Correct response |
| --- | --- |
| “Attach while I am away and explore.” | Do not attach; x64dbg requires a supervised interactive session with one named question and cue. |
| “The debugger can be killed when the timer fires.” | Do not use a timer or controller; clear all breakpoints, resume, and only then detach interactively. |
| A unique signature is treated as a type proof. | Inspect ABI/callers first; use the capture only for the unresolved observation. |
| A candidate absolute VA fails `bph`. | Do not retry with another absolute VA; validate `mem.valid(ffxiv_dx11.exe:$<RVA>)` and use the validated module-relative expression only. |
| The game is exclusive fullscreen or a tooltip moves. | Calibrate a visible anchor in windowed/borderless mode; do not guess coordinates. |
| A capture contains unrelated data. | Stop, discard the transcript locally, reduce the requested fields, and repeat only with permission. |
| One hit looks convincing. | Do not change native declarations; static ABI/layout proof remains required. |

## Example

Question: “At the `OnItemHovered` call site, does static caller analysis explain the source-kind argument and the two opaque native pointers?”

Use static callers, decompilation, and signature context first. If the exact
question remains unresolved, validate the x64dbg cleanup sequence on a
disposable target, request a new authorization, and perform one supervised
observation.
