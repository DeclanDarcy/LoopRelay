# Handoff

## Slice Summary

- Executed Epic 2 M6.2 commit preparation.
- Added commit preparation domain models:
  - `CommitPreparation`
  - `CommitStatusSnapshot`
  - `CommitScopeItem`
  - `CommitChangeType`
  - `CommitChangeOrigin`
- Extended `IGitService` and `GitService` with `PrepareCommitAsync`.
- Commit preparation now:
  - refreshes Git status;
  - generates a deterministic proposed message from the milestone filename and changed path count;
  - builds one selected scope item per current changed path;
  - classifies scope item origin as `PreExisting` when the path existed in the pre-execution dirty snapshot;
  - assigns a SHA-256 status snapshot id derived from canonical status content.
- Added `CommitPreparation` persistence on `ExecutionSession`.
- Extended `IExecutionSessionService` and `ExecutionSessionService` with `PrepareCommitAsync`.
- Added backend endpoint `POST /api/execution-sessions/{sessionId}/git/prepare-commit`.
- Added Tauri command `prepare_commit` as a thin HTTP bridge.
- Updated React Git workflow:
  - `AwaitingCommit` now loads commit preparation instead of raw status buckets;
  - displays snapshot metadata, pre-existing-change presence, editable proposed message, selected count, and exact scope items;
  - renders per-path checkboxes plus `Select All` and `Select None`.
- Updated the dev Tauri mock to return commit preparation after accepting a mock handoff.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0020.md`.

## Files Changed

- `.agents/milestones/m6-git-lifecycle.md`
- `.agents/handoffs/handoff.0020.md`
- `.agents/handoffs/handoff.md`
- `src/CommandCenter.Backend/Execution/CommitChangeOrigin.cs`
- `src/CommandCenter.Backend/Execution/CommitChangeType.cs`
- `src/CommandCenter.Backend/Execution/CommitPreparation.cs`
- `src/CommandCenter.Backend/Execution/CommitScopeItem.cs`
- `src/CommandCenter.Backend/Execution/CommitStatusSnapshot.cs`
- `src/CommandCenter.Backend/Execution/ExecutionSession.cs`
- `src/CommandCenter.Backend/Execution/ExecutionSessionService.cs`
- `src/CommandCenter.Backend/Execution/GitService.cs`
- `src/CommandCenter.Backend/Execution/IGitService.cs`
- `src/CommandCenter.Backend/Execution/IExecutionSessionService.cs`
- `src/CommandCenter.Backend/Program.cs`
- `src/CommandCenter.Shell/src/main.rs`
- `src/CommandCenter.UI/src/App.css`
- `src/CommandCenter.UI/src/App.tsx`
- `src/CommandCenter.UI/src/devTauriMock.ts`
- `tests/CommandCenter.Backend.Tests/ArtifactRotationServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionContextServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionMonitoringEndpointTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionSessionServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/GitServiceTests.cs`

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 121 tests.
- `npm run build --prefix src/CommandCenter.UI` passed.
- `cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml` passed.
- `dotnet build CommandCenter.slnx` passed.

## New State

- M6.2 commit preparation is implemented and verified.
- M6 checklist now marks commit preparation endpoint, deterministic message, dirty snapshot comparison, scope item generation, status snapshot id, pre-existing marking, path selection controls, message editing, and commit scope display complete.
- Latest commit preparation is stored on the execution session for later stale-scope validation.
- No staging, commit mutation, push mutation, stale-scope rejection, commit failure retry, or push failure retry was implemented in this slice.

## Recommended Next Slice

- Execute M6.3 commit mutation.
- Add commit request models with reviewed message, selected paths, and reviewed status snapshot id.
- Validate session state, selected paths, empty selections, repository-relative path safety, and stale snapshot id before staging.
- Stage only selected paths and commit through `IGitService`/`IProcessRunner`.
- Persist commit sha and transition successful commits to `AwaitingPush`.
