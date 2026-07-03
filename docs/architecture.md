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

## Orchestration Loop Architecture

The `next`-branch refactor (milestones m0–m10) adds a continuous **Plan Authoring → Execution → Decision** loop owned by `CommandCenter.Orchestration` and driven by held-open Codex processes. It is a distinct subsystem from the legacy execution-session model documented further below; both coexist in the running backend. The legacy endpoints are retained as a documented rollback path. The governance evidence packages, intentional divergences, rollback paths, and known fallback behavior for this loop are recorded in `docs/orchestration-loop-governance.md`.

### Shared role-agnostic process runtime (`CommandCenter.Agents`)

`CommandCenter.Agents` is a leaf project — it references no other `CommandCenter.*` project (enforced by `ArchitectureLayeringTests`) — and owns Codex process lifetime independent of any session role.

- A process is launched by `CodexAgentProcessLauncher` and reaped by `AgentProcess.DisposeAsync`, which calls `Process.Kill(entireProcessTree: true)` when the process has not exited. This is the single reaping path for every fail, cancel, and dispose.
- Two session shapes share one runtime: **one-shot** (`AgentSession` + `CodexEventTurnBoundaryDetector`, driving `codex exec --json`; `IAgentRuntime.RunOneShotAsync`) for single turns, and **held-open** (`CodexAppServerSession`, driving `codex app-server --listen stdio://` over JSON-RPC 2.0; `IAgentRuntime.OpenSessionAsync`) for warm multi-turn reuse. The held-open transport is the Codex app-server protocol — handshake `initialize → initialized → thread/start → turn/start`. The removed `codex proto` subcommand is **not** used.
- Held-open sessions are tracked in `AgentSessionRegistry`, keyed by `(RepositoryId, SessionIdentity)`. `IAgentRuntime.CloseSessionAsync` is the single teardown path (deregister + dispose, exactly-once).
- Effort is an `EffortProfile`: an `AgentEffortLevel` (Low/Medium/High — there is no `ExtraHigh` enum member) plus an `Identifier` escape hatch for non-enum tiers such as `xhigh`. Sandbox is a `SandboxProfile` (`CanWriteWorkspace`, `CanAccessNetwork`, `RequiresApproval`) mapped to Codex `read-only`/`workspace-write` and approval `never`/`on-request`.
- `SessionRole` (Planning, OperationalExecution, Decision, Transfer, ContextUpdate) is descriptive metadata; the runtime is otherwise role-agnostic.

### Operational vs Decision session roles

The loop drives two role-specialized sessions distinguished only by their declared sandbox and effort, not by runtime type:

- **Operational** (planning and execution turns): `workspace-write` sandbox, approvals off, effort `xhigh` for planning and `medium` for execution. It may author repository files.
- **Decision**: `read-only` sandbox, approvals `never`, effort `xhigh` — **zero operational authority**. It proposes governance decisions but cannot mutate the workspace. This posture is structural (the CLI sandbox flags themselves), not advisory, and is asserted by certification tests.

### Generated prompt authority

Every agent turn is issued from a compile-time class generated from a `.prompt` template under `src/CommandCenter.Core/Prompts/` (11 canonical prompts). No production code composes prompt text by hand: `PromptAuthorityTests` scans `src/**` for distinctive canonical prompt-body markers and fails if any literal prompt body reappears outside the generated catalog. Each turn records a `PromptProvenance` (prompt name, generated type, `SourceHash`, session role, workflow phase, and input/output artifact identities). The prompt catalog, generated signatures, and these mechanisms are documented in `docs/prompt-architecture.md` and `docs/architectural-mechanisms.md`.

### Repository-scoped orchestrator ownership

`RepositoryOrchestratorRegistry` is a process-wide singleton holding at most one `RepositoryOrchestrator` per repository id (`Lazy<>` dedup; a registry-wide `mutationGate` serializes create-versus-teardown so a replacement is published only after the prior instance fully disposes). A `RepositoryOrchestrator` owns only transient state — held-open session handles, cached plan/handoff/decisions, the iteration counter, `RouterInputs`, and the broadcast `OrchestratorStreamChannel`s. All durable lifecycle state (plan existence, handoff and decision sequence numbers) is reconstructed from the repository filesystem, so an orchestrator is restart-safe.

Two independent gates protect concurrency: a `runState` gate (`Idle`/`Planning`/`Executing`, via `Interlocked.CompareExchange`) serializes planning against execution; an independent `decisionState` gate lets a read-only Decision run overlap an execution run. `DisposeAsync` cancels the lifetime token, drains in-flight turns, completes the streams, then disposes and `CloseSessionAsync`-deregisters every session. Application shutdown additionally reaps the registry through `OrchestratorShutdownHostedService`, and `DELETE /api/repositories/{id}` tears down a repository's orchestrator (closing its held-open processes) before rewriting configuration.

### Plan authoring lifecycle

The orchestrator advances a repository through an unbounded loop. Milestones are not pinned to sessions; agents move between milestones dynamically and the iteration counter advances per loop pass.

