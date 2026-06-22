# Decisions

## Newly Authorized

- Treat M0, M1, M2, and M3 as closed.
- Start M4 with an inventory before implementation.
- Include current Execution tab surfaces, execution session state, execution events/feed, execution context panel, diagnostics, history, start/cancel/recovery controls, and commit/push adjacency in the M4 inventory.
- Separate Execution Workspace presentation and composition from backend-owned execution authority, React-owned draft/readiness state, and workflow command dispatch.
- Do not allow the Execution Workspace migration to make React the execution authority.
- Define the M4 target as a dedicated, coherent execution operational surface using existing hooks, projections, and backend commands.
