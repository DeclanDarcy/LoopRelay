# Milestone 6: Optional Specialized Read Models

Goal: implement only the specialized read models approved by Milestone 5. Skip this milestone for concepts that remain derived.

## Allowed Implementation Choices

| Choice | Description |
| --- | --- |
| Derived cache | Rebuildable file or memory cache, clearly marked non-authoritative. |
| Read-model report | Persisted reconstruction/report artifact created on demand. |
| First-class entity | Repository-backed structured artifact with explicit authority disclaimers and recovery rules. |

If no concept is approved for materialization, close this milestone with a no-op certification report and proceed to long-horizon validation.

## Constraints

- [x] Do not introduce CRUD endpoints for all concepts by default.
- [x] Do not create state machines just because an event family exists.
- [x] Do not persist direction as a first-class object unless the materialization review proves a stable abstraction.
- [x] Every new artifact type must document how it can be rebuilt or why it cannot be rebuilt.

No new artifact type was introduced in this milestone because Milestone 5 remained advisory and did not authorize any specialized persistence.

## Tests

- [x] Approved read models are rebuildable from events or explicitly justified.
- [x] No unapproved artifact directories are created.
- [x] New projections remain explanatory.
- [x] Existing authority boundaries remain intact.

`ReasoningSpecializedReadModelBoundaryTests` covers the no-op path: even advisory `AddReadModelReport` and `AddDerivedCache` recommendations do not create specialized artifact directories or change graph/query/reconstruction output.

## Exit Criteria

- [x] Only justified specialization exists.
- [x] Event-led reconstruction remains the primary path.

Milestone 6 is closed as a no-op specialization slice. The next implementation milestone is long-horizon validation.
