# Decisions

## Newly Authorized Decisions

- The first half of M7 is accepted as directionally correct because it preserves the projection-first architecture: backend continuity projections are authority, and the UI consumes those projections rather than interpreting Markdown or proposal files directly.
- The Current Understanding workspace surface must remain read-only; authoritative operational-context changes must continue to flow only through generate, review, and promote.
- Repository-level continuity summary is an important M7 observability surface because continuity health should be visible without opening proposal review.
- Missing, empty, present, pending proposal, accepted proposal, and stale proposal intersections are high-value M7 validation cases because continuity failures are likely at state intersections.
- Before closing M7, Command Center should expose whether operational context is actually participating in execution context, conceptually as an understanding participation projection with included status, included artifact count, and continuity warnings.
- M7 remains an observing-understanding milestone, not an editing, resolving, or governing milestone.

## Recommended Next Slice

- Finish M7 by adding execution-context inclusion visibility for operational context, dashboard last-updated display, and UI state validation for missing, empty, present, pending, accepted, and stale proposal combinations.
