# LokinModar RE Tools

Read-only PowerShell tooling, Ghidra helpers, and agent skills used to gather
repeatable evidence for FFXIVClientStructs reverse-engineering work.

This repository does not distribute game binaries, captures, debugger logs, or
personal runtime evidence.

## Contents

- Signature and PE utilities that record binary identity, scan executable
  sections, and resolve `E8`/`E9` relative targets.
- A Ghidra headless adapter and post-script for repeatable function evidence.
- Runtime-evidence helpers that keep candidate and observed addresses distinct.
- Guarded x64dbg disposable-target utilities and regression checks.
- Skills for static signature work, supervised runtime confirmation, and
  FFXIVClientStructs contribution discipline.
- PR-specific ClientStructs validation examples under `examples/`.

## Safety

Static analysis is the primary source of evidence. Runtime observation is only
for a concrete unresolved question and must remain read-only, supervised, and
scoped to a single authorized observation. Candidate process addresses are not
debugger authority; validate a module-relative expression in x64dbg before
arming a breakpoint.

CDB is intentionally excluded from live FFXIV breakpoint work. Cheat Engine is
not required by these tools.

## Local checks

The PowerShell checks in `scripts/` accept an `-Executable` path where needed.
Use a disposable PE executable for script-level verification; they do not
require an FFXIV client to be running. Ghidra command-preview checks require a
local Ghidra installation.

Runtime reports, Ghidra projects, logs, and captures are deliberately ignored.
Review and sanitize any evidence before sharing it.

The example checks take a `-ClientStructsRoot` argument and intentionally
validate a known mapping rather than providing a generic ABI proof.
