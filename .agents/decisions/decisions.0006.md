# Decisions

## Newly Authorized

- M0.3C should extract `useExecutionEvents(sessionId)` next.
- Define the `useExecutionEvents` responsibility boundary before implementation.
- `useExecutionEvents` may own event subscription, unsubscription, reconnect behavior, event ordering, duplicate replacement, event storage, and event exposure.
- `useExecutionEvents` must expose raw ordered events, not the meaning or interpretation of those events.
- `useExecutionEvents` must not own execution state machine decisions.
- `useExecutionEvents` must not derive workflow phase, milestone state, completion, readiness, session authority, or status authority.
- Preserve the event authority model: backend event stream -> `useExecutionEvents` -> raw ordered events -> `App.tsx`.
- Required M0.3C characterization includes event merge ordering.
- Required M0.3C characterization includes duplicate sequence replacement.
- Required M0.3C characterization includes SSE cleanup on session change and unmount.
- Required M0.3C characterization includes silent status-refresh recovery behavior.
- Required M0.3C characterization includes session boundary isolation: switching from session A to session B removes events A from the active event view and activates only events B.
- After M0.3C, perform an architecture review of remaining `App.tsx` responsibilities without refactoring them yet.
- The post-M0.3C architecture review should classify remaining `App.tsx` responsibility into navigation, draft state, workflow actions, workflow gating, view composition, and presentation.

## M0 Exit Criteria Adjustment

- M0 should close only when test infrastructure is established.
- M0 should close only when DTO authority is centralized.
- M0 should close only when transport authority is centralized.
- M0 should close only when core projection hooks are extracted.
- M0 should close only when session hooks are extracted.
- M0 should close only when event hooks are extracted.
- M0 should close only when characterization coverage protects preserved behavior.
- M0 should close only when `App.tsx` no longer owns transport.
- M0 should close only when `App.tsx` no longer owns projection retrieval.
- M0 should close only when navigation, projection, and draft separation is maintained.

## Validation Expected For Next Slice

- Add characterization before moving event-stream behavior.
- Keep `App.tsx` as owner of workflow lifecycle and event interpretation.
- Keep `useExecutionSession` as status/session projection access only.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`
