# Decisions

## Newly Authorized

- Continue Milestone 9 cleanup by emphasizing retirement of redundant presentation paths after replacement architecture has been validated.
- Use the retirement pattern of deleting duplicate renderers while preserving the underlying capability.
- Treat shared explainability adapters and components as the single authoritative presentation path, without changing backend semantic ownership.
- Keep negative architectural regression tests that prove retired implementation patterns do not reappear.
- For the next Decisions-domain cleanup audit, inventory recommendation explanation, quality explanation, burden explanation, and governance explanation components.
- Classify each audited decision presentation component as `Already using shared explainability`, `Thin wrapper over shared components`, `Local renderer duplicating shared diagnostics/evidence`, or `Domain-specific visualization`.
- Migrate local renderers that duplicate shared diagnostics or evidence.
- Retain thin wrappers over shared explainability components and domain-specific visualizations that add unique decision-domain information.
- Keep the primary Decisions workspace intact while reducing duplicated presentation logic.
- Treat the remaining Milestone 9 phase as final cohesion cleanup before Milestone 10 MVP closure.
