# Decisions

## Newly Authorized

- Continue Milestone 9 as presentation retirement work, not new UI infrastructure construction.
- Treat the `DecisionOptionComparison` cleanup as valid because it removes duplicate evidence presentation while preserving the detailed option-comparison workspace.
- Preserve the thin-wrapper boundary: shared explainability components own evidence and diagnostics rendering; domain components may keep layout, grouping, navigation, comparison structure, and framing.
- Continue auditing remaining decision explanation surfaces independently: recommendation explanation, burden explanation, and governance explanation.
- Classify remaining renderers as duplicated local evidence rendering, duplicated local diagnostics rendering, duplicated local fact-chip rendering, unique domain visualization or comparison layout, or wrapper providing grouping/navigation.
- Replace local evidence renderers duplicated by `EvidenceList`.
- Replace local diagnostics renderers duplicated by `DiagnosticList`.
- Replace local fact-chip renderers already represented in shared explainability components.
- Keep unique domain visualizations, comparison layouts, and wrappers that provide grouping or navigation.
- Treat remaining Milestone 9 work as identifying and removing the last redundant presentation paths.
