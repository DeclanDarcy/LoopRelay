# Decisions

## Newly Authorized

- Treat Milestone 2 as architecturally proven across Decision Lifecycle, Governance, and Operational Context capture integrations.
- Preserve the inferred-capture pattern for remaining integrations: authoritative transition persists first, reasoning fingerprints the source transition, then appends idempotent explanatory evidence.
- For execution handoff capture, avoid reasoning concepts such as `HandoffAccepted`, `HandoffRejected`, `ExecutionSessionAccepted`, or `ExecutionSessionRejected`.
- Model execution handoff acceptance or rejection by capturing the semantic meaning of the transition, such as `DirectionShifted`, `ConstraintModified`, `AssumptionReplaced`, `EvidenceAdded`, or `DecisionEvolution`, depending on what the handoff represents.
- Treat the execution handoff artifact itself as provenance rather than as the reasoning event meaning.
- For execution handoff capture, make the reasoning event explain why execution direction changed rather than merely that a handoff workflow action occurred.
- Continue prioritizing high-signal reasoning events over high-volume workflow captures.
- Treat semantic dilution from too many low-value captures as the highest current risk.
- Treat capture consistency across domains and provenance completeness as medium risks.
- Treat authority drift, materialization violations, thread semantics, governance ownership leakage, and operational-context ownership leakage as currently low risks.
