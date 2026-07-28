# ClientStructs Signature Validation Design

## Goal

Extend the `ReValidation` plugin set so it can automatically discover new or changed `FFXIVClientStructs` interop bindings for `Journal`, `AgentQuestJournal`, `AddonItemDetail`, `AgentItemDetail`, `AddonActionDetail`, and `AgentActionDetail`, then prove that each discovered binding is usable in real plugin scenarios.

The required proof bar for each discovered target is:

1. the binding resolves uniquely for the current build
2. the binding is observed in a real in-game cue
3. the binding can be hooked or otherwise intercepted safely
4. the binding participates in a reversible, visible client-side effect

This must work for both validation routes:

- `ReValidation.LocalClientStructs`
- `ReValidation.OwnerSignatures`

## Why This Exists

The current harness only validates a fixed set of manually declared signatures and scenarios:

- `journalProvider`
- `itemTooltip`
- `actionTooltip`

That is enough to prove a few specific reverse-engineering findings, but it is not enough to validate a branch of `FFXIVClientStructs` as a whole. The user wants to validate "the signatures we added to ClientStructs" rather than only "the signatures we manually remembered to wire into the plugin."

The new system must therefore:

- discover the new or changed bindings from the local `FFXIVClientStructs` checkout
- normalize them into a runtime validation catalog
- run route-specific proof logic against that catalog
- emit reproducible evidence per discovered target and per proof group

## Scope

### In Scope

- discovery of changed interop bindings from a local `FFXIVClientStructs` checkout
- filtering by target family:
  - `journal`
  - `item-tooltip`
  - `action-tooltip`
- support for relevant interop attributes:
  - `MemberFunction`
  - `StaticAddress`
  - `VirtualFunction`
- proof execution in both existing plugin routes
- JSON and Markdown evidence for each discovered target
- operator controls for discovery mode, base ref, route, and proof scope

### Out of Scope

- validating unrelated `FFXIVClientStructs` subsystems
- unattended gameplay automation
- any cue or proof step that could send data to the server
- silently downgrading an unproven target to success

## Source Of Truth

The source of truth for "what must be validated" is the local `FFXIVClientStructs` checkout, not a hand-maintained manifest in `LokinModarRETools`.

Default discovery source:

- repository: local `FFXIVClientStructs`
- branch: the current checked-out branch in the selected local checkout
- base ref: `upstream/main`
- mode: `diff`

Optional discovery source:

- mode: `all-current`
- still restricted to the approved target families

This preserves a branch-oriented workflow:

- "validate what changed in this branch"
- "revalidate all known bindings in these target families"

## High-Level Architecture

The design is split into four layers.

### 1. ClientStructs Delta Discovery

This layer reads the local `FFXIVClientStructs` checkout and determines which interop bindings are in scope for validation.

Responsibilities:

- resolve the configured local checkout path
- run `git diff <baseRef>...HEAD`
- identify changed files within the approved target families
- parse relevant members and interop attributes
- emit normalized discovered targets

Important rule:

- discovery is based on the local source tree, not only on the loaded assembly

Reason:

- the user cares about validating branch changes, not just whatever symbols happen to exist in a compiled DLL

### 2. Validation Catalog

This layer normalizes discovery output into stable runtime targets.

Each discovered binding becomes a `DiscoveredTarget`.

Required fields:

- `TargetId`
- `DeclaringType`
- `MemberName`
- `BindingKind`
- `PatternOrAddress`
- `SourceFile`
- `ChangedAgainstBaseRef`
- `CueFamily`
- `ProofProfile`
- `RequiredProofLevel`

Example target ids:

- `Journal.Instance`
- `Journal.IsEntryComplete`
- `Journal.GetQuestData`
- `AgentQuestJournal.SomeConsumer`
- `AddonItemDetail.GenerateTooltip`
- `AgentItemDetail.ReceiveEvent`
- `AddonActionDetail.GenerateTooltip`

The catalog is shared by both routes.

### 3. Route-Specific Proof Runners

The catalog is consumed by two route-specific runners:

- `LocalClientStructs` runner
- `OwnerSignatures` runner

Both use the same discovered targets, the same cue model, and the same proof semantics. They differ only in how they resolve and bind runtime hooks.

