# Decisions

## Newly Authorized

- Treat the selected repository summary as the product's operational dashboard and repository home rather than adding a separate dashboard destination.
- Preserve the dashboard as an observational composition layer over existing authoritative projections.
- Keep the dashboard compact and navigational across repository, workflow, execution, governance, operational context, reasoning, health, certification, and diagnostics.
- Do not introduce a dashboard-specific semantic model or dashboard-owned lifecycle authority.
- Treat the first full-suite smoke failure from the dashboard slice as a transient/flaky test unless a recurrence pattern emerges.
- Continue Milestone 9 by removing obsolete presentation now that navigation, explainability, interaction language, summary consolidation, and the dashboard are in place.
- Use the cleanup disposition `Primary`, `Contextual`, `Compatibility`, and `Duplicate` for remaining duplicate helpers and panels.
- Prioritize cleanup of workflow/status helpers, legacy contextual panels, duplicated summary widgets, and obsolete presentation helpers left behind after the explainability migration.
- Treat the remaining Milestone 9 work as redundancy removal and product polish rather than introducing new UI concepts.
