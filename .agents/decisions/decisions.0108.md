# Decisions

## Newly Authorized

- Continue Milestone 9 cleanup under the existing separation: shared explainability components own generic rendering, and domain components own composition, comparison, navigation, lifecycle framing, summaries, metrics, and visualization.
- Treat the preferred presentation path as: backend projection, narrow adapter, shared `EvidenceList` or `DiagnosticList` or `HealthView` or `CertificationFindingsView`, domain wrapper, then domain-specific layout.
- Prefer narrow adapters over broadening an existing adapter when only a subset of a projection should render through a shared component.
- For the next slice, audit remaining health and certification renderers across workflow, governance, decision, reasoning, and repository summary surfaces.
- Replace renderers that only present health entries, certification findings, evidence, or diagnostics with `HealthView`, `CertificationFindingsView`, `EvidenceList`, or `DiagnosticList`.
- Keep domain-specific navigation, timelines, lifecycle framing, comparison, summaries, metrics, and visualization in their owning workspace components.
- Treat remaining Milestone 9 renderer cleanup as finite and mechanical unless a surface is found to be mixing authority or duplicating domain state.
