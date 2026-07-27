# ReValidation Scenarios

Owner-signature scenarios must resolve every required signature before execution. A zero-match
resolution blocks the route. A requirement marked `MustBeUnique` also blocks when the signature
resolves to anything other than exactly one match.

Evidence metadata exposes only `signature:<id>` values containing the match count and, when
available, the RVA. Signature patterns and resolver failure details are not exported.
