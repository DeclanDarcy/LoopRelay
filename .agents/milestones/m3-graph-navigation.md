# Milestone 3: Derived Reasoning Graph Navigation

Goal: create a derived graph for navigation only. The graph is not persisted as authority.

## Backend Work

- [ ] Add `ReasoningGraph`, `ReasoningGraphNode`, `ReasoningGraphRelationship`, and `ReasoningTrace` models as derived read models.
- [ ] Implement `IReasoningGraphService`.
- [ ] Build graph nodes from events, threads, relationships, and external references.
- [ ] Build graph relationships from persisted relationships, event thread membership, references, and event provenance.
- [ ] Implement backward traceability.
- [ ] Implement forward impact traceability.
- [ ] Implement thread traversal.
- [ ] Add graph read endpoint.
- [ ] Add backward and forward trace endpoints.
- [ ] Keep graph rebuild in memory unless a later cache is justified.

## UI Work

- [ ] Add `ReasoningGraphPanel` with node filters, relationship filters, selected node details, backward trace, forward trace, and thread traversal.
- [ ] Use accessible lists/tables first; add visual graph rendering only if it remains readable and tested.

## Tests

- [ ] Graph nodes resolve or report missing external reference diagnostics.
- [ ] No orphan persisted reasoning relationships are produced.
- [ ] Backward trace for a decision can explain causes.
- [ ] Forward trace from a hypothesis event can show resulting alternatives, decisions, contradictions, or direction events.
- [ ] Thread traversal reconstructs event order.
- [ ] Graph output is reproducible from the same repository state.

## Exit Criteria

- [ ] Navigation is operational.
- [ ] Causal tracing is operational.
- [ ] Forward impact tracing is operational.
- [ ] Graph remains derived.
