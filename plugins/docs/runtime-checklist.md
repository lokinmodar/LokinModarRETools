# ReValidation Runtime Checklist

Use this checklist when validating the plugins inside Dalamud against the live game client.

## Before Loading A Plugin

1. Build `.\plugins\ReValidation.sln`.
2. Confirm the test suite passes:
   `dotnet test .\plugins\tests\ReValidation.Tests\ReValidation.Tests.csproj`
3. Choose exactly one route to load:
   - `ReValidation.LocalClientStructs`
   - `ReValidation.OwnerSignatures`
4. Close the other route plugin if it is already loaded.

## Local ClientStructs Route

Use this route when the question is "does my exact local ClientStructs branch behave correctly in game?"

1. Create `plugins/local/LocalClientStructs.props` from the example file.
2. Point `ClientStructsProjectPath` to the local `FFXIVClientStructs\FFXIVClientStructs\FFXIVClientStructs.csproj`.
3. Rebuild the plugin after any ClientStructs change.
4. Load `ReValidation.LocalClientStructs` and open `/revalidate-local`.
5. Confirm the window is not blocked by missing local configuration.

Expected route metadata:

- `clientStructsBranch`
- `clientStructsCommit`
- `clientStructsDirty`
- `clientStructsAssemblySha256`

## Owner Signatures Route

Use this route when the question is "can the same behavior be reproduced while we own the signatures and use the ClientStructs shipped by Dalamud?"

1. Load `ReValidation.OwnerSignatures` and open `/revalidate-owner`.
2. Confirm the route resolves unique matches for:
   - `journalProvider`
   - `itemTooltip`
   - `actionTooltip`
3. If any required signature reports zero or multiple matches, stop and fix the signature before treating the route as valid.

Expected route metadata:

- `signature:journalProvider`
- `signature:itemTooltip`
- `signature:actionTooltip`

## Scenario Execution

### Journal.CompletedEntries

What it proves now:

- capture of completed Journal quest data from `QuestManager` plus the quest sheet
- comparison behavior across routes or reference sources

What it does not claim yet:

- a safe pre-UI mutation point that changes the quest name consumed by the Journal list

Run steps:

1. Open the completed quest list in the Journal UI.
2. Run `CaptureOnly`.
3. Run `Compare` only when an independent comparison source is configured.
4. Treat `OverrideAssert` and `FullProof` blocks as expected for now.

### Journal.HookValidation

1. Confirm `signature:journalProvider` reports exactly one match before arming.
2. Open the Journal list.
3. Select `Journal.HookValidation` and arm the scenario.
4. Open or refresh the Journal list to provide the explicit cue.
5. Confirm the evidence reports the resolved signature, installed hook, observed hit, and captured context.
6. Confirm the scenario disarms after the capture.

### Journal.MutationProof

1. Confirm `signature:journalProvider` reports exactly one match before arming.
2. Open the Journal list.
3. Select `Journal.MutationProof` and choose `OverrideAssert` or `FullProof`; `CaptureOnly` and `Compare`
   capture the hook evidence but do not attempt a mutation or assertion.
4. Arm the scenario, then open or refresh the Journal list to provide the explicit cue.
5. Confirm the controlled mutation attempt and assertion run through the owner hook pipeline.
6. Confirm the scenario restores and disarms after the assertion.
7. Treat `effect_not_proven` as an honest failed assertion: it records that the attempted effect was not
   demonstrated and the run does not pass.

### Tooltip.ItemDetail

1. Select the tooltip scenario and mode first.
2. Set the arm window duration in seconds.
3. Select `Arm selected scenario`.
4. Hover or open a normal item tooltip before the arm window expires.
5. Wait for the window status to move from `Armed` to `Running`.
6. Run `Compare` when an independent comparison source is configured.
7. Run `OverrideAssert` or `FullProof` to confirm the sentinel is applied and restored.
8. Use `Run selected scenario now` only when the tooltip is already pinned/persistent and you do not
   need the armed hover window.

### Tooltip.ActionDetail

1. Select the tooltip scenario and mode first.
2. Set the arm window duration in seconds.
3. Select `Arm selected scenario`.
4. Hover or open an action tooltip before the arm window expires.
5. Wait for the window status to move from `Armed` to `Running`.
6. Run `Compare` when an independent comparison source is configured.
7. Run `OverrideAssert` or `FullProof` to confirm the sentinel is applied and restored.
8. Use `Run selected scenario now` only when the tooltip is already pinned/persistent and you do not
   need the armed hover window.

## Evidence Review

After every run, preserve both artifacts from the plugin config `evidence` directory:

- JSON report
- Markdown report

Review the artifacts for:

- selected route and mode
- blocked vs successful precondition state
- comparison mismatch counts
- override/assert/restore outcomes
- allowlisted route metadata only

Do not treat raw UI success alone as proof. The saved artifacts are the reproducible record.

## Dynamis Bridge Journal Session

The bridge captures only the explicit Journal addon and `QuestJournal` agent pointers. It does not expand neighboring pointers or create proof-schema evidence.

1. Enable Dynamis and confirm the bridge reaches `Ready` with API `1.7` or newer within major `1`.
2. Open Journal and select `Arm Journal Session`.
3. Confirm both anchors display raw addresses when available.
4. Select a candidate, render its Dynamis pointer, and use object or bounded region inspection.
5. Apply `Discarded`, `Promising`, or `HighValueForIda`.
6. Export the active session note and confirm the window reports `Exported`.
7. Use `Reset Session` before another capture.

If Dynamis unloads, confirm the armed anchors and candidates are cleared and the window reports `Blocked`.
