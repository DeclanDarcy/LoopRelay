# Decisions

## Newly Authorized

- Treat the inferred-capture architecture as proven across Decision Lifecycle and Governance integrations.
- Preserve the current inferred-capture pattern: authoritative artifact persists first, reasoning fingerprints the source artifact or transition, then appends idempotent reasoning evidence.
- For operational-context promotion capture, avoid reasoning concepts such as `OperationalContextPromoted`, `OperationalContextApproved`, or `OperationalContextAccepted`.
- Model operational-context promotion by capturing the reasoning significance of the change, such as `AssumptionReplaced`, `ConstraintModified`, `DirectionShifted`, `EvidenceAdded`, or `DecisionEvolution`, depending on what changed.
- Treat the operational-context promotion artifact itself as provenance rather than as the reasoning event meaning.
- For operational-context promotion, make the reasoning event explain why project understanding changed rather than merely that promotion occurred.
- Continue treating capture selection quality, provenance quality, and event-family overuse as medium risks.
- Treat capturing workflow transitions instead of reasoning evolution as the highest current risk for remaining Milestone 2 capture integrations.
