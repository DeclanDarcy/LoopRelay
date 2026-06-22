# Decisions

## Newly Authorized

- The implemented outcome semantics are accepted as the authoritative M6 interpretation:
  - `Accepted` -> `Resolved`
  - `Rejected` -> `Archived`
  - `Deferred` -> `UnderReview`
- `DecisionLifecycleRules` should remain the owner of outcome-to-state interpretation; services, endpoints, and UI must not duplicate competing transition semantics.
- A proposal becoming `Resolved` after any explicit human outcome is accepted as correct because it means the proposal lifecycle ended, not that its recommendation was accepted.
- The resulting decision state carries the meaning of the human response.
- M6 should now shift from recommendation evolution to decision authority evolution.
- Before UI expansion, supersede/archive work should settle whether and how decision authority can be replaced.
- Supersede must be treated as first-class lineage, not just a status flag:
  - an older decision is replaced by another authority
  - that replacement relationship must be reconstructable from repository artifacts
  - governance and operational adoption will later depend on that lineage
- Archive should mean a decision is no longer active; it is distinct from supersession.
- The next implementation work should validate that authority transitions can be reconstructed without external history:
  - proposal
  - resolution
  - decision
  - superseded decision, when applicable
  - replacement decision, when applicable
- Resolution UI remains deferred until resolution semantics, decision lifecycle transitions, supersede/archive behavior, assimilation boundaries, and projection consistency are stable.
- Projection/index refresh behavior after accept, reject, and defer is now a relevant M6 concern.
- The next backend slice should verify that proposal browser, decision browser, review workspace, and lineage projections cannot disagree after proposal-to-decision authority transitions.
- Current M6 status is accepted as:
  - Resolution snapshot complete
  - Resolution service boundary complete
  - Outcome semantics complete
  - Outcome certification complete
  - Supersede/archive authority next
  - Lineage validation next
  - Projection refresh next
  - Resolution UI deferred