1. **Write / Revise plan** (held-open Operational planning session). `BeginWritePlanAsync` persists the Roadmap to `.agents/specs/roadmap.md` and each Spec to `.agents/specs/s{n}.md` (1-based) *before* the turn, then runs `WritePlan` on the warm session, streaming to the plan stream; on completion it verifies `.agents/plan.md` exists, caches it, and emits `completed`. `BeginRevisePlanAsync` runs `RevisePlan` against the same warm session.
2. **Execute plan** (`BeginExecutePlanAsync` → background `RunExecutionAsync`). Closes the planning session, copies `.agents/plan.md` → `.agents/operational_context.md`, runs `ExtractMilestones` as an Operational one-shot (verifying `m*.md` were written), commits and pushes the planning/milestone artifacts (flag-gated — see below), runs `StartExecution` as an Operational one-shot, then verifies and reads the live `.agents/handoffs/handoff.md` and rotates it to `handoff.0001.md`.
3. **Decision run** (`BeginDecisionRunAsync` → `RunDecisionAsync`). Ensures the read-only Decision session, lazily seeds it once with `StartDecisionSession(operational_context)`, runs `GetNextDecisions(handoff)` streamed to the decision stream, and surfaces editable decisions on `review-ready`. The proposed decisions are transient until submitted.
4. **Human Submit gate** (`BeginSubmitDecisionsAsync`) — the only decision-persistence path. It claims the execution gate, writes the reviewed/edited text to the canonical `.agents/decisions/decisions.md` *and* a rotated `decisions.NNNN.md`, advances the iteration, then launches the continuation. This human gate replaces the legacy `AwaitingAcceptance` execution gate.
5. **Continuation** (`RunContinuationAsync`). Runs `ContinueExecution(plan, handoff, decisions)` as an Operational one-shot, verifies and rotates the next handoff, and on success routes the next decision run, closing the loop.

### Handoff and decision artifact rotation

Rotation is owned by the orchestrator, not by the legacy `HandoffService`:

- **Handoffs**: the live `.agents/handoffs/handoff.md` plus rotated `handoff.NNNN.md` (4-digit). The first rotation (Execute Plan) uses an in-memory counter under the one-way re-execution guard; later rotations derive the next sequence from disk (`NextHandoffSequenceAsync`), so rotation survives an orchestrator restart.
- **Decisions**: the live `.agents/decisions/decisions.md` plus rotated `decisions.NNNN.md`, one per human Submit.
- The historical globs `handoff.*.md` / `decisions.*.md` match only rotated files, never the live single-dot file.
- A **one-way re-execution guard** rejects Execute Plan with `409` if any historical handoff already exists, so a second execution cannot clobber rotated handoff history. Retries after an early-boundary failure (which leaves no historical handoff) are still allowed. All canonical paths are centralized in `OrchestrationArtifactPaths`.

### Router reuse and transfer

After a successful continuation, `RouteNextDecisionRunAsync` decides whether to reuse the warm Decision process or recycle it:

- It computes `RouterInputs` (the live Decision process's current context **occupancy** — the latest proposal's prompt+output tokens, i.e. `last_token_usage.input_tokens` + output, NOT a cumulative sum — when `> 0`, otherwise a deterministic `(len + 3) / 4` estimate over the handoff plus decisions) and calls the **registry-free** `DecisionSessionRouter.Evaluate`, which transfers once `DecisionSessionTokens` reaches `DecisionSessionRouterOptions.TransferOccupancyThresholdTokens` (default `ModelContextWindowTokens` `256_000` × `TransferOccupancyFraction` `0.80` = `204_800` — a high fraction of the window, because a recycle costs far more than continuing the ~95%-cached warm process). The router performs no I/O and consults no registry policy or eligibility service.
- A loop-owned eligibility gate downgrades `Transfer` → `Continue` when the process is unseeded (`!decisionSeeded`) or the execution gate is contended, so a recycle can never produce a bogus turn.
- **Reuse** (`Continue`): the warm Decision process proposes again. **Transfer**: `ProduceOperationalDelta` (warm) writes `.agents/operational_delta.md`, the session is closed, `UpdateOperationalContext` (Operational one-shot) rewrites `operational_context.md` — by default (Stage 2, `SandboxOperationalContextEvolutionEnabled`) inside an isolated sandbox workspace holding ONLY that context and the delta (codex `--cd`), so it no longer re-explores the whole repo each transfer; the evolved context is copied back into the repo. After the context update succeeds, the live delta is rotated to `.agents/deltas/operational_delta.NNNN.md` and removed — then a fresh session is opened and `StartDecisionSessionFromTransfer(rewrittenContext)` proposes against the rebuilt context. The evolved context's size is tracked across transfers and a sustained upward ratchet is flagged (`LastOperationalContextHealth`).

### Loop transport and contracts

The orchestrator exposes three Server-Sent Events streams (plan, execution, decision) over `OrchestratorStreamChannel`, a single-producer/multi-subscriber broadcast with monotonic sequence ids and a bounded replay buffer for `Last-Event-ID` reconnect (exactly-once across the replay/live boundary). Command endpoints acknowledge with `202` and run the work in the background on the orchestrator's lifetime token. The full endpoint, stream-event, and structured-error inventory is in `docs/contracts.md` and `docs/contract-endpoint-catalog.md`.

