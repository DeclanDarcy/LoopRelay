# Decisions

## Newly Authorized

- Continue using the callback-removal heuristic as a formal late-M0.5 audit question: if every callback prop is deleted and the component still represents a coherent user-facing concept, it is a candidate extraction; if not, it is a workflow coordinator.
- Treat the selected repository summary extraction as confirmed late-M0.5 presentation-only work because repository identity, workspace facts, and projection-derived metadata remain meaningful without refresh, remove, save, rotate, execute, commit, push, proposal, or continuity callbacks.
- Classify the repository surface into three responsibility classes: repository identity/facts are safe extraction territory, repository navigation remains authority-adjacent, and repository workflow stays centralized.
- Treat `RepositoryDashboardItemContent` and `SelectedRepositorySummary` as sufficient evidence that the repository surface has been appropriately harvested for now.
- Audit the artifact editor before operational-context proposal review because it has a higher probability of containing isolated presentation islands.
- In the artifact editor audit, look for one or two small read-only display clusters such as artifact metadata, path/status/category display, diagnostics, timestamps, or preview sections.
- Keep save, rotate, dirty tracking, draft ownership, editor state, validation readiness, and mutation gating in `App.tsx` unless a focused audit proves a narrower presentation boundary.
- Approach operational-context proposal review more cautiously because proposal loading, draft ownership, review notes, comparison rendering, promotion, accept/reject, and generation are likely more coupled.
- Prefer deliberate ownership boundaries over extracting every JSX block; avoid pressure to keep extracting once presentation-only candidates run out.
