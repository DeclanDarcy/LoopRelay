# Decisions

## Newly Authorized Decisions

- M2A.1 is accepted as correctly scoped session lifecycle skeleton work.
- Provider start failure should continue to record a failed historical session while returning the repository workflow state to `Ready`.
- The next slice is authorized as M2A.2: Execution Launch UX.
- M2A.2 should stay UI-focused and should not start Codex integration.
- Backend work in M2A.2 should avoid major architecture additions; only session projection refinements or launch validation DTO cleanup are authorized if discovered during UI integration.
- Tauri should add thin HTTP bridge commands for `start_execution`, `get_active_execution`, and `get_execution_session` if they are not already present.
- React launch authority should be gated by planning readiness, selected milestone, built context, no validation errors, no hard-limit failure, and no active session.
- Workspace projection should show session id, provider, started time, execution state, and repository execution state after launch.
- Dashboard projection should show executing status, active session id, and last updated/last activity metadata where available.
- Before M2B, add certification that an application restart or service reload restores session-store-backed dashboard projection showing the repository still executing.
- Do not add execution events, monitoring service behavior, SSE, or stdout/stderr capture in M2A.2.
- Do not add prompt construction, Codex provider, process launch, PID tracking, or restart recovery until M2B.

## Next Authorized Slice

- Proceed into M2A.2: Tauri execution commands, React start-execution workflow, launch gating from context diagnostics, active execution summary display, dashboard/workspace execution indicators, session detail loading, and persistence-backed UI restoration after refresh/restart.
