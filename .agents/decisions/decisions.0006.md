# Decisions

## Newly Authorized

- Treat Milestone 1 implementation objectives as complete pending the remaining lifecycle timeline audit.
- Prioritize the parallel lifecycle timeline audit before shell command test feasibility.
- The lifecycle audit should inspect app-level summaries, repository summaries, workspace rails, execution views, dashboard widgets, and repository projections for any second lifecycle model expressing current stage, progression, workflow state, required action, blocked state, or operational status.
- Use this audit question as the governing test: if workflow disappeared, the UI should not still be capable of rendering an operational lifecycle timeline.
- After the audit, inspect shell test infrastructure and add workflow command tests only if existing infrastructure makes them natural.
- Do not force artificial shell command tests if they require disproportionate scaffolding; document the limitation instead.
- If the lifecycle audit is clean, treat Milestone 1 as complete and move into Milestone 2.
