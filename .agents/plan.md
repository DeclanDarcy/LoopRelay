# Command Center Epic 2 Implementation Plan

## Objective

Transform Command Center from a repository and artifact management application into an autonomous execution system.

At completion, Command Center must be able to:

- Resolve execution-ready context for a registered repository.
- Launch a fresh execution session for a selected milestone.
- Stream and display execution output.
- Track execution state and activity.
- Validate execution completion through a generated handoff.
- Present the handoff for user review.
- Accept or reject completed execution.
- Prepare, commit, and push repository changes.
- Return the repository to a ready state for the next execution.

The completed execution loop is:

```text
Resolve Context
Start Fresh Session
Monitor Execution
Validate Handoff
Review Handoff
Accept
Commit
Push
Ready For Next Execution
```

This epic does not implement decision sessions, session routing, operational context consolidation, long-term continuity, automatic milestone progression, or project understanding transfer.

## Current Codebase Baseline

Command Center currently has:

- A .NET backend sidecar in `src/CommandCenter.Backend`.
- A React/TypeScript UI in `src/CommandCenter.UI`.
- A Rust/Tauri desktop shell in `src/CommandCenter.Shell`.
- Backend tests in `tests/CommandCenter.Backend.Tests`.
- Repository registration, removal, dashboard projection, and workspace projection.
- Artifact discovery, load, save, and rotation for current and historical handoffs and decisions.
- Planning readiness based on `.agents/plan.md` and `.agents/milestones/*.md`.
- A Tauri bridge that forwards UI commands to backend HTTP endpoints.

Existing canonical repository-owned artifact layout:

```text
<repository>/
  .agents/
    plan.md
    operational_context.md
    milestones/
      *.md
    handoffs/
      handoff.md
      handoff.0001.md
      handoff.0002.md
    decisions/
      decisions.md
      decisions.0001.md
      decisions.0002.md
```

Epic 2 adds execution capability without replacing this filesystem authority model.

## Architectural Principles

### Disposable Execution Sessions

Every execution session is disposable, fresh, and single purpose.

Execution sessions are workers. They perform one slice of work, generate a handoff, and terminate. They must not be reused, resumed as continuity carriers, or treated as project memory.

### Repository Filesystem Remains Authority

Plan, milestones, decisions, handoffs, and code changes remain in the repository filesystem.

Command Center may persist local app metadata for execution sessions and provider process state, but repository-owned work artifacts remain under the repository root.

### Backend Owns Execution Business Logic

The .NET backend owns:

- Context resolution.
- Execution state transitions.
- Provider abstraction.
- Session persistence.
- Monitoring event ingestion.
- Handoff validation and rotation.
- Git status, commit, and push operations.
- Dashboard and workspace projections.

React owns presentation state only.

Rust/Tauri owns desktop lifecycle, native dialogs, process startup for the backend sidecar, and IPC bridging. It should not implement execution business rules.

### Provider Isolation

Command Center must not be hard-coded to one execution provider implementation.

Introduce an execution provider boundary with a Codex provider as the first implementation. The provider implementation may use a configured Codex executable from `COMMAND_CENTER_CODEX_PATH` or the system `PATH`, but all provider-specific command invocation details remain behind backend interfaces.

### Observation Is Not Interpretation

Monitoring reports what occurred:

- Output received.
- Last activity time.
- Provider completed.
- Provider failed.
- Provider cancelled.

Monitoring must not infer quality, intent, confidence, health, confusion, or next steps.

### One Active Session Per Repository

A repository may have zero or one active execution session.

Starting a second session while one is active must fail with a clear duplicate execution error.

### Handoff Is The Completion Artifact

Provider completion alone is not successful execution.

Successful execution requires a current handoff at:

```text
.agents/handoffs/handoff.md
```

If the provider reports completion and no handoff can be validated, the execution fails.

## Target Backend Structure

Add an `Execution` feature area:

