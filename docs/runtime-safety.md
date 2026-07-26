# Runtime Safety Boundary

Runtime investigation is supplementary to static reverse engineering. It is
appropriate only for a named unresolved question after binary identity,
signature selectivity, and candidate RVA have been established statically.

- Obtain explicit user authorization for one supervised, read-only observation
  and one harmless UI cue.
- Treat module bases and runtime addresses calculated from process metadata as
  candidates. In x64dbg, validate the module-relative expression with
  `mem.valid(module:$<RVA>)` before arming one hardware breakpoint.
- At a hit, capture only the requested facts, clear all breakpoints, resume,
  and then detach. Do not inject, patch, write target memory, or automate game
  activity.
- Keep candidate and observed addresses distinct in the evidence report, and
  remove unrelated account, chat, character, or memory data before sharing.

CDB is not permitted for live FFXIV breakpoint capture. Its timeout cleanup
path previously left a breakpoint armed and produced `STATUS_SINGLE_STEP`
(`0x80000004`).
