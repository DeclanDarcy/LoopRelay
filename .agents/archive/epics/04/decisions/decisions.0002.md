# Decisions

## Newly Authorized

- M0 Workstream 0.2 is authorized as a transport-only extraction.
- The M0.2 objective is to move transport ownership out of `App.tsx`, not to create better APIs, introduce React Query, reorganize projections, or move state.
- The intended dependency direction is `Backend Contracts -> src/types -> api -> hooks -> views`.
- `src/api` may depend on `src/types`.
- `src/types` must depend on nothing.
- `App.tsx` may depend on `src/api`.
- `src/api` must not depend on React.
- Tauri `invoke(...)` calls should leave `App.tsx` and move into framework-independent API modules such as `src/api/tauriClient.ts`.
- `EventSource` construction should leave `App.tsx` and move into a framework-independent execution event transport module such as `src/api/executionEvents.ts`.
- M0.2 must not move loading state, selection state, projection state, render state, caching, interpretation, or frontend workflow authority.
- M0.2 should avoid introducing a frontend service layer that interprets backend projections.

## Validation Expected For Next Slice

- Characterization should cover repository refresh preserving request, response handling, and render outcome.
- Characterization should cover execution event flow from event receipt to UI update, including connection open/message/reconnect/close behavior where applicable.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