```text
src/CommandCenter.Backend/
  Execution/
    ExecutionState.cs
    RepositoryExecutionState.cs
    ExecutionRecoveryPolicy.cs
    ExecutionSession.cs
    ExecutionSessionSummary.cs
    ExecutionContext.cs
    ExecutionContextArtifact.cs
    ExecutionContextDiagnostics.cs
    ExecutionContextSizePolicy.cs
    ExecutionRepositorySnapshot.cs
    RepositoryDirtyState.cs
    ExecutionEvent.cs
    ExecutionStatus.cs
    ExecutionPrompt.cs
    ExecutionStartRequest.cs
    ExecutionAcceptanceRequest.cs
    CommitRequest.cs
    CommitScopeItem.cs
    CommitScopeSelection.cs
    PushRequest.cs
    IExecutionContextService.cs
    ExecutionContextService.cs
    IExecutionSessionService.cs
    ExecutionSessionService.cs
    IExecutionSessionStore.cs
    FileSystemExecutionSessionStore.cs
    IExecutionProvider.cs
    CodexExecutionProvider.cs
    IExecutionMonitoringService.cs
    ExecutionMonitoringService.cs
    IHandoffService.cs
    HandoffService.cs
    IGitService.cs
    GitService.cs
    CommitMessageService.cs
    IProcessRunner.cs
    ProcessRunner.cs
```

Keep low-level filesystem access behind existing `IArtifactStore` where possible. Use `IProcessRunner` for Git and provider command execution so tests do not shell out.

## State Models

Use separate session lifecycle and repository workflow states so provider completion is not confused with user acceptance or Git workflow.

### ExecutionSessionState

```csharp
public enum ExecutionSessionState
{
    Created,
    Executing,
    Completed,
    Failed,
    Cancelled
}
```

Meaning:

- `Created`: session record exists but provider has not started.
- `Executing`: provider process is running or active.
- `Completed`: provider reported completion.
- `Failed`: provider failed, completion validation failed, or lifecycle processing failed.
- `Cancelled`: provider or user cancellation was observed.

### RepositoryExecutionState

```csharp
public enum RepositoryExecutionState
{
    Ready,
    Executing,
    AwaitingAcceptance,
    Accepted,
    AwaitingCommit,
    AwaitingPush,
    Failed,
    Cancelled
}
```

Meaning:

- `Ready`: repository can launch the next execution.
- `Executing`: an active session owns execution for the repository.
- `AwaitingAcceptance`: execution completed and a handoff was validated.
- `Accepted`: user accepted the handoff and Git preparation can begin.
- `AwaitingCommit`: accepted work has uncommitted repository changes.
- `AwaitingPush`: commit succeeded and push is still required.
- `Failed`: execution, handoff validation, commit, or push failed.
- `Cancelled`: execution was cancelled.

Keep existing `ExecutionReadiness` for plan and milestone readiness:

```text
MissingPlan
MissingMilestones
Ready
```

Dashboard and workspace projections must show both planning readiness and repository execution state.

## Local Persistence

Add `IExecutionSessionStore` backed by a JSON file under the Command Center app data directory:

```text
%APPDATA%/CommandCenter/execution-sessions.json
```

Persist:

- Session id.
- Repository id.
- Repository path at launch.
- Selected milestone relative path.
- Started time.
- Completed time.
- Last activity time.
- Session state.
- Repository execution state.
- Provider name.
- Provider process id when available.
- Failure reason when available.
- Token usage when available.
- Handoff path when available.
- Git commit sha when available.
- Push result when available.

Do not persist full repository contents. Persist output events only as an optional bounded event history for review and diagnostics. The event history may be truncated by count or size.

## Restart And Orphan Recovery Policy

Session recovery must have explicit semantics.

When the backend starts, `ExecutionSessionService` loads persisted sessions and evaluates any session whose repository execution state is `Executing`.

Recovery behavior:

- If the provider process id is present and the process is still alive, attempt best-effort reattach through the provider.
- If reattach succeeds, keep the session in `Executing`, resume monitoring, and append a recovery event.
- If the process is gone, cannot be identified, or cannot be reattached, mark the session `Failed`.
- The repository execution state also becomes `Failed`.
- The failure reason must say that the active provider process could not be reattached after backend restart.
- Do not silently mark an orphaned session as completed.
- Do not leave an unrecoverable session indefinitely in `Executing`.

Best-effort reattach is provider-specific. The first Codex provider implementation may only support reattaching when the process remains alive and its output stream can still be observed. If that cannot be guaranteed, failing explicitly is the correct behavior.

Sessions in `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Ready`, `Failed`, or `Cancelled` are restored from persisted metadata without provider reattach.

## Execution Context Package