`LocalClientStructs` runner purpose:

- prove that the exact local branch build works as a real plugin dependency

`OwnerSignatures` runner purpose:

- prove that the same surfaces can be resolved and owned through plugin-controlled signatures and hooks while using the `FFXIVClientStructs` provided by Dalamud

### 4. Evidence And Verdict

The system emits evidence at two levels:

- run summary
- per-target proof record

The run fails if any target in scope fails the required proof bar.

The system must never report success when a discovered target only resolves but does not reach observe, hook, and effect proof.

## Discovery Model

### Target Families

Discovery is limited to these families for the first version:

- `journal`
  - `FFXIV/Client/Game/UI/Journal.cs`
  - `FFXIV/Client/Game/UI/GameEventCallback.cs`
  - `FFXIV/Client/UI/Agent/AgentQuestJournal.cs`
- `item-tooltip`
  - `FFXIV/Client/UI/AddonItemDetail.cs`
  - `FFXIV/Client/UI/Agent/AgentItemDetail.cs`
- `action-tooltip`
  - `FFXIV/Client/UI/AddonActionDetail.cs`
  - `FFXIV/Client/UI/AddonActionDetailBase.cs`
  - `FFXIV/Client/UI/Agent/AgentActionDetail.cs`

### Discovery Modes

`diff`

- discover only bindings that are new or changed against `baseRef`

`all-current`

- discover all relevant bindings in the approved target families, regardless of diff state

### Accepted Attribute Types

First version of discovery supports:

- `[MemberFunction]`
- `[StaticAddress]`
- `[VirtualFunction]`

`[GenerateInterop]` and file membership are used as context for classification but are not themselves validation targets.

## Cue Model

Validation must stay aligned with safe client-side operator cues.

`CueFamily` values:

- `journal.completed-list`
- `tooltip.item-detail`
- `tooltip.action-detail`

Each discovered target belongs to one cue family.

This matters because several targets can be proven by the same cue. A single item tooltip hover may validate multiple tooltip-related bindings in the same chain.

## Proof Profiles

The system must infer proof strategy from the discovered binding plus its target family.

Initial `ProofProfile` set:

- `detour-function`
- `static-address-consumer`
- `consumer-chain`
- `layout-assisted-effect`
- `unclassified`

### detour-function

Used for hookable functions such as `MemberFunction` or `VirtualFunction` targets in tooltip and Journal paths.

Expected proof path:

- resolve the symbol
- install an observation detour
- count and capture hits during the cue
- prove safe hook installation
- apply reversible effect in the function path

### static-address-consumer

Used for `StaticAddress` targets such as `Journal.Instance`.

Expected proof path:

- resolve the static address
- prove it points to the live runtime instance
- prove a hookable consumer path reads that instance
- prove the consumer path can produce the intended visible effect

### consumer-chain

Used when a target is best validated as part of a larger chain rather than as a standalone effect point.

Expected proof path:

- resolve the target
- observe the chain during the cue
- prove interception at the target or immediate consumer
- prove downstream visible effect from the same chain

### layout-assisted-effect

Used when an effect is visible in UI but the proof target is not itself the final text node mutation site.

Expected proof path:

- observe or hook the discovered target
- apply reversible effect at the nearest honest chain point
- prove the resulting UI change

### unclassified

This is an explicit blocked state, not a success path.

If the discovery layer cannot assign a proof profile honestly, the target must be reported as discovered but unclassified, and the aggregate verdict must fail or block according to the selected policy.

## Proof Semantics

The required proof bar is `1 + 2 + 3 + 4`.

### 1. Resolve

For function-like targets:

- unique match count
- stable RVA
- runtime bind created successfully

For static-address targets:

- non-null address
- stable address relative to module
- readable instance pointer

### 2. Observe

The target must be hit or otherwise proven live during a real in-game cue.

Required evidence:

- hit count greater than zero or equivalent live-instance observation
- cue family used
- route used
- safe summary of captured context

### 3. Hook

The target must be safely interceptable.

Function-like targets:

- detour installed
- detour executed
- original behavior preserved
- detour removed cleanly

Static-address targets:

- prove hookability via a consumer path that uses the resolved address

### 4. Effect

The target must participate in a reversible, visible client-side effect.

