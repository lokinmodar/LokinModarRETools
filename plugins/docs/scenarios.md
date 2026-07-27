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
