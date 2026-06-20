# Decisions

## Newly Authorized Decisions

- M2 is functionally complete from an infrastructure standpoint after M2B.3 restart/orphan recovery.
- M2 must receive one final closeout slice before M3 begins.
- The next authorized slice is M2C: failure projection and recovery visibility.
- M2C must add UI visibility for launch and recovery failures.
- M2C must not add new backend lifecycle work.
- M2C must not add monitoring.
- M2C must not add streaming.
- M2C must not add completion detection.
- Dashboard must surface repository execution state: `Failed`, `Cancelled`, `Executing`, and `Ready`.
- Dashboard must surface a failure summary when one is present.
- Orphan recovery failure must be visible directly from the dashboard without drill-down.
- Workspace must surface session id, provider, PID, provider executable, started time, and failure reason when available.
- Workspace is the operational debugging view for execution-session metadata.
- M2 can be formally closed after M2C failure visibility is complete.
- M3 must not begin until M2 is formally closed.

## Certification Required

- Provider start failure UI test:
  - Launch.
  - Executable missing.
  - Session failed.
  - Repository ready.
  - Failure visible.
- Orphan recovery UI test:
  - Executing session exists.
  - Backend restart occurs.
  - Recovery runs.
  - Session and repository become failed.
  - Failure visible.
- Metadata preservation UI test:
  - PID is displayed after recovery failure.
  - Provider path is displayed after recovery failure.
  - Provider name is displayed after recovery failure.
  - Prompt metadata is displayed after recovery failure.

## Explicitly Deferred

- Do not start M3 during M2C.
- Do not add `ExecutionEvent`.
- Do not add `ExecutionStatus`.
- Do not add monitoring infrastructure.
- Do not add stdout capture.
- Do not add stderr capture.
- Do not add SSE streaming.
- Do not add event retention.
- Do not add completion detection.

## Next Authorized Slice

- Proceed with M2C failure projection and recovery visibility, then mark M2 closed.
