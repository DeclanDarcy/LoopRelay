# Decisions

## Newly Authorized

- Treat Milestone 2 as complete, certified, and closed.
- Treat the current program status as M0, M1, and M2 complete/certified/closed, with M3 as the next active milestone.
- Frame M3 as Workspace Decomposition, not generic `App.tsx` size reduction.
- Before extracting M3 code, identify the coherent authority boundary being moved.
- Prefer boundaries such as Workspace Draft Authority, Workspace View Composition Authority, workspace-local drafts, workspace-local rendering, and workspace-local view composition.
- Do not extract miscellaneous `App.tsx` code solely because it is large.
- Continue protecting the M0 authority split during M3: shell owns navigation/composition only, the design system remains render-only, workflow mutations stay explicit and backend-owned, and readiness must not migrate independently of the workflow that owns it.
- Treat the central M3 certification question as: what authority does the new module own?