`ExecutionContextService` builds a deterministic execution package from a registered repository and selected milestone.

Required inputs:

- Registered repository.
- `.agents/plan.md`.
- Selected `.agents/milestones/*.md`.
- Repository snapshot.

Optional inputs:

- `.agents/handoffs/handoff.md`.
- `.agents/decisions/decisions.md`.

Context package fields:

- Repository id, name, and path.
- Plan artifact path, name, content, and byte count.
- Selected milestone path, name, content, and byte count.
- Current handoff path, name, content, and byte count when present.
- Current decisions path, name, content, and byte count when present.
- Git branch.
- Git status summary.
- Generated timestamp.
- Validation results.
- Missing optional artifacts.
- Total context size in characters and bytes.
- Context warning and hard-limit diagnostics.
- Pre-execution dirty working-tree snapshot.

Validation rules:

- Repository must be registered and available.
- Planning readiness must be `Ready`.
- Plan must exist.
- Selected milestone must exist.
- Selected milestone path must stay within `.agents/milestones`.
- Missing handoff is allowed.
- Missing decisions is allowed.
- Git snapshot failure blocks launch but may still be displayed during context preview as a validation error.
- Dirty working tree does not block launch, but must be reported and captured before launch.
- Context hard-limit failure blocks launch, but the preview still displays diagnostics.

The context package must be reviewable in the UI before launch.

## Context Size Policy

Execution context must have explicit size limits from the first implementation.

`ExecutionContextDiagnostics` must include:

```csharp
public long TotalBytes { get; init; }
public long TotalCharacters { get; init; }
public long WarningThresholdBytes { get; init; }
public long HardLimitBytes { get; init; }
public bool WarningThresholdExceeded { get; init; }
public bool HardLimitExceeded { get; init; }
public IReadOnlyList<ExecutionContextArtifactDiagnostic> ArtifactDiagnostics { get; init; }
```

Initial default thresholds:

```text
Warning threshold: 128 KiB total context bytes
Hard limit:        512 KiB total context bytes
Artifact warning:  96 KiB per artifact
Artifact hard:     256 KiB per artifact
```

Rules:

- Context preview is always allowed, even when limits are exceeded.
- Launch is blocked when `HardLimitExceeded` is true.
- Warning threshold excess is visible but does not block launch.
- Threshold values must be centralized in `ExecutionContextSizePolicy`.
- The UI must show which artifact or aggregate size caused the warning or hard failure.
- The backend must return a structured validation error instead of truncating silently.

Do not implement summarization or automatic pruning in this epic. If context is too large, the user must revise the repository artifacts before launch.

Certification must record observed context sizes, largest artifact size, and warning or hard-limit frequency for the repositories used in test runs. Threshold tuning is allowed later only as an explicit policy change, not as hidden runtime behavior.

## Dirty Repository Policy

Command Center must behave deterministically when a repository has local changes before execution starts.

Epic 2 policy:

```text
Allow launch
Show diagnostics
Capture pre-execution snapshot
Require commit review before publication
```

Launch is allowed when the repository is dirty, including modified, added, deleted, renamed, untracked, or staged files.

Before launching, `ExecutionContextService` captures:

- Current branch.
- Staged paths.
- Modified paths.
- Deleted paths.
- Renamed paths.
- Untracked paths.
- Whether the working tree was clean.
- Snapshot timestamp.

The context preview and launch confirmation must display this dirty-state snapshot.

Commit ownership rules:

- The commit review screen must distinguish files that were dirty before execution from files that appeared or changed after execution when Git status data allows that comparison.
- Pre-existing dirty files are eligible for commit only after the user sees them in the commit review.
- Command Center must not assume every changed file was produced by the execution session.
- Every changed file must be individually selectable in the commit review.
- `Select All` may be the default, but the user must be able to remove individual files from the commit scope.
- The backend commit request must contain the explicit selected repository-relative paths.
- Command Center must never stage paths that were not displayed and selected for the reviewed commit scope.

This policy avoids blocking legitimate local work while preserving user control over what gets published.

## Prompt Construction

`ExecutionSessionService` constructs the execution prompt from the resolved context.

Prompt requirements:

