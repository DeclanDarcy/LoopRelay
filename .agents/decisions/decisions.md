# Decisions

## Newly Authorized

- Continue Milestone 9 as a retire-compatibility-presentation phase focused on auditing remaining legacy workflow and status helpers.
- Audit remaining uses of `RepositoryExecutionState` to distinguish execution-state display from workflow-state derivation.
- Keep uses of `RepositoryExecutionState` that only display execution state.
- Replace uses of `RepositoryExecutionState` that derive workflow state with authoritative workflow projection.
- Consolidate or remove UI paths that duplicate information already available from the Workflow domain.
- Apply the same audit to remaining rails, badges, and status widgets.
- Preserve the boundary that Execution may summarize workflow, but Execution must not compute workflow.
- Treat Workflow as the sole lifecycle authority.
- Continue preserving certification as observational: render findings, failures, and diagnostics without inferring repairs, lifecycle transitions, or workflow legality.
- Preserve the narrow adapter pattern: backend projection, small adapter, shared component, then domain panel.
- Keep domain-specific composition, navigation, comparisons, timelines, graphs, and summaries in the owning domain surfaces.
- Before Milestone 10, confirm every remaining duplicate presentation path has either been retired or intentionally retained.
