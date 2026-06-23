# Milestone 3: Derived Reasoning Graph Navigation

Goal: create a derived graph for navigation only. The graph is not persisted as authority.

## Backend Work

- [x] Add `ReasoningGraph`, `ReasoningGraphNode`, `ReasoningGraphRelationship`, and `ReasoningTrace` models as derived read models.
- [x] Implement `IReasoningGraphService`.
- [x] Build graph nodes from events, threads, relationships, and external references.
- [x] Build graph relationships from persisted relationships, event thread membership, references, and event provenance.
- [x] Implement backward traceability.
- [x] Implement forward impact traceability.
- [x] Implement thread traversal.
- [x] Add graph read endpoint.
- [x] Add backward and forward trace endpoints.
- [x] Keep graph rebuild in memory unless a later cache is justified.

## UI Work

- [x] Add `ReasoningGraphPanel` with node filters, relationship filters, selected node details, backward trace, forward trace, and thread traversal.
- [x] Use accessible lists/tables first; add visual graph rendering only if it remains readable and tested.

## Tests

- [x] Graph nodes resolve or report missing external reference diagnostics.
- [x] No orphan persisted reasoning relationships are produced.
- [x] Backward trace for a decision can explain causes.
- [x] Forward trace from a hypothesis event can show resulting alternatives, decisions, contradictions, or direction events.
- [x] Thread traversal reconstructs event order.
- [x] Graph output is reproducible from the same repository state.

## Exit Criteria

- [x] Navigation is operational.
- [x] Causal tracing is operational.
- [x] Forward impact tracing is operational.
- [x] Graph remains derived.
