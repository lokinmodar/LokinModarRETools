# ReValidation Scenarios

Owner-signature scenarios must resolve every required signature before execution. A zero-match
resolution blocks the route. A requirement marked `MustBeUnique` also blocks when the signature
resolves to anything other than exactly one match.

Evidence metadata exposes only `signature:<id>` values containing the match count and, when
available, the RVA. Signature patterns and resolver failure details are not exported.
JSON and Markdown artifacts include phase outcomes, comparison difference counts, override/restore
outcomes, a sanitized verdict summary, and scenario-specific allowlisted capture metrics. Raw Journal
text, tooltip payload lines, visible text, arbitrary context metadata, raw exception messages, and
writer exception details are not exported.

## Journal.CompletedEntries

Open the completed Journal list. Both routes currently capture completed quest state from `QuestManager`
and the quest sheet rather than mutating a pre-UI Journal provider. Evidence exports only the completed-entry
count; entry text and quest keys remain only in the in-memory snapshot used for comparison.

`Compare` requires an independent `IJournalCompletedEntriesComparisonSource`. The run blocks instead of
treating a missing reference as a successful comparison.

`OverrideAssert` and `FullProof` are intentionally blocked for `Journal.CompletedEntries` on both routes.
That is the current limit of the reverse-engineering state, not a hidden failure. We can already prove
capture and comparison of completed Journal data, but we do not yet claim a safe pre-UI mutation point
that changes the quest name consumed by the Journal list itself.

The local route also blocks when local ClientStructs wiring is unavailable. The owner-signature route
also blocks when any scenario-required signature is unresolved or non-unique.

## Tooltip.ItemDetail and Tooltip.ActionDetail

Open an item or action tooltip. Both routes capture only the fixed detail kind and resolved ID for
evidence; payload lines and visible text remain in memory for tooltip comparison. Full proof applies
`[REVALIDATION] Tooltip Sentinel`, verifies it, and restores the original visible text. The local
route blocks full proof when local ClientStructs is unavailable. The owner-signature route blocks when
any scenario-required signature is unresolved or non-unique.
Compare and FullProof require an independent `ITooltipComparisonSource`; the run blocks instead of
treating a missing reference as a successful comparison.

## Operator Workflow

1. Open the route window with `/revalidate-local` or `/revalidate-owner`.
2. Confirm that the route prerequisites are satisfied:
   local props/project wiring for the local route, or unique signature resolutions for the owner route.
3. Select the scenario and the required validation mode: `CaptureOnly`, `Compare`, `OverrideAssert`, or `FullProof`.
4. Prepare the required Journal or tooltip UI cue, then select `Run selected scenario`.
5. Wait for the window status to change from `Running` to `Passed`, `Failed`, or `Cancelled`.
6. Open the JSON and Markdown paths listed in the window and preserve both artifacts with the review notes.

Do not bypass a blocked run. A block means one of three things:

- the route prerequisite was not validated
- the scenario requires an independent comparison source that is not configured
- the current RE state does not yet support the requested mutation proof honestly

Correct the route-specific prerequisite, or lower the requested mode to one the scenario can prove today,
and run the scenario again.
Every configured evidence writer is attempted. A writer failure marks the run failed with a sanitized export
failure record while allowing later writers to produce any remaining artifact.
