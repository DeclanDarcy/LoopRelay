# Decisions

## Newly Authorized

- Treat M0.6 as frontend-wide authority certification, not local behavior documentation.
- Use the invariant that navigation, draft editing, and projection loading are not workflow mutations; only explicit workflow actions may mutate workflow state.
- Treat the four currently characterized workflow domains as certified mutation systems: execution context, git workflow, operational-context proposals, and continuity reports.
- Perform a workflow-mutating backend command inventory next before adding more opportunistic characterization.
- Define M0.6 completion as every workflow-mutating backend command appearing in an authority matrix, every such command having characterization coverage, and every such command being certified to require an explicit user workflow action.
- If the inventory reveals only a small number of uncovered commands, finish M0.6 before returning to M0.5 decomposition.
- If the inventory shows coverage is largely complete, formally close Workstream 0.6 and record the resulting authority constitution as a primary M0 deliverable.