### Standalone planning pipeline (`CommandCenter.Plan.CLI`)

`CommandCenter.Plan.CLI` is a separate, standalone console application (not part of the backend sidecar) that runs a one-shot codex-driven planning pipeline against a target repository's `.agents/` directory ahead of the orchestration loop above. `PlanPipeline` sequences: a preflight gate that verifies the repository is clean of prior planning artifacts and that `.agents/specs/roadmap.md` exists; a held-open `danger-full-access` planning session that authors `.agents/plan.md` (`WritePlan`); a single-turn `read-only` session that adversarially reviews the plan (`AdversarialPlanReview`); a `RevisePlan` turn on the same warm planning session, followed by an eager session close; and three isolated, seeded-temp-workspace one-shots — `CollectDetails`, `ExtractMilestones`, and `ExtractDetails` — that in turn produce `.agents/details.md`, split the plan into checkbox-tracked `.agents/milestones/m*.md` files with the plan's milestone entries rewritten to pointers, and redistribute milestone-specific details. Every step is followed by a deterministic filesystem verification gate; a gate failure throws and aborts the run with the agent's stderr tail attached, and every opened codex session is closed on every path. Process exit codes: `0` completed, `2` bad arguments, `4` preflight blocked, `130` cancelled (Ctrl+C), `1` any other failure.

## Legacy Execution-Session Subsystem

> This subsystem predates the orchestration loop above and remains in the running backend as a parallel, supported path. It is the documented rollback target if the orchestration loop is disabled (`docs/orchestration-loop-governance.md`, rollback path 5). Its `AwaitingAcceptance` acceptance gate, disposable-worker session model, and explicit manual commit/push steps describe the **legacy** flow only — the orchestration loop replaces them with synchronous handoff rotation and the Decision Submit gate.

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

## Execution Context Preview (Legacy)

> Pre-loop execution-subsystem behavior, genericized. The milestone-pinned context preview was **removed** during the refactor: the `execution/context` endpoint survives but no longer takes a milestone — its `?milestonePath=` query parameter and the required-milestone validation are gone. The orchestration loop composes its own execution context internally and does not use this preview endpoint; a milestone *viewer* remains in the UI, decoupled from triggering execution. The paragraphs below describe the current, milestone-free endpoint.

Originally an M1 feature, `IExecutionContextService.BuildContextAsync(Guid repositoryId)` resolves a deterministic execution context. As implemented today it composes registered repository metadata, the required `.agents/plan.md`, the optional `.agents/operational_context.md`, the optional `.agents/handoffs/handoff.md`, a Git snapshot, and the governed decision projection (via `IDecisionProjectionService`). It loads no milestone file and no raw `.agents/decisions/decisions.md` artifact.

Only the plan is required; a missing operational context or current handoff is allowed and reported as a missing optional input. There is no milestone parameter and no `.agents/milestones` path validation — milestone pinning was removed from execution context.

Context preview captures repository branch, staged paths, modified paths, deleted paths, renamed paths, untracked paths, clean/dirty state, and snapshot timestamp. Dirty repository state is visible but does not block preview.

Context size policy is centralized in `ExecutionContextSizePolicy`: 128 KiB aggregate warning, 512 KiB aggregate hard limit, 96 KiB per-artifact warning, and 256 KiB per-artifact hard limit. Preview is still returned when validation or size diagnostics fail. Hard-limit excess and validation errors are represented as launch-blocking diagnostics for the future launch workflow.

The preview endpoint is `GET /api/repositories/{repositoryId:guid}/execution/context` (no query parameter). It does not launch a session, start a provider process, or perform handoff acceptance, commit, or push.

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

The legacy execution context consumed this ordering:

```text
Plan
Selected Milestone
Operational Context
Current Handoff
Current Decisions
Git Snapshot
```

> Orchestration-loop divergence: milestone pinning was removed, so the loop's execution context drops the `Selected Milestone` line. The loop composes execution prompts from the repo-global plan, operational context, current handoff, and the governed decisions projection — agents move between milestones dynamically rather than binding a session to one milestone path.

Operational context supplements plan, milestone, handoff, and decisions. It does not replace any of them and does not become a new workflow authority or repository state machine.

Markdown is the repository serialization format for operational context. Backend continuity services reason over a canonical `OperationalContextDocument` model defined in `docs/operational-context-schema.md`. Parsing, rendering, projection, coarse diffing, compression, decision assimilation, diagnostics, and later proposal review must use that document model rather than independent ad hoc Markdown parsing.

The first supported semantic changes are intentionally coarse: section added, section removed, section changed, item added, item removed, item changed, constraint added or removed, question added or removed, risk added or removed, decision added or removed, rationale changed, and preservation warning. Deeper semantic interpretation, correctness scoring, automatic drift correction, and metrics-driven mutation are outside the architecture boundary.
