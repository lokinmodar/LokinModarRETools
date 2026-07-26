---
name: investigating-game-signatures
description: Use when locating, validating, or documenting byte signatures in a Windows x64 game executable, especially FFXIV `ffxiv_dx11.exe`, PE files, `E8`/`E9` relative calls, hooks, or FFXIVClientStructs PR evidence.
---

# Investigating Game Signatures

Use static, read-only analysis first. A byte match is not proof of a name, owner, ABI, or safe ClientStructs declaration.

## Workflow

1. Record the binary identity before inspecting it.

   ```powershell
   $exe = 'C:\path\to\ffxiv_dx11.exe'
   Get-Item -LiteralPath $exe | Select-Object FullName, Length, LastWriteTime
   Get-FileHash -LiteralPath $exe -Algorithm SHA256
   [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe) | Format-List ProductVersion, FileVersion
   ```

2. Scan every executable PE section with the bundled scanner.

   ```powershell
   & "$PSScriptRoot\scripts\find-signatures.ps1" `
       -Executable $exe `
       -Signature 'E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF'
   ```

   The output has file offset, RVA, preferred-base VA, section, and the target of a leading `E8`/`E9`. Treat zero or multiple matches as an investigation result.

3. Open the match and its target in Ghidra or IDA. Recover function boundaries, xrefs, x64 arguments, return type, and `this` accesses. Use dynamic analysis only for a concrete unresolved question.

4. Write an evidence row per proposed declaration:

   | Binary SHA-256 | Pattern | Match RVA | Target RVA | Native owner/name | ABI | Result |
   | --- | --- | --- | --- | --- | --- | --- |
   | `<hash>` | `<sig>` | `<rva>` | `<rva or n/a>` | `<confirmed>` | `<args → return>` | add/update/reject |

## Call-Site Rule

`E8 rel32` and `E9 rel32` identify a call/jump instruction. A plugin hook may target that **call-site**; following `rel32` gives the callee. Record both addresses.

In FFXIVClientStructs, `[MemberFunction]` signatures beginning with `E8` or `E9` follow the relative target at offset `1`. Confirm the callee's ABI and owner; this does not make a call-site hook equivalent to the wrapper.

## FFXIVClientStructs PR Gate

Only propose a mapping after confirming a selective signature and match count, native owner, ABI/`this`/vtable slot, and any new layout's fields and size.

Use an existing `unsafe partial` mapped type and the appropriate generated attribute; instance declarations omit `this`. Do not add hooks, `IntPtr` placeholders, managed references, or guessed names. Run build, formatting, and CExporter for a layout PR.

## Tools

- `scripts/find-signatures.ps1`: scans PE32+ AMD64 executable sections and resolves leading `E8`/`E9` targets.
- `scripts/test-find-signatures.ps1`: regression check; pass `-Executable` for another build.
- `rizin`/`rz-bin`: terminal PE and disassembly inspection.
- `ghidraRun`: interactive decompilation, xrefs, and types.

The [Dalamud RE guide](https://dalamud.dev/plugin-development/reverse-engineering/) is the practical orientation for static versus dynamic analysis, signatures, hooks, delegates, and structures.

## Common Mistakes

- Reporting a VA without the binary hash or RVA; ASLR makes runtime VAs non-portable.
- Treating a unique byte match as semantic proof of a function.
- Resolving an `E8` target but losing the caller context required by a hook.
- Converting unknown arguments into confident C# types before inspecting the x64 calling convention.