- Identify the repository path.
- Identify the selected milestone.
- Include the plan content.
- Include selected milestone content.
- Include current handoff content when present.
- Include current decisions content when present.
- Include repository branch and Git status summary.
- Include dirty working-tree diagnostics when present.
- Instruct the provider to work only in the selected repository.
- Instruct the provider to produce or update `.agents/handoffs/handoff.md` before completing.
- Instruct the provider not to commit or push changes.
- Instruct the provider to leave completion, acceptance, commit, and push control to Command Center.

The prompt must be generated by backend code, not by the UI.

## Handoff Preservation Design

Because the provider writes the canonical current handoff path, `HandoffService` must preserve the previous current handoff safely.

Launch-time behavior:

- If `.agents/handoffs/handoff.md` exists, capture its content and metadata in the session record before provider start.
- Do not require the provider to know historical numbering.

Completion behavior:

- Validate that `.agents/handoffs/handoff.md` exists after provider completion.
- If a previous current handoff snapshot was captured and the current handoff content differs, write the previous snapshot to the next available `.agents/handoffs/handoff.NNNN.md`.
- If no previous handoff existed, no historical handoff is created.
- If historical write fails, mark execution failed and keep the generated current handoff in place.
- Refresh repository projections after handoff lifecycle processing.

This avoids losing the previous handoff when the provider overwrites `handoff.md`.

## Git Automation Design

Add `IGitService` with process-backed implementation.

Capabilities:

- Get current branch.
- Get status with modified, added, deleted, renamed, untracked, and staged paths.
- Detect clean working tree.
- Generate a conservative proposed commit message from session metadata, milestone name, and changed path counts.
- Build an individually selectable commit scope from displayed Git status paths.
- Stage only selected repository-relative paths.
- Commit with a reviewed message.
- Push the current branch to its configured upstream.

Command execution rules:

- Use `ProcessStartInfo` with argument lists, not shell command strings.
- Set working directory to the repository root.
- Capture stdout, stderr, exit code, and duration.
- Treat non-zero exit as structured failure.
- Never run `git reset --hard`, force push, checkout, rebase, or destructive cleanup as part of this epic.

Default commit behavior:

- Show changed paths before commit.
- Allow the user to review and edit the proposed commit message.
- Display whether the repository had pre-existing dirty files before execution launch.
- Make the per-file commit scope explicit before staging.
- Preselect all displayed changes by default, but allow each file to be deselected.
- Commit action stages selected files with `git add -A -- <selected paths>` and then runs `git commit`.
- Reject commit requests with no selected paths unless the working tree is clean and the user is marking the execution ready without commit.
- Reject commit requests containing paths outside the current displayed Git status.
- If the working tree is clean after acceptance, allow the user to mark the execution ready without commit.
- Push action runs `git push` for the current branch and reports upstream errors clearly.

Commit message policy:

- Do not generate an AI-written narrative summary.
- Use deterministic text based on milestone name and changed-file counts.
- Initial format:

```text
<milestone name>

- <N> files changed
```

- If the milestone name is empty, use `Execute selected milestone`.
- The user may edit the message before commit.
- Backend tests must verify the generated message is deterministic.

Git automation remains part of this epic because the target lifecycle returns repositories to a ready state for the next execution after publication. It is isolated in M6 so M0 through M5 can be certified without commit/push automation if execution, monitoring, handoff validation, and acceptance need to stabilize first.

## Backend API Surface

Extend `Program.cs` with endpoints grouped by repository.

Context endpoints:

```text
GET  /api/repositories/{repositoryId}/execution/context?milestonePath=...
POST /api/repositories/{repositoryId}/execution/context
```

The `GET` endpoint previews context. The `POST` endpoint may rebuild and persist launch-ready context diagnostics if needed.

Execution endpoints:

```text
POST /api/repositories/{repositoryId}/execution/start
GET  /api/repositories/{repositoryId}/execution/active
GET  /api/execution-sessions/{sessionId}
GET  /api/execution-sessions/{sessionId}/status
GET  /api/execution-sessions/{sessionId}/events
POST /api/execution-sessions/{sessionId}/cancel
POST /api/execution-sessions/{sessionId}/complete
```

`/events` streams server-sent events for live output. It also supports returning recent retained events for restart recovery.

Handoff and acceptance endpoints:

```text
GET  /api/execution-sessions/{sessionId}/handoff
POST /api/execution-sessions/{sessionId}/accept
POST /api/execution-sessions/{sessionId}/reject
```

