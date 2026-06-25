# Decisions

## Newly Authorized

- Treat the proposal-review `OperationalContextEvolutionTimeline` as complete for Milestone 7.
- Keep the broader operational evolution exit criterion open until backend revision-history entries expose preserved, lost, resolved, and modified facts with previous state, current state, reason, evidence, and identity basis where relevant.
- Continue to require React to render backend semantic events for operational-context timelines without parsing markdown, classifying events, inferring relationships, or reconstructing operational-context meaning.
- Make the next Milestone 7 dependency a backend revision-history slice that extends `OperationalEvolutionSummary` with explicit timeline entries.
- Surface backend revision-history timeline entries in the Continuity diagnostics tab only after the backend projection supplies authoritative timeline facts.
- Add backend tests proving continuity services emit revision-history timeline facts.
- Add UI tests proving React renders revision-history timeline facts without classification or inference.
- Continue deferring `Merged` and item-level `NoiseRemoved` compression outcomes unless the compression engine gains distinct backend semantic actions for them.
