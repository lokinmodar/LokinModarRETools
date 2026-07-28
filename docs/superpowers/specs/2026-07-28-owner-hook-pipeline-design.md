# Owner Hook Pipeline Design

**Date:** 2026-07-28

## Goal

Replace the current tooltip-specific owner-hook path in `ReValidation.OwnerSignatures` with an explicit scenario-first hook pipeline that validates the current Journal finding first, then migrates tooltip scenarios onto the same infrastructure.

## Context

`ReValidation.OwnerSignatures` already has three useful pieces:

- owned signature resolution
- runtime hook installation from resolved RVAs
- tooltip proof execution with hit counting, assert, and restore

Those capabilities are real, but they are still organized around tooltip-specific behavior. Journal currently participates only as a signature-gated capture/comparison route and does not have an explicit hook-validation or mutation-proof path.

The next step is not "more tooltip." The next step is to make the current Journal finding first-class, validate it through an explicit owner-hook pipeline, and only then migrate tooltip to that same model.

## User-Driven Design Decisions

These decisions are fixed for this work:

- `Journal` is the first target of the new infrastructure.
- The current Journal finding must be the first real hook target; do not invent a different Journal target before validating the current one.
- The UI and artifacts must stay explicit rather than compressing multiple proof steps into a single opaque verdict.
- `Journal.CompletedEntries` remains separate from hook validation.
- `Journal.HookValidation` and `Journal.MutationProof` both exist from the first slice.
- `Journal.MutationProof` may honestly report `effect_not_proven`.
- Tooltip migration happens in the same overall effort, but after the Journal-first slice is in place.

## Problem Statement

The current owner route has two structural problems:

1. The runtime hook infrastructure is centered on tooltip behavior instead of a reusable proof pipeline.
2. The current Journal finding cannot be expressed as an explicit proof lifecycle with separate outcomes for:
   - signature resolution
   - hook installation
   - hit observation
   - context capture
   - mutation attempt
   - effect proof
   - restore proof

Because of that, the system cannot cleanly say "we proved the hook point is real" without also implying "we already proved semantic mutation."

## Non-Goals

This design does not include:

- upstream `ClientStructs` declarations inferred only from runtime behavior
- uncontrolled direct process-memory reads or writes
- detours outside the owned signature route
- silent downgrades from mutation proof to capture-only success
- merging `Journal.CompletedEntries` into the hook-validation workflow
- generic support for every possible addon or agent before the Journal-first slice is proven

## Scenario Model

The owner route gains explicit hook scenarios:

- `Journal.HookValidation`
- `Journal.MutationProof`

The existing `Journal.CompletedEntries` scenario remains separate and keeps its current purpose: capture and comparison of completed Journal data.

After the Journal-first slice is stable, tooltip scenarios migrate onto the new pipeline:

- `Tooltip.ItemDetail`
- `Tooltip.ActionDetail`

At the end state, tooltip is no longer the defining model for owner hooks. It becomes another consumer of the same explicit proof pipeline.

## Architecture

### 1. Hook Target Definition Layer

Introduce a declarative target definition layer for owned hooks. Each target definition describes:

- `TargetId`
- `SignatureId`
- the exact hook delegate type
- how to install the hook
- which runtime arguments or derived values should be captured as sanitized context
- whether mutation is supported
- how assert and restore are expressed for that target

This keeps Journal and tooltip differences in target definitions instead of baking them into the executor.

The first real target is the current Journal finding tied to `journalProvider`.

### 2. Generic Proof Pipeline

Introduce a reusable owner proof pipeline that runs explicit proof stages in order and records each stage independently.

The pipeline stages are:

- `SignatureResolved`
- `HookInstalled`
- `HitObserved`
- `ContextCaptured`
- `MutationAttempted`
- `EffectAsserted`
- `RestoreAttempted`

This pipeline is generic. It should not know whether it is executing Journal or tooltip logic. It only knows how to:

- look up the target definition
- resolve the owned signature
- install and dispose the hook
- collect stage results
- call optional context capture, mutation, assert, and restore strategies

### 3. Scenario Layer

Scenarios remain explicit user-facing units.

`Journal.HookValidation` uses the Journal target but stops at validating:

- signature resolution
- hook installation
- hit observation
- context capture

`Journal.MutationProof` uses the same Journal target but also attempts:

- mutation
- effect assertion
- restore

If the current Journal point allows control but does not yet prove a useful semantic change in the live Journal list, the result must be `effect_not_proven`, not `passed`.

### 4. Tooltip Migration Layer

Tooltip scenarios migrate after the Journal-first slice is in place.

The existing tooltip-specific hook factory and executor become implementation details or are removed once:

- tooltip targets are represented as normal hook target definitions
- tooltip proof execution runs through the generic owner proof pipeline
- existing tooltip evidence semantics remain preserved

## Proof Status Model

Each proof stage uses a small, stable status set:

- `Passed`
- `Blocked`
- `NotObserved`
- `EffectNotProven`
- `Failed`

Meaning:

- `Passed`: the stage completed and met its proof condition
- `Blocked`: execution could not honestly proceed because a precondition was not met
- `NotObserved`: the hook installed but the expected runtime event was not observed
- `EffectNotProven`: mutation was attempted or control exists, but useful semantic effect was not proven
- `Failed`: the stage should have been runnable but ended in an actual execution failure

This model exists to prevent the system from collapsing "the point is real" and "the semantic mutation is proven" into the same verdict.

## Evidence Model

Artifacts for owner hook scenarios must become more explicit.

Each run should expose:

