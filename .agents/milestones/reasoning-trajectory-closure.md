# Reasoning Trajectory Preservation Closure Review

## Scope

This review closes the Reasoning Trajectory Preservation milestone set after Milestones 0 through 8 completed.

The review attempts to falsify the central architecture claim:

```text
Reasoning trajectory remains sufficient as events, threads, relationships, graph traversal, reconstruction, materialization review, and certification without first-class hypothesis, alternative, contradiction, direction, graph, query, or historical-state persistence.
```

## Closure Questions

| Question | Evidence | Closure Result |
| --- | --- | --- |
| Why did this decision replace an earlier decision? | `ReasoningCertificationService` certifies decision supersession through `CERT-100`; long-horizon tests recover decision replacement answers after repository reload. | Answerable from generic reconstruction. |
| Why was this alternative rejected? | `CERT-110` and long-horizon alternative queries cite rejected and selected alternative events plus comparison relationships. | Answerable without alternative entities. |
| What assumption failed? | `CERT-130` and long-horizon assumption queries cite invalidated assumption events and challenging contradiction evidence. | Answerable without assumption state persistence. |
| Which contradiction changed direction? | `CERT-120` and long-horizon contradiction queries cite recurring contradiction events leading to direction-shift evidence. | Answerable without contradiction entities. |
| How did current strategy emerge? | `CERT-140`, long-horizon direction queries, and materialization review scenarios reconstruct strategy from decision, alternative, contradiction, assumption, direction, thread, and relationship evidence. | Answerable without direction entities or persisted graph authority. |

## Falsification Review

### Historical Reconstruction Correctness

`ReasoningQuery.HistoricalAt` derives point-in-time state from event timelines and category filters. The implementation records historical diagnostics and explicitly states that historical state is derived, not persisted lifecycle authority.

No failure case was found that requires persisted historical state. The current risk is semantic precision of future historical questions, but that risk is handled by adding event evidence and query/reconstruction behavior, not by promoting historical state to authority.

### Reference Integrity

Certification fails missing required reasoning endpoints and reports unresolved external references as diagnostics. Relationship integrity is enforced through graph diagnostics and `CERT-020`.

No failure case was found that requires reasoning to own external domain artifacts.

### Recovery Determinism

Long-horizon tests rebuild graph and query results from repository artifacts after constructing a recovered repository and fresh reasoning services. Certification also survives fresh service graph reload and missing markdown projections because structured JSON remains the source.

No failure case was found that requires a persisted graph, query cache, or session continuity mechanism.

### Answerability Determinism

Certification runs repeated query reconstruction and compares deterministic evidence signatures through `CERT-040`. Reconstruction output is ordered and grouped for UI consumption.

No failure case was found that requires category-specific reconstruction engines or specialized read models.

### Authority Leakage

Tests assert no derived authority directories are created for hypotheses, alternatives, contradictions, directions, graphs, or queries. Materialization review remains advisory and can recommend persistence only when concrete reconstruction failures or workflow duplication are supplied.

No failure case was found that justifies first-class specialized persistence.

## Closure Decision

Reasoning Trajectory Preservation is closed.

No Milestone 9 is created because the closure review did not find a concrete architectural failure. Remaining risk is evolutionary: future use may reveal repeated reconstruction failures, excessive workflow duplication, or event taxonomy pressure. Those conditions should re-enter through materialization review rather than through speculative persistence.

## Follow-On Boundary

The next backlog item should move to Epic 6, Continuity Fidelity, only after this closure state is committed. Epic 6 should not reopen reasoning trajectory unless it discovers a concrete failure in transfer-success evidence that cannot be represented as reasoning events, relationships, reconstruction evidence, or certification diagnostics.