Tooltip examples:

- apply a sentinel visible text rewrite
- verify the sentinel appears
- restore the original visible text

Journal example:

- prove a pre-UI or in-chain rewrite that changes what the Journal consumes without iterating text nodes
- verify the visible Journal list reflects the rewrite
- restore the original state

Important rule:

- if a discovered target reaches `1`, `2`, and `3` but not `4`, the result is not success
- the result must remain explicit as `effect-not-proven` or a stronger blocking state

## Proof Groups

The executable unit is not a single raw signature by default. The executable unit is a `ProofGroup`.

A `ProofGroup` contains one or more discovered targets that share:

- the same cue family
- compatible proof profile
- the same operator cue

Examples:

- one item tooltip hover may validate `AddonItemDetail.GenerateTooltip` and related item tooltip consumer bindings
- one action tooltip hover may validate `AddonActionDetail.GenerateTooltip` and related action tooltip consumer bindings
- one Journal completed-list opening may validate `Journal.Instance`, `Journal` helpers, and `AgentQuestJournal` consumer paths

This keeps the harness honest without creating one brittle scenario per signature.

## Scenarios

The current fixed scenario model must be expanded.

### Discovery Scenario

Purpose:

- discover targets in scope
- show exactly what the branch requires the harness to prove

Outputs:

- discovered target list
- grouped proof plan
- unclassified targets, if any

### Proof Group Scenario

Purpose:

- execute proof for a single generated group

Outputs:

- per-target proof records
- group verdict
- blocking or failure details

### Aggregate Scenario

Purpose:

- execute all groups in scope and produce a branch verdict

Outputs:

- run summary
- per-group verdicts
- per-target proof records
- final route verdict

## Route Behavior

### LocalClientStructs Route

This route validates the local branch build directly.

Responsibilities:

- use the configured local `FFXIVClientStructs` checkout
- use the local compiled assembly surface
- prove real plugin viability of the branch build

This route answers:

- "does the exact branch I want to merge really work in a live client plugin context?"

### OwnerSignatures Route

This route validates plugin-owned resolution and hooks.

Responsibilities:

- consume the same discovered target catalog
- resolve signatures with plugin-owned patterns
- prove ownership-grade observe, hook, and effect behavior against the runtime shipped by Dalamud

This route answers:

- "can I own these same surfaces independently of the local ClientStructs assembly?"

### Divergence Between Routes

Divergence is expected and must be reported honestly.

Examples:

- local route passes, owner route fails
- local route proves effect, owner route only proves observe and hook

These are not infrastructure bugs. They are meaningful validation results.

## Parameters

The operator must be able to configure discovery and proof scope explicitly.

Required parameters:

- `baseRef`
- `discoveryMode`
- `targetFamilies`
- `targetFilter`
- `requiredProofLevel`
- `route`
- `armTimeoutSeconds`

### baseRef

Default:

- `upstream/main`

### discoveryMode

Allowed values:

- `diff`
- `all-current`

### targetFamilies

Allowed values:

- `journal`
- `item-tooltip`
- `action-tooltip`

### targetFilter

Optional filter by stable target id or member prefix.

Examples:

- `Journal.*`
- `AddonItemDetail.GenerateTooltip`

### requiredProofLevel

Default:

- `4`

Lower levels are diagnostic only and must not silently redefine the branch validation bar.

### route

Allowed values:

- `local-clientstructs`
- `owner-signatures`

### armTimeoutSeconds

Used for transient cue families such as tooltip hovers.

## User Interface Changes

The validation window must move beyond the current fixed-scenario-only UX.

Required commands:

- `Discover targets`
- `Review discovered groups`
- `Run selected proof group`
- `Run all in scope`

Required visible configuration:

- `baseRef`
- `discoveryMode`
- `targetFamilies`
- `targetFilter`
- `requiredProofLevel`
- `route`
- `armTimeoutSeconds`

The fixed `Journal` and tooltip scenarios may remain available during transition, but the long-term primary UX should be discovery-driven.

## Evidence Model

Evidence must be reproducible and branch-oriented.

### Run Summary

The run summary must contain:

- run id
- route
- base ref
- discovery mode
- target families
- discovered target count
- proof group count
- passed count
- failed count
- blocked count
- final verdict

