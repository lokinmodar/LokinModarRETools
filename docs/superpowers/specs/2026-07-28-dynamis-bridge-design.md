# Dynamis Bridge Design

Date: 2026-07-28

## Goal

Add a dedicated exploration plugin to `LokinModarRETools` that uses `Dynamis`
as a runtime inspection backend for reverse-engineering work that is still
below the confidence bar required for `FFXIVClientStructs` or for the existing
`ReValidation` proof routes.

The first target is `Journal`. The bridge must help locate and classify
candidate provider, cache, array, and string-bearing objects before the final
UI render path consumes them.

This plugin is an exploration aid. It is not an authoritative proof route and
it is not a substitute for static RE, ClientStructs mappings, or the existing
validation plugins.

## Scope

Initial delivery:

- New optional plugin: `plugins/ReValidation.DynamisBridge/`
- IPC-only integration with the external `Dynamis` plugin
- Journal-focused guided exploration session
- Pointer inspection UI for candidate objects
- Local Markdown export of session notes and candidate rankings
- Unit and controller-level test coverage for IPC availability and candidate
  prioritization

Out of scope for the first delivery:

- Direct code reuse from the `Dynamis` repository
- Memory patching, live mutation, or UI overrides
- Authoritative validation evidence in the `ReValidation` proof schema
- Generic support for every addon or agent in the game
- Automated `old exe -> new exe` binary diff workflows

The bridge may later inform binary-diff work after a patch, but that is not
required for the first Journal-focused version.

## Design Principles

1. Keep exploration separate from proof.
   `ReValidation.LocalClientStructs` and `ReValidation.OwnerSignatures`
   continue to own reproducible proof. `ReValidation.DynamisBridge` only helps
   discover what should later be mapped or formally validated.
2. Stay upstream-safe.
   Nothing in the bridge becomes a `ClientStructs` declaration or an upstream
   conclusion by itself.
3. Prefer narrow guided sessions over generic scanning.
   The first version should solve Journal exploration well instead of exposing
   a broad, weakly structured inspector.
4. Do not copy Dynamis implementation code.
   The bridge integrates through IPC only. This keeps ownership boundaries and
   license boundaries clear.
5. Remain read-only and session-scoped.
   No persistent detours, no memory writes, no server traffic, and no
   unattended runtime behavior.

## Repository Layout

The new plugin fits into the existing plugin solution and documentation layout.

```text
plugins/
  ReValidation.sln
  ReValidation.Common/
  ReValidation.LocalClientStructs/
  ReValidation.OwnerSignatures/
  ReValidation.DynamisBridge/
  tests/
    ReValidation.Tests/
  docs/
    setup.md
    scenarios.md
    dynamis-bridge.md
```

Notes:

- `ReValidation.DynamisBridge` is optional and must degrade cleanly when
  `Dynamis` is not installed or not initialized.
- The existing shared test project remains the single test package unless a
  later need forces plugin-specific runtime tests.

## Relationship To Existing ReValidation Work

The bridge does not join the current branch-proof execution pipeline.

Current roles:

- `ReValidation.LocalClientStructs`
  Proves that a local ClientStructs checkout/build works in a live plugin
  scenario.
- `ReValidation.OwnerSignatures`
  Proves equivalent behavior through plugin-owned signatures and hooks.
- `ReValidation.DynamisBridge`
  Helps find new pre-UI runtime objects and relationships when neither route
  yet knows the right mapping or hook point.

Output handoff:

- The bridge produces human-reviewed exploration notes.
- Those notes inform static work in IDA and future `ClientStructs` or
  `ReValidation` changes.
- No bridge session is treated as authoritative proof by itself.

## IPC Integration Model

The plugin talks to `Dynamis` only through its public IPC surface.

Required IPC features:

- `Dynamis.GetApiVersion`
- `Dynamis.ApiInitialized`
- `Dynamis.ApiDisposing`
- `Dynamis.InspectObject.V3`
- `Dynamis.InspectRegion.V2`
- `Dynamis.GetClass.V1`
- `Dynamis.IsInstanceOf.V1`
- `Dynamis.ImGuiDrawPointer.V4`

Compatibility behavior:

