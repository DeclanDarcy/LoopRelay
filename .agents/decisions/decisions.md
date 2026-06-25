# Decisions

## Newly Authorized

- Continue Milestone 9 cleanup under the rule: share generic presentation and retain domain composition.
- Treat the following presentation path as the preferred shape for generic explainability surfaces: backend projection, adapter, shared `EvidenceList` or `DiagnosticList`, domain wrapper, then domain-specific visualization.
- Keep revision comparison, lifecycle summaries, conflict detail cards, timelines, graphs, and navigation wrappers domain-owned.
- Prefer the shared explainability layer whenever a component is only iterating over evidence, diagnostics, findings, certification items, or health entries.
- Use the next cleanup audit targets: `DecisionOptionEvaluationTable`, `DecisionProposalViewer`, `ReasoningReconstructionPanel`, and operational-context proposal/comparison panels.
- For those targets, migrate generic evidence, diagnostics, findings, certification, and health renderers to shared components.
- Preserve comparison matrices, reconstruction visualization, proposal structure, semantic relationships, and timeline composition as thin domain wrappers.
- Treat remaining Milestone 9 work as refinement: retire last duplicated renderers, preserve intentional domain-specific composition, perform the final cohesion audit, and prepare for Milestone 10.
