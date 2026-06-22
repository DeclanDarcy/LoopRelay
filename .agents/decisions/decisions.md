# Decisions

## Newly Authorized

- The M3 refinement slice is accepted as aligned with the roadmap.
- Refinement remains proposal evolution, not decision authority.
- `REV-*` artifacts are repository-backed authority for proposal evolution.
- Candidate authority, proposal authority, revision authority, and decision authority must remain separate layers.
- `NeedsRefinement -> Refined` must continue to require an explicit content-bearing refinement operation.
- `ReadyForResolution` must remain protected from refinement and expiration shortcuts.
- The next authorized proposal path after `ReadyForResolution` is `Resolve`.
- The next M3 slice should proceed to resolution, kept narrow.
- Resolution work should introduce `ResolveDecisionCommand`, `DecisionResolution`, `DecisionResolutionRationale`, and `DecisionResolutionHistory`.
- Resolution should implement `ReadyForResolution -> Resolved`.
- Resolution should create authoritative `DEC-*` records and refresh `decision.json`, `decision.md`, and `decisions.md`.
- Resolution tests should prove non-ready proposals cannot resolve, rationale is required, resolver metadata is required, selected option is recorded, recommendation divergence is recorded, proposal resolution does not mutate operational context, and proposal resolution does not project into execution.
- No operational-context assimilation should be implemented in the resolution slice.

## Current Milestone Status

- M0 is complete.
- M1 is complete.
- M2 is complete.
- M3 is in progress.
