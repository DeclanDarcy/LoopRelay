# Decisions

## Newly Authorized

- Treat Milestone 0 as genuinely complete because it certified authority ownership, not because `App.tsx` became small or every possible hook/component extraction was pursued.
- Preserve the M0 authority model as the baseline for later work: `types` own DTO authority, `api` owns transport authority, hooks own read-only projection authority, `shellState` owns navigation authority, feature components own presentation authority, and `App.tsx` currently owns workflow, draft, readiness, and mutation authority.
- Do not revisit M0 decisions during Milestone 1.
- Begin Milestone 1 from the assumption that M0 authority boundaries are certified.
- Allow Milestone 1 to change appearance, layout, composition, tokens, typography, spacing, color system, surfaces, panels, interaction styling, and shared primitives.
- Do not use Milestone 1 to relocate workflow authority, migrate authority boundaries, restructure hooks, redesign projections, redesign navigation ownership, or change backend ownership.
- Keep design primitives render-only. They must not become smart workflow components.
- Keep workflow decisions in workflow surfaces, not in design primitives such as cards, badges, panels, or status components.
- Reopen M0 authority boundaries only if a future milestone explicitly authorizes an ownership change.
