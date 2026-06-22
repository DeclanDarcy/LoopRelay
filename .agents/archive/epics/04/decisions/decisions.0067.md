# Decisions

## Newly Authorized

- Treat M6 as legitimately certified and closed.
- Treat completion of the four observational workspaces, Workspace, Execution, Operational Context, and Continuity, as a substantial architecture milestone because they now share the same boundary: feature tabs own composition/presentation/navigation surfaces while `App.tsx` retains workflow authority, backend dispatch, mutation logic, readiness, and lifecycle decisions.
- Preserve the highest-value Continuity invariant going into M7: Continuity shows evidence, diagnostics, and state; it must not introduce scores, readiness gates, workflow blocking, auto-correction, auto-promotion, auto-rejection, health scoring, confidence scoring, or quality scoring.
- Start M7 as a navigation, discovery, and cohesion milestone rather than an authority or workflow-expansion milestone.
- Treat the main M7 risk as navigation fragmentation across sidebar navigation, workspace tabs, cross-links, artifact links, command palette entries, and section anchors.
- M7 should produce a certified navigation/discovery model that standardizes workspace ids, section ids, anchor ids, cross-link targets, command palette targets, and artifact navigation targets.
- Preserve the M7 authority constraint that navigation is not workflow mutation.
- Command palette actions in M7 should continue to resolve only to navigation-oriented behavior such as navigate, focus, select, or scroll unless an explicit backend-owned workflow command is separately authorized.
- Certify M7 by checking discovery consistency, anchor consistency, cross-link consistency, and the invariant that navigation still works if backend mutation endpoints disappear.
