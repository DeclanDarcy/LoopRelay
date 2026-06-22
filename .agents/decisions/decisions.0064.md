# Decisions

## Newly Authorized

- Treat M4 as closed.
- Preserve the M4 Execution boundary: `ExecutionTab` owns composition and presentation, while `App.tsx` and backend-owned surfaces retain workflow authority, readiness authority, and mutation authority.
- Preserve the existing `useExecutionEvents` event stream as the single event source; do not introduce client replay, client persistence, client event caching, or a secondary event store.
- Source Execution stream provider, status, and session id from the projected session model rather than inferring them from execution events.
- Keep Abort absent unless and until a backend-owned abort capability exists.
- Keep Execution cross-links navigation-only; they must not mutate workflow state.
- Begin M5 with an Operational Context inventory before extraction.
- Treat Operational Context as the most authority-sensitive remaining surface because proposal generation, review, comparison, acceptance, rejection, promotion, draft ownership, and readiness ownership are tightly coupled.
- For M5, separate presentation/composition from authority before implementation.
- Ensure `OperationalContextTab` owns composition and presentation only; proposal decisions and workflow authority must remain with `App.tsx` and backend-owned commands/projections.
