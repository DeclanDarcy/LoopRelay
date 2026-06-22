# Decisions

## Newly Authorized

- The M3 resolution slice is accepted as aligned with the roadmap.
- Resolution is the correct lifecycle point for authoritative `DEC-*` decision records to begin existing.
- Resolution must remain an explicit human action, not an automatic promotion from proposal recommendation.
- `ReadyForResolution` remains the required gate before `Resolved`.
- Selected option, resolver metadata, rationale, and recommendation divergence are decision-authority metadata.
- Decision resolution must continue not to mutate operational context, create assimilation recommendations, or project into execution.
- The next M3 slice should implement proposal discard as a constrained proposal-state transition only.
- Discard should explicitly define allowed source states and reject all others.
- Discard must persist proposal state, history entry, timestamp, and reason without mutating candidates, decisions, or resolution objects.
- Discard should refresh `proposal.md` and `decisions.md`.
- Discard tests should prove resolved proposals cannot be discarded.
- Discard boundary tests should prove no mutation of `DEC-*` records, operational context, assimilation recommendations, or execution projection.
- After discard, run final lifecycle validation and M3 closure review.

## Current Milestone Status

- M0 is complete.
- M1 is complete.
- M2 is complete.
- M3 is nearly complete.
