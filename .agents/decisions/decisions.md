# Decisions

## Newly Authorized

- Continue the Milestone 9 cleanup direction where generic diagnostics flow through `DiagnosticList`, generic evidence through `EvidenceList`, health through `HealthView`, and certification findings through `CertificationFindingsView`.
- Preserve domain-specific visualization and composition in owning workspaces, including graphs, timelines, comparisons, operational dashboards, navigation, and domain summaries.
- Treat navigation affordances as interaction owned by domain wrappers, not explainability rendering owned by shared components.
- Use this cleanup path for the next slice: search remaining `.map(...)` renderers for evidence, diagnostics, findings, certification, and health collections, then classify each renderer as duplicate generic presentation, thin wrapper around shared component, domain visualization, or domain summary.
- Replace only duplicate generic presentation renderers; keep thin wrappers, domain visualizations, and domain summaries.
- Treat the remaining Milestone 9 work as final cleanup before Milestone 10: retire last duplicated presentation paths, validate shared explainability as the only generic rendering path, preserve intentional domain-specific composition, and prepare the final cohesion audit.
