# Command Center Architecture

Command Center is split into a React UI, a thin Tauri shell, and a .NET backend sidecar. The backend owns repository and artifact business logic; React owns presentation state; Tauri owns desktop windowing, native dialogs, sidecar lifecycle, and IPC bridging. During M0, the shell starts the backend sidecar on application launch and stops it when the desktop window closes.

## Filesystem Authority

Repository-owned state remains in each repository under `.agents`. Command Center reads, edits, and organizes those files, but it does not replace them with a private database. Missing artifact files and directories are valid states and must be projected explicitly rather than treated as application failures.

## Persistence Strategy

Application configuration stores local Command Center metadata, starting with registered repositories. Repository artifacts remain in the repository filesystem. In-memory state is a performance cache only and must be rebuildable from configuration plus filesystem scans.

## Service Ownership

Backend behavior is exposed through contracts:

- `IRepositoryService` for repository registration and removal.
- `IArtifactService` for artifact discovery, load, and save.
- `IArtifactStore` for persistence access.
- `IRepositoryProjectionService` for dashboard and workspace projections.
- `IApplicationConfigurationStore` for local application configuration.

The M0 implementation defines these boundaries and implements the artifact stores and configuration store. Repository registration, artifact discovery, rotation, planning readiness, and workspace projection behavior are implemented by later milestones.

## Artifact Store Philosophy

`IArtifactStore` is deliberately low level. It can read, write, delete, list, and check existence, but it does not classify artifacts, validate repositories, determine readiness, or decide lifecycle behavior. Higher-level services own those decisions.

## Manual Refresh Policy

Epic 1 uses explicit refresh. The application must not add filesystem watchers, background polling, or automatic rescans. Refresh rebuilds projections from repository filesystem state when the user asks for it.

## Deferred Snapshot Boundary

`IRepositorySnapshotService` is deferred. Git branch state, modified file counts, and execution-context snapshots belong to a later execution-oriented phase and are intentionally outside M0 and Epic 1 repository artifact management.

## Execution Architecture

Epic 2 introduces execution as a backend-owned subsystem. React presents execution state and controls, Tauri bridges HTTP commands and owns desktop lifecycle, and the backend owns context resolution, state transitions, provider invocation, monitoring, handoff validation, acceptance, and Git operations.

Execution sessions are disposable workers. A session exists for one selected repository and milestone, runs in a fresh provider process, writes or updates `.agents/handoffs/handoff.md`, and terminates. Sessions are not reused as continuity or treated as project memory.

Repository files remain authoritative. Plans, milestones, handoffs, decisions, and code changes stay in the repository filesystem. Command Center may persist local execution metadata so it can recover workflow state, but it must not replace repository-owned artifacts with private app state.

The backend execution boundary is split across these service contracts:

- `IExecutionContextService` builds deterministic execution context packages.
- `IExecutionSessionService` owns repository execution state and session lifecycle.
- `IExecutionMonitoringService` records provider output and activity without interpreting quality or intent.
- `IHandoffService` validates the current handoff and preserves previous handoffs.
- `IExecutionProvider` isolates provider-specific process launch and reattach behavior.
- `IGitService` owns non-destructive status, commit, and push operations.

Execution uses two separate state models. `ExecutionSessionState` tracks the provider lifecycle: `Created`, `Executing`, `Completed`, `Failed`, and `Cancelled`. `RepositoryExecutionState` tracks the repository workflow: `Ready`, `Executing`, `AwaitingAcceptance`, `Accepted`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.

Provider completion is not enough to mark repository work successful. A completed execution must produce a valid current handoff at `.agents/handoffs/handoff.md`; otherwise the execution is failed. User acceptance, commit, and push remain explicit workflow steps after handoff validation.

The first M0 implementation registers no-op execution services and projects repositories as `Ready` with no active session. It intentionally defines the contracts and UI placeholders without adding a start action or provider launch path.

## Execution Context Preview

M1 adds deterministic execution context resolution before any provider launch exists. A context preview is composed from registered repository metadata, `.agents/plan.md`, one selected `.agents/milestones/*.md` file, optional `.agents/handoffs/handoff.md`, optional `.agents/decisions/decisions.md`, and a Git snapshot.

Plan and selected milestone artifacts are required. Missing current handoff and current decisions artifacts are allowed and reported as missing optional inputs. The selected milestone path must remain inside `.agents/milestones`; non-milestone paths are validation errors.

Context preview captures repository branch, staged paths, modified paths, deleted paths, renamed paths, untracked paths, clean/dirty state, and snapshot timestamp. Dirty repository state is visible but does not block preview.

Context size policy is centralized in `ExecutionContextSizePolicy`: 128 KiB aggregate warning, 512 KiB aggregate hard limit, 96 KiB per-artifact warning, and 256 KiB per-artifact hard limit. Preview is still returned when validation or size diagnostics fail. Hard-limit excess and validation errors are represented as launch-blocking diagnostics for the future launch workflow.

The preview endpoint is `GET /api/repositories/{repositoryId}/execution/context?milestonePath=...`. M1 does not add session launch, provider process start, monitoring, handoff acceptance, commit, or push behavior.

## Operational Context

Operational context is the repository-owned current project understanding artifact at `.agents/operational_context.md`. It carries the durable mental model needed by future execution slices: architecture, authority boundaries, constraints, stable decisions, rationale that still affects future work, open questions, active risks, and recent understanding changes.

Operational context is not session memory. Execution sessions remain disposable provider workers, and the repository filesystem remains authoritative. Command Center may generate, review, promote, archive, and project operational context, but those operations must be mediated by repository artifacts and explicit human review.

Operational context must not contain raw execution history, execution streams, conversation logs, complete handoff archives, Git commit history, milestone status tracking, provider transcripts, or generic progress notes. Those inputs may inform a proposed understanding update, but they are not themselves current understanding.

Repository artifact responsibilities remain distinct:

- Plan: durable project intent, scope, and implementation strategy.
- Milestones: planned execution slices and certification criteria.
- Handoff: compact result of the most recent execution slice.
- Decisions: decision records, rationale history, and newly authorized choices.
- Operational context: current understanding synthesized from stable, still-relevant information.

Future execution context consumption must preserve this ordering:

```text
Plan
Selected Milestone
Operational Context
Current Handoff
Current Decisions
Git Snapshot
```

Operational context supplements plan, milestone, handoff, and decisions. It does not replace any of them and does not become a new workflow authority or repository state machine.

Markdown is the repository serialization format for operational context. Backend continuity services reason over a canonical `OperationalContextDocument` model defined in `docs/operational-context-schema.md`. Parsing, rendering, projection, coarse diffing, compression, decision assimilation, diagnostics, and later proposal review must use that document model rather than independent ad hoc Markdown parsing.

The first supported semantic changes are intentionally coarse: section added, section removed, section changed, item added, item removed, item changed, constraint added or removed, question added or removed, risk added or removed, decision added or removed, rationale changed, and preservation warning. Deeper semantic interpretation, correctness scoring, automatic drift correction, and metrics-driven mutation are outside the architecture boundary.
