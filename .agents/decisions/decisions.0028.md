# Decisions

## Newly Authorized

- The M6 resolution service boundary is accepted as correct:
  - `IDecisionGenerationService` owns advisory recommendation/proposal creation.
  - `IDecisionResolutionService` owns human authority creation.
- Outcome semantics are now the active M6 domain decision to settle before any resolution UI.
- Resolution outcome must drive lifecycle state intentionally; `Outcome` is not the same concept as a state enum value.
- Accepted, rejected, and deferred outcomes must not all be treated as the same authority state.
- Directionally authorized lifecycle semantics:
  - Accepted creates accepted decision authority and should transition the decision to `Resolved`.
  - Rejected creates rejected decision authority and should align closer to `Archived` than `Resolved`.
  - Deferred means no conclusion was reached and should align closer to `UnderReview` than `Resolved` or `Archived`.
- The next implementation slice should reconcile `DecisionResolutionService` with `DecisionLifecycleRules` until accepted, rejected, and deferred have exactly one backend interpretation.
- Add certification-style backend tests for accepted, rejected, and deferred outcomes before UI work.
- Tests must verify proposal state, decision state, resolution snapshot, decision artifact creation, and projection/index output for each outcome.
- Resolution snapshots must be preserved for accepted, rejected, and deferred outcomes because all three are explicit human actions.
- A future reader must be able to inspect the decision record and reconstruct the resolution outcome, what was reviewed, and why it happened without traversing proposal history.
- Resolution UI remains blocked until outcome semantics are stable.
