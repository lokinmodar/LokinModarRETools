---
name: contributing-ffxiv-client-structs
description: Use when preparing, reviewing, or updating a pull request against aers/FFXIVClientStructs, especially after changing native C# layouts, function signatures, vtables, source generators, CExporter metadata, or ida/data.yml.
---

# Contributing to FFXIVClientStructs

This is a 64-bit MSVC ABI map. Require agreement between RE evidence, layout, generated behavior, and exporter output.

## Prepare the contribution

1. Read `AGENTS.md`, `README.md`, `.editorconfig`, workflows, and current contribution guidance.
2. Check `git status --short` and `git remote -v`; preserve unrelated work. From a fork, fetch the original repository as `upstream` and base the branch on current `upstream/main`.
3. Keep one concern per PR. Before editing, record native class/name, x64 offset and size, ABI, vtable slot, and signature/resolver offset as applicable.

## Preserve the interop contract

| Change | Required shape |
| --- | --- |
| Native field or type | Mirror the native namespace. Use `[StructLayout(LayoutKind.Explicit, Size = 0x...)]`, a hexadecimal `[FieldOffset(0x...)]` per mapped member, ascending offsets, and unmanaged types. Call an uncertain member `Unk<offset>`. |
| Native call | Put it on an `unsafe partial` generated type. Use exact `[MemberFunction]`, `[StaticAddress]`, or `[VirtualFunction]` metadata and ABI-correct pointers/values. Signatures use two characters per byte and `??` wildcards. |
| Inheritance, arrays, flags | Use `[Inherits<T>]`, `FixedSizeArrayN<T>` plus `[FixedSizeArray]`, and `[BitField]` only on confirmed backing storage. |
| C strings or generic pointers | Use `byte*` plus `[GenerateStringOverloads]`; do not expose ABI-facing `string`. Use `Pointer<T>` for generic pointer storage. |

Do not hand-edit generated code, reorder fields for aesthetics, hide uncertainty by changing a size, or introduce managed references/marshalling into ABI-facing declarations.

## Validate by changed surface

Run from the repository root:

```powershell
dotnet restore .\FFXIVClientStructs.slnx
dotnet build .\FFXIVClientStructs.slnx --no-restore
dotnet format .\FFXIVClientStructs.slnx --verify-no-changes
```

- For `FFXIVClientStructs/**/*.cs`, run `dotnet run --project .\CExporter\CExporter.csproj -c Release -- --no-write`; then omit `--no-write` when `ida/ffxiv_structs.yml` should change. Review that diff and require empty `ida/errors.txt`.
- For generator/runtime changes, run `dotnet test .\InteropGenerator.Tests\InteropGenerator.Tests.csproj --no-restore`; change only intentional focused snapshots.
- For `ida/data.yml`, run `node .\ida\data-validator.js` after installing `js-yaml` as CI does.
- Finish with `git diff --check`, `git diff --stat`, and `git status --short`. The CExporter workflow validates C# layout changes; PRs to upstream `main` also receive API-compatibility checking.

## Open a reviewable PR

Use upstream `main` as base. Keep the diff free of unrelated formatting, local tooling, and generated files other than expected exporter output. Describe verified facts, not guesses.

```markdown
Title: Add <type/member> for <native subsystem>

## Evidence
- Native type/member: `<name>`; offset/size/vtable/signature: `<confirmed values>`.

## Changes
- Added/updated `<fields or wrappers>` without unrelated layout changes.

## Validation
- `dotnet build ...`
- `CExporter ...`; `ida/errors.txt` empty
- `<focused generator test or data validator, if applicable>`
```

## Common mistakes

- Treating a successful compile as ABI proof; the exporter cannot prove RE evidence.
- Using `string`, arrays, or managed abstractions with runtime marshalling disabled.
- Forgetting `unsafe partial`, using the wrong interop attribute, or failing to review exporter output.
- Combining a layout update with speculative cleanup or unrelated refactors.