- scenario id
- target id
- signature resolution summary
- stage-by-stage records
- sanitized context captured from hook arguments
- mutation attempt summary
- assert summary
- restore summary
- final scenario verdict derived from stage outcomes

For Journal, the artifacts must clearly show:

- whether the current `journalProvider` finding resolved uniquely
- whether a hook was installed
- whether the detour observed runtime hits
- what useful context was captured
- whether mutation was attempted
- whether the effect was actually proven

The evidence format must remain honest even when mutation proof is not yet available.

## Journal-First Runtime Behavior

### `Journal.HookValidation`

This scenario must:

1. Resolve the owned `journalProvider` signature.
2. Install the hook using the new pipeline.
3. Wait for the relevant Journal runtime event.
4. Record whether a hit was observed.
5. Capture sanitized context from the detour.
6. Export explicit stage evidence.

It must not pretend semantic mutation proof happened if only observation and context capture were achieved.

### `Journal.MutationProof`

This scenario must:

1. Reuse the same target definition.
2. Attempt a controlled mutation strategy if the target supports one.
3. Attempt an assertion of effect.
4. Attempt restore.
5. Report `effect_not_proven` when control is plausible but semantic effect is still unproven.

The first slice may legitimately stop here. That is not a design failure; it is an honest reflection of current reverse-engineering maturity.

## Candidate Journal Target Policy

The first Journal target must be the current finding represented today by `journalProvider`.

Do not introduce a different first target simply because it is easier to fit into the infrastructure. The new pipeline exists specifically to validate what has already been found.

If later reverse-engineering reveals a better Journal hook point, that should be introduced as a new target with explicit evidence, not silently substituted for the original finding.

## Migration Strategy

The work is split into two tracks inside the same overall effort.

### Track A: Journal-first infrastructure and scenarios

Deliver:

- generic owner hook pipeline
- Journal target definition for the current finding
- `Journal.HookValidation`
- `Journal.MutationProof`
- explicit evidence and UI state for stage-by-stage results

### Track B: Tooltip migration

After Track A:

- represent tooltip targets with the new target-definition model
- migrate tooltip proof execution to the generic pipeline
- preserve the existing tooltip sentinel lifecycle semantics
- remove or retire the old tooltip-specific execution path so the repo does not keep two competing systems

## Testing Strategy

Minimum required coverage:

### Core pipeline tests

- target registry binding from `TargetId` to `SignatureId`
- signature resolution stage behavior
- hook installation success/failure behavior
- `NotObserved` behavior when no hit occurs
- explicit stage aggregation into a final report

### Journal tests

- `Journal.HookValidation` reports:
  - signature resolved
  - hook installed
  - hit observed
  - context captured
- `Journal.MutationProof` can report `EffectNotProven`
- evidence records preserve explicit stage outcomes

### Tooltip migration tests

- migrated tooltip targets still install hooks
- existing tooltip sentinel apply/assert/restore behavior remains preserved
- migrated tooltip reports are equivalent in meaning to the current system

### Composition tests

- scenario registry explicitly includes:
  - `Journal.HookValidation`
  - `Journal.MutationProof`
- `Journal.CompletedEntries` remains separate

## Error Handling

Expected outcomes must be explicit.

- zero or multiple signature matches: `Blocked`
- hook installation failure: `Failed`
- hook installed but no hit observed: `NotObserved`
- context capture impossible because target fired with unusable arguments: `Failed` or `Blocked`, depending on whether the failure is structural or environmental
- mutation attempted but semantic effect not proven: `EffectNotProven`

The system must not collapse these into one generic failure string.

## Safety Constraints

The new pipeline must preserve the current safety posture:

- owned signatures only
- no direct arbitrary process-memory reads
- no arbitrary process-memory writes
- no server traffic
- no unattended runtime automation
- no promotion of runtime observations into upstream claims by themselves

Mutation proof is allowed only through the explicit controlled strategies defined for a target, with explicit assert and restore semantics.

## Acceptance Criteria

### Journal-first slice

The Journal-first slice is complete when:

- `Journal.HookValidation` exists as an explicit scenario
- `Journal.MutationProof` exists as an explicit scenario
- both scenarios are backed by the new generic owner hook pipeline
- both scenarios use the current `journalProvider` finding as the first target
- artifacts expose explicit proof stages
- `Journal.MutationProof` can honestly return `EffectNotProven`
- none of this is folded into `Journal.CompletedEntries`

### Tooltip migration slice

The tooltip migration is complete when:

- tooltip scenarios use the new generic owner hook pipeline
- their current proof semantics remain intact
- the old tooltip-specific executor path is removable or removed
- the repository does not maintain two long-term owner-hook systems

## File/Responsibility Direction For The Implementation Plan

The implementation plan should expect new focused files in these areas:

- `plugins/ReValidation.OwnerSignatures/Runtime/HookTargets/`
- `plugins/ReValidation.OwnerSignatures/Runtime/Proof/`
- `plugins/ReValidation.OwnerSignatures/Scenarios/`
- `plugins/tests/ReValidation.Tests/Routes/` or `plugins/tests/ReValidation.Tests/Runtime/` for pipeline coverage

The plan should treat Journal-first delivery and tooltip migration as separate tasks within the same branch, with independent test gates.

## Rationale

This design is intentionally explicit because the current problem is not just "install a hook." The real problem is separating:

- "we found a viable point"
- "we can observe it"
- "we can capture context"
- "we can control it"
- "we can prove semantic effect"

The design succeeds if the system can say those things separately and honestly, starting with the current Journal finding and then absorbing tooltip into the same model.
