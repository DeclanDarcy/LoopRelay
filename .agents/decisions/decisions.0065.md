# Decisions

## Newly Authorized

- Proceed with M5 Workstream 5.7 next, focused on Operational Context cross-links.
- Treat Operational Context cross-links as navigation-only: tab selection, section targeting, focus navigation, and view switching are allowed.
- Forbid Operational Context cross-links from generating, accepting, rejecting, promoting, refreshing projections, loading sessions, or invoking backend commands.
- Before closing M5, audit `OperationalContextTab` for no draft ownership, no readiness ownership, and no mutation ownership.
- Preserve the current M5 authority boundary: `OperationalContextTab` owns presentation/composition only, while `App.tsx` continues to own draft authority, readiness authority, workflow authority, mutation dispatch, refresh lifecycle, selected repository state, and backend command invocation.
- If Workstream 5.7 passes the navigation-only audit and no authority ownership leaked into `OperationalContextTab`, M5 is eligible for certification and closure.
