# Decisions

## Newly Authorized

- Treat the governance cleanup architecture as correct: backend projection to governance explainability adapter to shared `EvidenceList` or `DiagnosticList` to `GovernanceWorkspace`.
- Continue assigning shared explainability components ownership of evidence rendering, diagnostic rendering, findings, health presentation, and certification presentation.
- Continue assigning thin governance and domain wrappers ownership of lifecycle grouping, timeline context, transfer context, navigation, and status summaries.
- Preserve lineage and artifact summary lists when they communicate compact domain-specific governance state rather than duplicating generic evidence or diagnostic rendering.
- Continue the remaining Milestone 9 cleanup using this disposition: replace local evidence, diagnostics, certification, and health renderers when duplicated by shared components; keep timeline, evolution, semantic diff, provenance, graph visualization, domain grouping, and navigation.
- Use the question "Does this renderer present unique domain semantics, or is it only another way of rendering facts the shared layer already knows how to display?" as the criterion for retiring remaining renderers.
- Treat Milestone 9 as final cleanup: retire remaining duplicate renderers, preserve domain-specific composition, complete the cohesion audit, then prepare for Milestone 10.