### Per-Target Proof Record

Each discovered target must emit a proof record containing:

- `targetId`
- `declaringType`
- `memberName`
- `bindingKind`
- `cueFamily`
- `proofProfile`
- `matchCount`
- `rva`
- `observedHitCount`
- `hookInstalled`
- `effectApplied`
- `effectRestored`
- `verdict`
- `blockingReason`

### Sanitization

The evidence model must stay compatible with the current sanitization philosophy:

- no raw tooltip payload dumps in exported evidence
- no arbitrary Journal text dumps by default
- no unsanitized exception messages
- no signature pattern leakage unless explicitly approved for local-only diagnostics

The default exported record should prove behavior without leaking more runtime detail than needed.

## Failure Semantics

The aggregate run must not hide gaps.

### Pass

Only when every discovered target in scope passes the required proof bar.

### Fail

When a discovered target has enough classification and execution context to prove that the proof bar was not met.

Examples:

- hook installation failed
- target was never observed during the claimed cue
- effect could not be verified
- restore failed

### Blocked

When the harness cannot honestly complete the proof.

Examples:

- route prerequisites missing
- target discovered but `ProofProfile` is `unclassified`
- no safe non-server cue exists
- Journal effect point still unresolved

## Safety Constraints

The harness must not trigger server traffic or drift into botting behavior.

Hard constraints:

- cue execution must be limited to local UI actions
- no automated gameplay loops
- no combat, movement, farming, or server-originating actions
- all effect proofs must be reversible
- all hooks must be temporary and removed after the scenario
- a failed restore must mark the run failed and stop further automation for that target group

## Testing Strategy

### Unit Tests

Add tests for:

- diff-based discovery
- file-to-family classification
- interop attribute extraction
- `DiscoveredTarget` normalization
- `ProofGroup` grouping
- aggregate verdict calculation

### Contract Tests

Use real source fixtures from the local `FFXIVClientStructs` target files to prove that discovery keeps working when the file content changes shape.

These tests protect:

- attribute parsing
- target id stability
- family classification

### Route Tests

Use fakes for:

- discovery provider
- signature resolver
- local binding resolver
- hook factory
- cue arming
- effect applicator
- restore pipeline

Cover:

- success
- blocked
- `effect-not-proven`
- restore failure
- local/owner divergence

### In-Game Validation

The runtime plugins remain the final proof surface.

Expected artifacts:

- JSON evidence
- Markdown evidence

These artifacts become the reproducible proof record for the branch validation run.

### Regression Bar

Whenever the scoped `FFXIVClientStructs` branch adds a relevant binding in the approved families, the discovery tests must fail until the new binding appears in the discovered catalog.

This prevents silent drift between the branch and the validation harness.

## Risks And Honest Limits

### Automatic Discovery Does Not Infer Full Semantics

Discovery can tell us that a binding changed. It cannot always infer the correct effect site automatically.

The system must therefore prefer:

- explicit `unclassified`
- explicit `effect-not-proven`

over a false pass.

### Journal Is The Most Expensive Family

Tooltip already has a clear reversible visible effect path.

Journal still depends on identifying and proving the correct pre-UI consumer path for list population. Until that is complete, some Journal targets may honestly block at proof level `4`.

### Not Every Target Deserves A Standalone Scenario

That is why `ProofGroup` is required. Without grouping, the harness becomes brittle and noisy.

## Recommended Implementation Order

1. add discovery and catalog generation
2. add proof grouping and parameter model
3. migrate tooltip validation onto the new proof engine first
4. add local and owner route adapters over the shared proof engine
5. add aggregate evidence model
6. extend Journal proof toward effect validation once the correct pre-UI mutation point is confirmed

## Success Criteria

The design succeeds when all of the following are true:

- a branch like `feature/journal-tooltip-unified` can be discovered against `upstream/main`
- all new Journal and tooltip-related bindings appear in the discovered catalog
- both routes can run proof groups over the same discovered catalog
- tooltip bindings can close `1/2/3/4` end to end
- Journal bindings can at minimum close `1/2/3` and explicitly block `4` until the honest effect point is proven
- the evidence artifacts clearly show which discovered targets passed, failed, or blocked, and why
