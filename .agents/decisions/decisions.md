# Decisions

## Newly Authorized

- Treat Workstream 8.8 as a responsibility inventory rather than a size audit of `App.tsx`.
- Classify each major `App.tsx` block as either authority, presentation/candidate extraction, or deviation.
- Preserve `App.tsx` code that is genuinely authority-centric, including backend dispatch, workflow decisions, draft ownership, readiness ownership, proposal review decisions, promotion, commit, and push.
- Treat layout helpers, display formatting, view composition, navigation glue, and section construction in `App.tsx` as candidate extraction areas when they can move without transferring authority.
- Record an intentional deviation instead of extracting when shrinking `App.tsx` would move workflow ownership, draft ownership, or readiness ownership into feature surfaces.
- Continue treating `devTauriMock.ts` as certification infrastructure while it consumes shared projection contracts rather than duplicating DTOs.
- Resolve the `App.tsx` question only after UX validation across Workspace, Execution, Operational Context, Continuity, the navigation registry, command palette, and discovery surfaces.
- Close M8 only if remaining `App.tsx` code is authority-centric, remaining deviations are documented, remaining capability gaps are backend-owned, and UX validation passes.
- Keep M8 open if `App.tsx` still contains presentation or composition responsibilities that can be extracted without moving authority.