- If `GetApiVersion` is unavailable, the bridge reports `Unavailable`.
- If the API version is below the minimum expected contract, the bridge reports
  `Incompatible`.
- If `Dynamis` unloads or disposes during a session, the bridge tears down the
  session state and returns to `Unavailable`.
- Delegates and handles received from `Dynamis` are never cached beyond the
  active availability window.

## Plugin Components

### ReValidation.DynamisBridge.Plugin

Plugin entrypoint modeled after the existing `ReValidation` plugin shells.

Responsibilities:

- Register services
- Bind the window system
- Track `Dynamis` availability lifecycle
- Expose one command to open the bridge window

### DynamisApiClient

Thin wrapper around all supported IPC endpoints.

Responsibilities:

- Resolve IPC providers/callgates
- Surface a stable, typed API to the rest of the plugin
- Handle version checks and feature gates
- Subscribe and unsubscribe from `ApiInitialized` and `ApiDisposing`

### DynamisAvailabilityService

Single source of truth for bridge readiness.

States:

- `Unavailable`
- `Incompatible`
- `Ready`
- `SessionActive`

Responsibilities:

- Translate low-level IPC availability into simple UI/controller states
- Clear active sessions when `Dynamis` becomes unavailable

### PointerInspectionService

Read-only runtime exploration service.

Responsibilities:

- `GetClass` lookup
- `IsInstanceOf` checks against known class names
- `InspectObject` requests
- `InspectRegion` fallback for unknown objects
- Pointer drawing helpers through `ImGuiDrawPointer`

### JournalProbeSession

Journal-focused exploration session state.

Responsibilities:

- Capture the current set of Journal-related anchor pointers
- Build a bounded list of candidate pointers derived from those anchors
- Query `Dynamis` for class and region insight
- Rank candidates by likely reverse-engineering value

The session is temporary. It must be explicitly armed and may be discarded and
restarted at any time.

### JournalCandidateModel

Stable model for one candidate object.

Fields:

- Candidate id
- Raw address
- Anchor source
- Logical role guess
- Class name, if known
- Region/type fallback summary, if class is unknown
- Confidence level
- Notes
- User classification

### JournalExplorerWindow

ImGui window for the guided Journal workflow.

Responsibilities:

- Show bridge availability
- Arm and reset Journal sessions
- Show anchor pointers and candidate list
- Let the user open objects/regions in `Dynamis`
- Let the user mark candidates as discarded, promising, or high-value for IDA
- Export a local session note

### EvidenceNoteWriter

Writes a human-readable session artifact.

Format:

- Markdown only in the first delivery
- Local file under a plugin-owned artifacts directory
- No proof-schema coupling to `ReValidation.Common` evidence envelopes

## Journal-First Exploration Flow

The bridge is optimized for one narrow question:

"What object or object chain appears to feed Journal entry data before it is
flattened into final UI nodes?"

Flow:

1. The user opens `JournalExplorerWindow`.
2. The plugin checks `Dynamis` IPC availability and version.
3. The user clicks `Arm Journal Session`.
4. The plugin captures a small, known set of anchor pointers related to the
   Journal path.
5. For each anchor or derived candidate pointer, the plugin asks `Dynamis` for
   class, instance, or region information.
6. The plugin ranks each candidate into one of these buckets:
   - `UI root`
   - `agent state`
   - `provider/cache candidate`
   - `entry array candidate`
   - `string-bearing candidate`
   - `unknown`
7. The user opens the most promising candidates in `Dynamis` from the bridge
   UI.
8. The user marks interesting candidates.
9. The plugin exports a Markdown note for later IDA and ClientStructs work.

## Journal Anchor Strategy

The first version should not guess broadly across memory. It should start from
known runtime anchors and walk only a narrow local neighborhood.

Anchor sources may include:

- Journal addon pointer
- Journal-related agent pointer
- Immediate child pointers reachable from those anchors
- Candidate arrays or pointer-bearing fields already suspected from prior
  RE work
- Pointers surfaced by temporary Journal probes already present in the local
  validation codebase

The bridge prefers bounded traversal:

- direct fields
- small fixed neighborhoods
- explicit candidate pointers already observed in prior RE work

The bridge does not attempt a generic heap scan or broad memory crawl.

