# Decisions

## Newly Authorized

- Preserve the M3 invariant that `ReasoningGraph` is a derived projection, not a new authority layer.
- Keep reasoning graph rebuilds service-driven and do not introduce graph-specific caching in the Tauri or UI layers.
- Treat directional reasoning relationships as explanatory edges only, not workflow transitions, state machines, or authority paths.
- Treat references and provenance as evidence edges so graph traversal can explain why a reasoning event exists.
- Preserve unresolved graph references as diagnostics; do not silently omit unresolved nodes or treat absence as nonexistence.
- For the remaining M3 work, add Tauri commands for `get_reasoning_graph`, `trace_reasoning_backward`, and `trace_reasoning_forward`.
- Add thin UI API/types/hooks for `ReasoningGraph`, `ReasoningGraphNode`, `ReasoningGraphRelationship`, and `ReasoningTrace` without introducing client-side graph authority.
- Build `ReasoningGraphPanel` with accessible lists and tables first.
- Defer force-directed graphs, canvas graphs, node editors, and interactive diagrams until the accessible projection proves insufficient.
- Keep graph node identity limited to things that actually exist: reasoning events, reasoning threads, reasoning references, and provenance/evidence references.
- Do not introduce first-class `Hypothesis`, `Alternative`, `Contradiction`, `Direction`, `Assumption`, or `Constraint` graph nodes; those remain classifications, filters, and analytical projections.
