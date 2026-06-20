# Milestone M2 - Execution Session Integration

## Goal

Launch fresh execution sessions from resolved context.

Implement M2 in two internal phases so session persistence is stable before real provider process complexity is introduced.

## M2A - Session Store, Launch API, And Fake Provider

### Backend Work

- [x] Implement `ExecutionSessionService`.
- [x] Implement `IExecutionSessionStore`.
- [x] Implement a deterministic fake execution provider for tests and local certification.
- [x] Add launch and active session endpoints.
- [x] Validate context before launch, including context hard limits.
- [x] Allow launch from dirty repositories only after capturing the pre-execution dirty snapshot.
- [x] Capture previous current handoff snapshot before provider start.
- [x] Enforce one active session per repository.
- [x] Persist session records.
- [x] Transition repository execution state from `Ready` to `Executing`.
- [x] Handle context failure, hard-limit failure, fake provider start failure, and duplicate execution failure.
- [x] Do not launch Codex yet.

### UI Work

- [x] Add `Start Execution` action when repository readiness is `Ready`, a milestone is selected, context validation succeeds, context hard limits are not exceeded, and no active session exists.
- [x] Show dirty repository diagnostics before launch when applicable.
- [x] On launch success, show execution workspace with session id, selected milestone, started time, provider name, and state.
- [x] Show `Executing` in dashboard immediately.

### Tests

- [x] Ready repository launches with fake provider.
- [x] Missing plan blocks launch.
- [x] Missing milestone blocks launch.
- [x] Context hard-limit failure blocks launch.
- [x] Dirty repository launch succeeds and stores the pre-execution dirty snapshot.
- [x] Duplicate launch blocks.
- [x] Fake provider failure leaves repository ready and records failure details.
- [x] Active session restores after session store reload.
- [x] Dashboard and workspace projections restore active session state after session store reload.
- [x] Launch endpoint returns session metadata.

### Exit Criteria

- [x] Command Center can create and persist a fresh execution session through a fake provider.
- [x] Active session state persists across backend restart.
- [x] Dashboard and workspace show restored active session state from persisted session metadata.
- [x] Duplicate execution is blocked.
- [x] Real provider process launch is still deferred.

## M2B - Codex Provider, Prompt Construction, And Process Launch

### Backend Work

- [x] Implement initial `CodexExecutionProvider`.
- [x] Add provider process invocation through `IProcessRunner`.
- [x] Generate execution prompt from context.
- [x] Include context size diagnostics and dirty-state diagnostics in launch metadata.
- [x] Start the Codex process with repository root as the working directory.
- [x] Capture provider process id when available.
- [x] Persist prompt metadata without persisting secrets or full process environment.
- [x] Handle provider executable missing, process start failure, and immediate provider exit.
- [x] Implement startup recovery semantics for active persisted sessions:
  - [x] Treat Codex reattach as unsupported for this phase.
  - [x] Mark unrecoverable active sessions and repositories `Failed` with an explicit orphaned-process reason.

### UI Work

- [ ] Display provider name and process-start failure messages.
- [ ] Display orphaned-session failure details after backend restart when reattach fails.

### Tests

- [x] Prompt construction includes required artifacts, selected milestone, repository path, Git snapshot, dirty-state diagnostics, and handoff requirement.
- [x] Missing Codex executable fails with structured provider error.
- [x] Provider start failure leaves repository ready and records failure details.
- [x] Immediate provider exit records failure.
- [x] Startup reload marks unrecoverable active provider sessions and repositories failed.
- [x] Startup recovery leaves non-executing sessions unchanged.
- [x] Startup recovery preserves provider path, PID, and prompt metadata.

### Exit Criteria

- [x] Command Center can create a fresh Codex-backed execution session.
- [x] Prompt construction is backend-owned and deterministic.
- [x] Process launch failure and orphaned restart recovery are explicit.
- [ ] Live monitoring output is still completed in M3.
