# Milestone M2 - Execution Session Integration

## Goal

Launch fresh execution sessions from resolved context.

Implement M2 in two internal phases so session persistence is stable before real provider process complexity is introduced.

## M2A - Session Store, Launch API, And Fake Provider

### Backend Work

- [ ] Implement `ExecutionSessionService`.
- [ ] Implement `IExecutionSessionStore`.
- [ ] Implement a deterministic fake execution provider for tests and local certification.
- [ ] Add launch and active session endpoints.
- [ ] Validate context before launch, including context hard limits.
- [ ] Allow launch from dirty repositories only after capturing the pre-execution dirty snapshot.
- [ ] Capture previous current handoff snapshot before provider start.
- [ ] Enforce one active session per repository.
- [ ] Persist session records.
- [ ] Transition repository execution state from `Ready` to `Executing`.
- [ ] Handle context failure, hard-limit failure, fake provider start failure, and duplicate execution failure.
- [ ] Do not launch Codex yet.

### UI Work

- [ ] Add `Start Execution` action when repository readiness is `Ready`, a milestone is selected, context validation succeeds, context hard limits are not exceeded, and no active session exists.
- [ ] Show dirty repository diagnostics before launch when applicable.
- [ ] On launch success, show execution workspace with session id, selected milestone, started time, provider name, and state.
- [ ] Show `Executing` in dashboard immediately.

### Tests

- [ ] Ready repository launches with fake provider.
- [ ] Missing plan blocks launch.
- [ ] Missing milestone blocks launch.
- [ ] Context hard-limit failure blocks launch.
- [ ] Dirty repository launch succeeds and stores the pre-execution dirty snapshot.
- [ ] Duplicate launch blocks.
- [ ] Fake provider failure leaves repository ready and records failure details.
- [ ] Active session restores after session store reload.
- [ ] Launch endpoint returns session metadata.

### Exit Criteria

- [ ] Command Center can create and persist a fresh execution session through a fake provider.
- [ ] Active session state persists across backend restart.
- [ ] Duplicate execution is blocked.
- [ ] Real provider process launch is still deferred.

## M2B - Codex Provider, Prompt Construction, And Process Launch

### Backend Work

- [ ] Implement initial `CodexExecutionProvider`.
- [ ] Add provider process invocation through `IProcessRunner`.
- [ ] Generate execution prompt from context.
- [ ] Include context size diagnostics and dirty-state diagnostics in launch metadata.
- [ ] Start the Codex process with repository root as the working directory.
- [ ] Capture provider process id when available.
- [ ] Persist prompt metadata without persisting secrets or full process environment.
- [ ] Handle provider executable missing, process start failure, and immediate provider exit.
- [ ] Implement startup recovery semantics for active persisted sessions:
  - [ ] Best-effort reattach when the provider process is alive and reattach is supported.
  - [ ] Otherwise mark the session and repository `Failed` with an explicit orphaned-process reason.

### UI Work

- [ ] Display provider name and process-start failure messages.
- [ ] Display orphaned-session failure details after backend restart when reattach fails.

### Tests

- [ ] Prompt construction includes required artifacts, selected milestone, repository path, Git snapshot, dirty-state diagnostics, and handoff requirement.
- [ ] Missing Codex executable fails with structured provider error.
- [ ] Provider start failure leaves repository ready and records failure details.
- [ ] Immediate provider exit records failure.
- [ ] Restart with reattachable provider process keeps session executing.
- [ ] Restart with missing or unrecoverable provider process marks session and repository failed.

### Exit Criteria

- [ ] Command Center can create a fresh Codex-backed execution session.
- [ ] Prompt construction is backend-owned and deterministic.
- [ ] Process launch failure and orphaned restart recovery are explicit.
- [ ] Live monitoring output is still completed in M3.
