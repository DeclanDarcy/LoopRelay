# Handoff

## Slice Summary

- Completed Epic 2 M2C: failure projection and recovery visibility.
- Added a latest repository execution summary path separate from active-session lookup.
- Dashboard projections now include `executionSummary` while preserving `activeExecutionSession`.
- Workspace projections now return the latest session summary, including failed and recovered sessions.
- Provider launch failures remain visible even when the repository state returns to `Ready`.
- Orphan recovery failures remain visible when the repository state becomes `Failed`.
- UI now displays session id, provider name, provider executable path, PID, provider start time, start/activity times, state, repository state, and failure reason when available.
- Dashboard now shows failed-session summaries and failure reason text without requiring workspace drill-down.
- M2 milestone checklist now marks failure visibility complete and explicitly keeps live monitoring deferred to M3.

## Files Changed

- `.agents/milestones/m2-session-integration.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0007.md`
- `src/CommandCenter.Backend/Execution/IExecutionSessionService.cs`
- `src/CommandCenter.Backend/Execution/ExecutionSessionService.cs`
- `src/CommandCenter.Backend/Projections/RepositoryDashboardProjection.cs`
- `src/CommandCenter.Backend/Projections/RepositoryProjectionService.cs`
- `src/CommandCenter.UI/src/App.css`
- `src/CommandCenter.UI/src/App.tsx`
- `src/CommandCenter.UI/src/devTauriMock.ts`
- `tests/CommandCenter.Backend.Tests/ArtifactRotationServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionSessionServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/RepositoryProjectionServiceTests.cs`

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 82 tests.
- `npm run build --prefix src/CommandCenter.UI` passed.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## High-Leverage Decisions

- Keep `GetActiveSessionAsync` narrowly scoped to active execution only.
- Add `GetRepositorySessionSummaryAsync` for the latest session so failure metadata is visible without redefining active execution semantics.
- Preserve repository `Ready` after provider start failure while still surfacing the failed session summary for operator diagnosis.
- Keep M2C limited to projection and UI visibility; no monitoring, streaming, completion detection, or event model work was added.

## Recommended Next Slice

- Start M3: execution monitoring and observability.
- First M3 slice should add the backend event/status model and provider output capture behind the existing execution boundary.
- Keep SSE/UI streaming as the follow-up M3 slice after backend event retention and status projection are testable.
