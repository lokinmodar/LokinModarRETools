# ReValidation Scenarios

Owner-signature scenarios must resolve every required signature before execution. A zero-match
resolution blocks the route. A requirement marked `MustBeUnique` also blocks when the signature
resolves to anything other than exactly one match.

Evidence metadata exposes only `signature:<id>` values containing the match count and, when
available, the RVA. Signature patterns and resolver failure details are not exported.

## Journal.CompletedEntries

Open the completed Journal list. Both routes capture only the completed-entry count for evidence;
entry text and quest keys remain in the in-memory Journal snapshot used for comparison. Full proof
applies `[REVALIDATION] Journal Sentinel`, verifies it, and restores the original Journal text.
The local route blocks full proof when local ClientStructs is unavailable. The owner-signature route
blocks when any scenario-required signature is unresolved or non-unique.

## Tooltip.ItemDetail and Tooltip.ActionDetail

Open an item or action tooltip. Both routes capture only the fixed detail kind and resolved ID for
evidence; payload lines and visible text remain in memory for tooltip comparison. Full proof applies
`[REVALIDATION] Tooltip Sentinel`, verifies it, and restores the original visible text. The local
route blocks full proof when local ClientStructs is unavailable. The owner-signature route blocks when
any scenario-required signature is unresolved or non-unique.

## Operator Workflow

1. Open the route window with `/revalidate-local` or `/revalidate-owner`.
2. Confirm that the required route-specific runtime adapter and route inputs are configured; otherwise the selected scenario will report a blocked run.
3. Select the scenario and the required validation mode: `CaptureOnly`, `Compare`, `OverrideAssert`, or `FullProof`.
4. Prepare the required Journal or tooltip UI cue, then select `Run selected scenario`.
5. Wait for the window status to change from `Running` to `Passed`, `Failed`, or `Cancelled`.
6. Open the JSON and Markdown paths listed in the window and preserve both artifacts with the review notes.

Do not bypass a blocked run. A block means that the route's local ClientStructs availability or required
owner signatures were not validated. Correct the route-specific prerequisite and run the scenario again.