Git endpoints:

```text
GET  /api/repositories/{repositoryId}/git/status
POST /api/execution-sessions/{sessionId}/git/prepare-commit
POST /api/execution-sessions/{sessionId}/git/commit
POST /api/execution-sessions/{sessionId}/git/push
```

`CommitRequest` must include:

- Reviewed commit message.
- Explicit selected repository-relative paths.
- The Git status version or snapshot id used for review.

The backend must reject stale or invalid commit scopes when selected paths no longer match the current status snapshot.

Projection updates:

- Add `executionState` to `RepositoryDashboardProjection`.
- Add `activeExecutionSession` or `executionSummary` to dashboard projection.
- Add `executionState`, `executionSummary`, and selected session details to `RepositoryWorkspaceProjection`.
- Keep existing planning readiness fields.

Register new services in `Program.CreateApp`.

## Tauri Shell Updates

Extend `src/CommandCenter.Shell/src/main.rs` with typed request and response structs for new backend projections.

Add Tauri commands:

- `get_backend_url`.
- `preview_execution_context`.
- `start_execution`.
- `get_active_execution`.
- `get_execution_session`.
- `get_execution_status`.
- `load_execution_handoff`.
- `accept_execution_handoff`.
- `reject_execution_handoff`.
- `get_git_status`.
- `prepare_commit`.
- `commit_execution`.
- `push_execution`.

React should use `get_backend_url` and browser `EventSource` for `/api/execution-sessions/{sessionId}/events`. Add backend CORS support for the Tauri production origin and local Vite development origin so server-sent events work in dev and packaged builds.

Keep Rust command logic as HTTP bridging only.

## UI Plan

Refactor the current large `App.tsx` as execution features are added.

Suggested structure:

```text
src/CommandCenter.UI/src/
  api.ts
  types.ts
  App.tsx
  components/
    RepositoryDashboard.tsx
    RepositoryWorkspace.tsx
    ArtifactWorkspace.tsx
    ExecutionContextPanel.tsx
    ExecutionWorkspace.tsx
    ExecutionStream.tsx
    HandoffReview.tsx
    GitWorkflow.tsx
```

The UI should remain a dense operational workspace, not a landing page.

Dashboard additions:

- Planning readiness.
- Execution state.
- Active session id when executing.
- Last activity when available.
- Awaiting acceptance indicator.
- Awaiting commit indicator.
- Awaiting push indicator.

Repository workspace additions:

- Milestone selector.
- Execution context preview action.
- Context diagnostics panel.
- Start execution action.
- Current execution summary.
- Handoff review access.
- Git status and commit/push controls when applicable.

Execution workspace:

- Repository name and path.
- Selected milestone.
- Session id.
- Started at.
- State.
- Last activity.
- Completion time.
- Duration.
- Token usage when reported.
- Chronological output stream.
- Failure or cancellation reason when present.

Handoff review:

- Display the complete generated handoff.
- Display execution metadata.
- Provide `Accept Handoff` and `Reject Handoff`.
- Do not summarize or reinterpret handoff content.

Git workflow:

- Display modified, added, deleted, renamed, untracked, and staged files.
- Mark paths that were already dirty before execution when known.
- Provide per-file selection controls plus `Select All` and `Select None`.
- Display deterministic proposed commit message in an editable field.
- Display the exact commit scope before staging.
- Provide commit action after user review.
- Provide push action after commit.
- Display commit sha and push result.

## Milestone M0 - Execution Architecture Ratification

(See ./milestones/m0-architecture.md)

## Milestone M1 - Execution Context Resolution

(See ./milestones/m1-context-resolution.md)

## Milestone M2 - Execution Session Integration

(See ./milestones/m2-session-integration.md)

## Milestone M3 - Execution Monitoring and Observability

(See ./milestones/m3-monitoring-observability.md)

## Milestone M4 - Handoff Lifecycle Management

(See ./milestones/m4-handoff-lifecycle.md)

## Milestone M5 - Execution Acceptance Workflow

(See ./milestones/m5-acceptance-workflow.md)

## Milestone M6 - Git Lifecycle Automation

(See ./milestones/m6-git-lifecycle.md)

## Milestone M7 - Unified Execution Workspace

(See ./milestones/m7-execution-workspace.md)

## Milestone M8 - Next Execution Flow

