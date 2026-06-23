# Reasoning Capture Policy

Reasoning capture preserves explanatory history without moving authority away from the source domain where a transition occurred.

## Capture Modes

Manual capture records rationale supplied directly by a user. It is appropriate for context the system cannot infer from existing artifacts, such as why an unstated assumption mattered or why a path was rejected outside a formal proposal.

Assisted capture pre-populates source references and provenance from the current workflow, then asks a user for the missing rationale. It is appropriate when Command Center can identify relevant artifacts but cannot safely infer intent.

Inferred capture records objective reasoning events from authoritative source-domain transitions that already occurred. Examples include decision supersession, proposal resolution, governance report generation, operational-context promotion, handoff acceptance, and execution projection changes.

The expected maturity path is:

```text
Manual Capture -> Assisted Capture -> Inferred Capture
```

Inferred capture should become dominant for source-domain transitions the system can observe directly.

## User-Supplied Rationale And Inferred Transitions

User-supplied rationale is narrative evidence. It may explain motivations, tradeoffs, assumptions, and uncertainty that cannot be derived from file changes alone.

Inferred source-domain transitions are objective evidence. They may record that a proposal was resolved, a decision was superseded, a governance finding recurred, or an operational-context revision was promoted.

The two must remain distinguishable in provenance. An inferred transition must not pretend to know user rationale unless that rationale exists in the source artifact.

## Idempotency

Capture must be idempotent for inferred transitions. Re-running capture over the same source-domain transition must not create duplicate reasoning events or relationships.

Stable idempotency inputs should include:

- repository identity
- source artifact kind and path
- source artifact identifier
- source transition identifier or timestamp when available
- normalized source fingerprint
- event family and event type

When a source artifact changes, stale capture commands should be rejected or produce a new event only when the change represents new reasoning evidence.

## Provenance

Every captured event and relationship requires provenance. Provenance should identify whether the event came from manual capture, assisted capture, or inferred capture, and should include source references and fingerprints where possible.

## Boundaries

Capture may append reasoning records. It may not mutate decisions, operational context, governance reports, execution projections, handoffs, commits, or provider sessions.

Automatic capture from existing services should wait until event schema and idempotency rules are stable. Early integration should prefer explicit endpoints and user-triggered actions.
