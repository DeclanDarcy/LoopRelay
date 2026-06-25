# Decisions

## Newly Authorized

- Continue Milestone 9 as targeted retirement work, removing redundant implementations now that shared explainability can carry the same information.
- Preserve backend ownership of quality signals and supporting evidence.
- Treat `decisionQualitySignalsToDiagnostics` as the single translation path for quality signals into presentation.
- Treat `DiagnosticList` as the single renderer for quality-signal diagnostics.
- Delete duplicate presentation paths without deleting domain framing.
- Retain thin wrappers when they provide navigation, ordering, grouping, contextual framing, or section composition while delegating evidence and diagnostics to shared explainability components.
- Continue verifying presentation retirements with focused characterization tests, adapter preservation tests, and a successful build.
- Next cleanup audit should cover decision influence and proposal-option transparency.
- Classify audited renderers as local evidence renderer, local diagnostics renderer, local fact chips, domain-specific visualization, or wrapper providing navigation/grouping/framing.
- Replace local evidence, diagnostics, and fact-chip renderers when shared explainability covers the same facts.
- Keep domain-specific visualizations and wrapper composition that add unique value.
- Treat remaining Milestone 9 cleanup as incremental deletion of obsolete renderers rather than new infrastructure creation.