## Candidate Ranking

Candidate ranking is the main value-add over simply opening `Dynamis`
manually.

Signals that increase rank:

- Pointer resolves to a known class with structured fields
- Region size suggests a non-trivial object rather than a leaf string buffer
- Object shape or immediate children look like arrays or entry collections
- Nearby strings resemble quest or Journal text
- Candidate is not already obviously a final UI `TextNode`

Signals that decrease rank:

- Candidate is clearly a leaf render node
- Candidate has no useful class or region structure
- Candidate is too volatile or transient across repeated Journal opens

The ranking goal is not certainty. The goal is to narrow the search space
before static RE continues.

## UI Design

The window should remain intentionally plain and utilitarian.

Sections:

- Bridge status
- Session controls
- Anchors
- Ranked candidates
- Candidate details
- Export controls

Expected controls:

- `Arm Journal Session`
- `Reset Session`
- `Inspect Object`
- `Inspect Region`
- `Mark Discarded`
- `Mark Promising`
- `Mark High Value For IDA`
- `Export Session Note`

The bridge window should show the raw pointer address in a copy-friendly form
and also render the pointer through `Dynamis.ImGuiDrawPointer` when available.

## Export Format

Each session note should contain:

- Session timestamp
- Bridge plugin version
- Dynamis API version
- Candidate executable identity when available from the host environment
- Journal anchor list
- Ranked candidate list
- User-applied labels
- Short rationale per promising candidate
- Suggested next static RE actions

The note is intentionally human-authored and human-reviewed. It is not treated
as machine-authoritative proof.

## Safety And Runtime Constraints

The bridge is explicitly read-only.

Allowed behavior:

- Read pointers already available to the plugin
- Ask `Dynamis` to inspect object or region structure
- Render pointer helpers
- Export local notes

Forbidden behavior:

- Memory patching
- Text mutation
- Persistent detours
- Background polling without an armed session
- Server-interacting automation
- Any workflow that turns exploration into unattended gameplay behavior

Failure behavior:

- If `Dynamis` is unavailable, the bridge reports a blocked state and does
  nothing else.
- If an IPC call fails during a session, the bridge marks that candidate with a
  diagnostic note and continues where safe.
- If the availability changes mid-session, the session is invalidated and must
  be re-armed.

## Licensing And Ownership Boundary

`Dynamis` is an external project with its own license and implementation.

Requirements:

- Integrate through public IPC only
- Do not copy implementation code, heuristics, or internal assets into
  `LokinModarRETools`
- Keep the bridge's own logic limited to orchestration, ranking, and export

This preserves a clear ownership boundary and avoids treating the bridge as a
fork or derivative implementation of `Dynamis`.

## Testing Strategy

The first delivery should use the existing unified test project.

Test coverage:

- `DynamisApiClient` availability and version gating
- Transition handling for `ApiInitialized` and `ApiDisposing`
- Journal candidate ranking logic
- Window/controller state transitions for unavailable, ready, armed, and
  completed sessions
- Export-note structure and file naming

The tests do not require `Dynamis` or the game process to be running.

Optional later tests:

- local integration smoke test with a fake IPC shim
- manual runtime checklist in `plugins/docs/dynamis-bridge.md`

## Phased Delivery

### Phase 1

- Create `ReValidation.DynamisBridge`
- Add IPC wrapper and availability model
- Add Journal session model and basic candidate ranking
- Add UI and Markdown export
- Add tests and docs

### Phase 2

- Improve Journal anchor derivation with newly learned runtime pointers
- Add stronger candidate ranking heuristics
- Add optional linkage from session notes to known ClientStructs/IDA targets

### Phase 3

- Consider additional guided targets beyond Journal
- Consider a separate binary-diff assistant that consumes `old exe` and
  `new exe` identities, using bridge discoveries only as a supporting signal

## Success Criteria

The first version is successful if:

- the bridge starts and degrades cleanly with or without `Dynamis`
- a Journal session can be armed intentionally by the user
- the plugin produces a bounded, readable candidate list
- promising non-TextNode candidates can be opened directly in `Dynamis`
- the session exports a useful Markdown note for later static RE work
- the design remains clearly separate from proof and from upstream-safe
  ClientStructs conclusions