(See ./milestones/m8-next-execution-flow.md)

## Certification Plan

Certification must verify the full execution system end to end with fake providers for deterministic automation and at least one local real-provider smoke test when the Codex executable is available.

### Domain 1 - Context Resolution

Verify:

- Context builds for a ready repository.
- Required artifacts are included.
- Optional missing handoff and decisions are reported but not fatal.
- Branch and Git status summary are captured.
- Dirty working-tree diagnostics are captured and visible.
- Warning threshold excess is visible but does not block launch.
- Hard-limit excess blocks launch.
- Certification records observed total context sizes and largest artifact sizes.
- Context diagnostics are visible in UI.

### Domain 2 - Execution Launch

Verify:

- Session is created.
- Prompt is constructed automatically.
- Provider starts.
- Repository transitions to `Executing`.
- Duplicate launch is blocked.
- Active session survives backend restart.
- Unrecoverable active sessions are marked failed after restart with an explicit reason.

### Domain 3 - Monitoring

Verify:

- Output streams to UI.
- Last activity updates.
- Running, completed, failed, and cancelled states project correctly.
- Dashboard reflects execution state without opening the execution workspace.

### Domain 4 - Handoff Lifecycle

Verify:

- Completion with handoff transitions to `AwaitingAcceptance`.
- Completion without handoff transitions to `Failed`.
- Previous current handoff is archived.
- New current handoff resolves correctly.
- Handoff review shows complete handoff content.

### Domain 5 - Acceptance Workflow

Verify:

- Handoff cannot be accepted before completion validation.
- Acceptance transitions to Git workflow.
- Rejection prevents commit and push.
- Acceptance state persists after restart.

### Domain 6 - Git Lifecycle

Verify:

- Modified, added, deleted, renamed, untracked, and staged files display correctly.
- Pre-existing dirty paths are visible during commit review when known.
- Proposed commit message is deterministic and editable.
- Each changed file is individually selectable.
- `Select All` and `Select None` affect the commit scope predictably.
- Unselected files are not staged or committed.
- Stale or invalid selected paths are rejected.
- Commit succeeds and records sha.
- Push succeeds and records result.
- Commit and push failures are visible and retryable.

### Domain 7 - Repeatable Repository Lifecycle

Verify:

```text
Ready
Executing
AwaitingAcceptance
AwaitingCommit
AwaitingPush
Ready
```

Run the lifecycle twice against the same repository and verify:

- No second active session can be created during execution.
- Handoff history increments.
- Session history remains available.
- Repository is ready for the next selected milestone after push.

## Implementation Order

1. Add execution models, interfaces, service registration, and architecture documentation.
2. Add Git snapshot support required by context resolution, including dirty-state capture.
3. Implement context package generation, size-limit diagnostics, and preview UI.
4. Implement session store, launch API, duplicate protection, and fake provider workflow.
5. Implement prompt construction, Codex provider process launch, and restart/orphan recovery.
6. Implement monitoring, event stream, and execution workspace stream display.
7. Implement handoff validation, previous handoff preservation, and awaiting acceptance projection.
8. Implement accept/reject workflow.
9. Implement Git status, deterministic commit preparation, per-file commit scope review, commit, and push.
10. Consolidate the execution workspace and responsive UI behavior.
11. Certify repeated execution lifecycle with fake provider and fake Git, then run a real-provider smoke test when available.

## Verification Commands

Backend tests:

```text
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj
```

UI build:

```text
npm run build --prefix src/CommandCenter.UI
```

Shell build:

```text
cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml
```

Full solution build:

```text
dotnet build CommandCenter.slnx
```

## Non-Goals

Do not implement:

- Decision sessions.
- Session routing.
- Session reuse as continuity.
- Automatic milestone progression.
- Operational context consolidation.
- Project understanding transfer.
- Provider selection UI beyond the initial Codex provider.
- Full repository content snapshots.
- Background filesystem watchers.
- Destructive Git recovery operations.
- Force push, rebase, checkout, or reset workflows.

## Final Exit State

Command Center can resolve execution context, launch a fresh execution session, observe output, validate handoff completion, obtain user acceptance, commit, push, and return a repository to a ready state for the next execution.

The system remains intentionally simple: every execution is new, every handoff is explicit, every acceptance is user-reviewed, and every Git operation is visible before it is performed.
