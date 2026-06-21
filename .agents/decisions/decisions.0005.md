# Decisions

## Newly Authorized

- M0.3B should extract `useExecutionSession(repositoryId, sessionId)` next.
- Define the `useExecutionSession` responsibility boundary before implementation.
- `useExecutionSession` may own session projection access only: load session, refresh session, reattach session, loading state, error state, session projection, and recovery projection.
- `useExecutionSession` must not own workflow authority.
- `useExecutionSession` must not decide whether execution can or should run.
- `useExecutionSession` must not derive session completion, failure, health, workflow next steps, promotion decisions, or review decisions.
- Backend projections and UI composition remain responsible for workflow meaning.
- Characterize existing behavior before moving session behavior.
- Required M0.3B characterization includes session refresh preserving current observable behavior.
- Required M0.3B characterization includes reattachment from an existing session id preserving current behavior exactly.
- Required M0.3B characterization includes existing recovery semantics across process restart or projection refresh without improving or simplifying them.
- Required M0.3B characterization includes repository switching so repo A/session A and repo B/session B do not leak loading state, session state, or refresh state across repository boundaries.
- During M0.3B, resist moving session orchestration out of `App.tsx`.
- Continue using the rule: hooks own projection lifecycle; `App.tsx` owns workflow lifecycle.

## Validation Expected For Next Slice

- Add characterization before moving `useExecutionSession`.
- Keep `App.tsx` as owner of navigation state, draft state, workflow actions, workflow gating, and view composition.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
- `dotnet test CommandCenter.slnx`
