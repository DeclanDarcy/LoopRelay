# Decisions

## Newly Authorized

- Treat first-class navigation targets in `src/CommandCenter.UI/src/lib/navigation.ts` as the correct architectural layer for M7 navigation cohesion.
- Keep navigation targets projection-derived rather than UI-derived so discovery and navigation remain anchored to backend-owned facts.
- Continue using the command palette only for navigation-oriented outcomes: navigate, focus, scroll, or select.
- Treat Sidebar Discovery as another view of the shared navigation target model, not as a separate navigation system.
- Continue pushing M7 toward one destination registry with many entry points rather than multiple destination registries.
- Treat per-repository active primary tab preservation as valid shell navigation state and not workflow authority.
- Treat stable anchors for milestones, git workflow, and generated handoffs as navigation identity, not workflow identity.
- Make the next M7 slice a cross-workspace link audit followed by cohesion audit work across status language, interaction behavior, focus, keyboard behavior, and responsive behavior.
- Preserve the current App-level ownership of workflow authority, draft authority, readiness authority, and mutation authority while the extracted surfaces converge through shared navigation.
