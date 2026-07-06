# Orchestration Loop Governance

Governance evidence for the Plan Authoring → Execution → Decision orchestration loop (milestones m0–m10, `next` branch). This document is the durable evidence register required by `docs/architecture-decision-governance.md` and follows the Evidence Package Schema in `docs/architectural-evidence.md`. Each architecture-affecting change below records its invariant, owner, evidence, consumers affected, compatibility impact, and rollback path. The implementation overview is in `docs/architecture.md` (Orchestration Loop Architecture); the contract surface is in `docs/contracts.md` and `docs/contract-endpoint-catalog.md`; the protective mechanisms are in `docs/architectural-mechanisms.md`.

Scope note: the orchestration loop is a new subsystem that runs alongside — not in place of — the legacy execution-session subsystem (`LoopRelay.Execution`, `HandoffService`, `AwaitingAcceptance`). Both coexist in the running backend. The legacy subsystem is unmodified and is the documented rollback target (rollback path 5).

## Architecture-Affecting Change Register

Each row is an evidence package keyed by an evidence id. "Evidence" cites the authoritative source symbol; "Guard" names the regression that protects the invariant (see the test coverage map).

### LOOP-1 — Shared role-agnostic process runtime

- **Capability**: Codex process lifetime / state ownership.
- **Invariant**: A Codex process is owned by `LoopRelay.Agents` independent of session role and is reaped on every fail/cancel/dispose via `Process.Kill(entireProcessTree: true)`; `LoopRelay.Agents` references no other `LoopRelay.*` project.
- **Owner**: `LoopRelay.Agents` (`AgentRuntime`, `AgentProcess`, `AgentSessionRegistry`).
- **Slice/milestone**: m1.
- **Evidence**: `AgentProcess.DisposeAsync` (`Process.Kill(entireProcessTree: true)`); `LoopRelay.Agents.csproj` references only `Microsoft.Extensions.DependencyInjection.Abstractions`; two session shapes in `AgentRuntime.RunOneShotAsync` / `OpenSessionAsync`.
- **Consumers affected**: `LoopRelay.Execution`, `LoopRelay.DecisionSessions`, `LoopRelay.Orchestration` (the four importers of `LoopRelay.Agents`).
- **Compatibility impact**: Additive; legacy execution provider path unchanged.
- **Guard**: `ArchitectureLayeringTests` (leaf isolation), `ProcessLeakDetectionTests` (counting-process leak harness, m10).
- **Rollback path**: None required — the runtime is dormant unless an orchestrator opens a session; disabling the loop (rollback paths 1–4) leaves no live Agents sessions.

### LOOP-2 — Held-open transport is Codex app-server, not `codex proto`

- **Capability**: Agent transport.
- **Invariant**: The held-open multi-turn path drives `codex app-server --listen stdio://` over JSON-RPC 2.0 (`initialize → initialized → thread/start → turn/start`). The removed `codex proto` subcommand is never invoked.
- **Owner**: `CodexAppServerSession`, `CodexAppServerProtocol`, `CodexAgentArgumentBuilder`.
- **Slice/milestone**: m1.
- **Evidence**: `CodexAgentArgumentBuilder.Build` returns `app-server --listen stdio://` for `AgentSessionMode.Persistent` (with the explicit "removed `codex proto`" comment); `CodexAppServerSession` handshake; live protocol validation recorded in the m1 milestone evidence and the Conduit.Codex reference client.
- **Consumers affected**: The Decision and planning held-open sessions only.
- **Compatibility impact**: Replaces an earlier non-functional `codex proto` assumption; one-shot turns continue to use `codex exec --json` (`CodexEventTurnBoundaryDetector`).
- **Known limits**: A fully authenticated end-to-end app-server turn is gated on `codex login`; the corresponding live assertion is a `[Fact(Skip)]` runbook item, not a CI blocker.
- **Rollback path**: Disable persistent planning and decision reuse (rollback paths 2–3) so every turn runs one-shot via `codex exec --json`.

### LOOP-3 — Repository-scoped orchestrator ownership and two-gate concurrency

- **Capability**: State ownership / concurrency.
- **Invariant**: At most one `RepositoryOrchestrator` per repository id; planning and execution are serialized by a `runState` gate while a read-only Decision run may overlap an execution run via an independent `decisionState` gate; all durable lifecycle state is reconstructed from disk (restart-safe).
- **Owner**: `RepositoryOrchestratorRegistry`, `RepositoryOrchestrator`.
- **Slice/milestone**: m2 (registry/gates), m5–m7 (decision gate overlap).
- **Evidence**: `RepositoryOrchestratorRegistry` `Lazy<>` dedup + `mutationGate` serializing create-vs-teardown across `DisposeAsync`; `RepositoryOrchestrator` `runState`/`decisionState` `Interlocked.CompareExchange` claims; `GetPlanStatusAsync` reads plan existence from `IArtifactStore`.
- **Consumers affected**: All loop endpoints; the m9 repository workspace UI (`PlanAuthoring`/`ExecutingPlan` projection).
- **Compatibility impact**: Additive `PlanLifecycleState` projection; the wire-coupled `RepositoryExecutionState` is untouched.
- **Guard**: orchestration concurrency/lifecycle tests (teardown-vs-create serialization, dispose-drains-in-flight), `RepositoryOrchestratorStressTests`.
- **Rollback path**: Disable the loop (paths 1–4); the registry holds no orchestrators once no repository drives a turn, and `OrchestratorShutdownHostedService` reaps it on shutdown.

### LOOP-4 — Generated prompt authority, provenance, and no-literal-prompt enforcement

- **Capability**: Prompt authority.
- **Invariant**: Every agent turn is issued from a generated prompt class (11 canonical `.prompt` templates); no production code re-types a prompt body; each turn records a `PromptProvenance`.
- **Owner**: `LoopRelay.Core.Prompts` catalog + the Lib.Prompts source generator; `ExecutionPromptBuilder` and `RepositoryOrchestrator` (provenance capture).
- **Slice/milestone**: m0 (authority + provenance), m3–m7 (loop turns).
- **Evidence**: 11 `.prompt` files under `src/LoopRelay.Core/Prompts/`; `PromptProvenance` (7 fields) and `PromptSessionRole`; `PromptAuthorityTests` scans `src/**` for canonical prompt-body markers and asserts none.
- **Consumers affected**: All agent turns; the additive nullable `ExecutionPromptManifest.Provenance` wire field.
- **Compatibility impact**: Additive and nullable; no consumer is required to read provenance.
- **Guard**: `PromptAuthorityTests` (no-literal-prompt), `ArchitectureLayeringTests` (Core ⊥ Agents).
- **Rollback path**: Not applicable — prompt authority is a structural invariant with no behavioral toggle. The `.prompt` templates must not be edited as part of documentation work.

### LOOP-5 — Synchronous handoff rotation replaces `AwaitingAcceptance` (intentional divergence)

See the dedicated divergence record below. Owner: `RepositoryOrchestrator` (rotation) replacing the legacy `HandoffService.ProcessProviderCompletionAsync → AwaitingAcceptance` path. Guard: the one-way re-execution guard (`409` on existing historical handoff) + the recovery-window certification tests.

### LOOP-6 — Registry-free synchronous Decision router

- **Capability**: Decision routing.
- **Invariant**: The next decision turn's reuse-vs-transfer verdict is produced by a pure synchronous threshold over `RouterInputs.DecisionSessionTokens`; the router does no I/O and consults no `DecisionSessions` registry policy or eligibility service.
- **Owner**: `DecisionSessionRouter` (`IDecisionSessionRouter.Evaluate`), `DecisionSessionRouterOptions`.
- **Slice/milestone**: m7 (after a course-correction away from a registry-based router that was structurally unreachable in the registry-free loop — recorded in the m7 milestone evidence).
- **Evidence**: `DecisionSessionRouter.Evaluate(RouterInputs)` transfers once the live process's context **occupancy** (`DecisionSessionTokens` — the latest proposal's prompt+output, not a cumulative sum) reaches `TransferOccupancyThresholdTokens` (default `ModelContextWindowTokens` `256_000` × `TransferOccupancyFraction` `0.80` = `204_800`); the loop-owned eligibility downgrade (`route == Transfer && !decisionSeeded ⇒ Continue`) lives in the orchestrator, not the router.
- **Consumers affected**: The continuation → next-decision routing only; UI shows the effective route + a `transferred` event.
- **Compatibility impact**: Supersedes the unreachable registry-policy design; `LoopRelay.DecisionSessions` registry services remain for the legacy/deterministic path.
- **Guard**: deterministic-fallback router tests (estimate-when-observed-zero, token-reset-on-recycle, deferred-transfer fall-through), `DecisionRuntime ⊥ Execution` layering cert.
- **Rollback path**: Force transfer-only (rollback path 3) or disable decision reuse (path 2). Raising `ModelContextWindowTokens` or `TransferOccupancyFraction` makes the router Continue (reuse) longer.

### LOOP-7 — Milestone-pinning removed

- **Capability**: Execution context composition.
- **Invariant**: Execution sessions are not bound to a single milestone path; agents move between milestones dynamically and the loop's execution context is repo-global.
- **Owner**: `RepositoryOrchestrator` execution-context composition; `ExecutionPromptBuilder`.
- **Slice/milestone**: pre-m2 genericization + the milestone-pinning removal (recorded in `refactor-plan-status`).
- **Evidence**: the loop composes from plan + operational context + handoff + governed decisions projection with no `Selected Milestone` input; the legacy `?milestonePath=` preview is the only milestone-pinned path and is legacy-only.
- **Consumers affected**: UI keeps a milestone *viewer* decoupled from triggering execution.
- **Compatibility impact**: Wire-breaking for the legacy milestone-linked start was resolved cross-stack; the milestone viewer was retained.
- **Rollback path**: Rollback path 5 (legacy execution-session endpoints) restores the milestone-pinned context preview.

### LOOP-8 — m10 feature flags, leak fixes, and shutdown/teardown reaping

- **Capability**: Hardening / rollback controls.
- **Invariant**: A default-constructed `OrchestrationFeatureFlags` reproduces production behavior byte-for-byte for the four m10 flags (each an opt-out, or for the fallback opt-in, switch); the fifth flag, `SandboxOperationalContextEvolutionEnabled` (Stage 2), instead defaults ON to the sandboxed evolution — a deliberate cost behavior change — with OFF as its rollback lever (see LOOP-9). Held-open sessions are deregistered at every teardown site, on app shutdown, and on repository delete.
- **Owner**: `OrchestrationFeatureFlags`, `OrchestratorShutdownHostedService`, `IAgentRuntime.CloseSessionAsync`, `RepositoriesEndpoints` (DELETE teardown).
- **Slice/milestone**: m10.
- **Evidence**: `OrchestrationFeatureFlags` (5 bools, defaults `true/true/false/true/true`; the fifth is the Stage-2 `SandboxOperationalContextEvolutionEnabled` — see LOOP-9); the branch points in `RepositoryOrchestrator`; `DELETE /api/repositories/{repositoryId}` calls `registry.RemoveAsync` before config rewrite.
- **Consumers affected**: Operators (config `LoopRelay:Orchestration`); the `completed` frame keeps shape with `commitSha = null` when auto-commit is off.
- **Compatibility impact**: Additive; `NoContent`/`202` contracts unchanged.
- **Guard**: `RepositoryOrchestratorFeatureFlagsTests` (per-flag OFF-branch), `ProcessLeakDetectionTests`, `OrchestratorShutdownAndRemovalTests`.
- **Rollback path**: The flags *are* the rollback surface — see Rollback Paths.

### LOOP-9 — Sandboxed operational-context evolution + size health guard

- **Capability**: Transfer cost reduction and renewal-reward stability (Stage 2 of the transfer-cost economics work).
- **Invariant**: A decision-session Transfer's `UpdateOperationalContext` evolution one-shot runs in an ISOLATED sandbox workspace seeded with ONLY `.agents/operational_context.md` (read/write) and `.agents/operational_delta.md` (read), scoped by codex `--cd` (`AgentSessionSpec.WorkingDirectory` + `workspace-write`); it can no longer re-explore the repository. The evolved context is copied back into the repo. Separately, the operational context's size is tracked across transfers and a SUSTAINED upward ratchet (growth streak ≥ 2) is flagged.
- **Owner**: `RepositoryOrchestrator.EvolveOperationalContextAsync` / `RecordOperationalContextHealth`; `ISandboxWorkspaceFactory` / `TempSandboxWorkspaceFactory`; `OperationalContextHealthMonitor`; mirrored by `LoopRelay.CLI` `DecisionSession`.
- **Slice/milestone**: Stage 2 (post-refactor; transfer-cost economics).
- **Evidence**: the evolution one-shot's `WorkingDirectory` is the sandbox root, not the repository; the transfer copies the evolved `operational_context.md` back into the repo; `LastOperationalContextHealth.Warning` becomes true on a sustained ratchet (the CLI emits a `console.Warn`). Motivated by the measured ~425k-token repo re-exploration that dominated transfer cost.
- **Consumers affected**: None external — the evolved context and the `transferred` frame are unchanged; the size-health verdict is a read-only property (`LastOperationalContextHealth`), deliberately kept OFF the SSE decision-stream contract to avoid a contract/freshness/UI ripple.
- **Compatibility impact**: Additive; no SSE contract, prompt, or UI change. The prompt's `.agents/…` relative paths are preserved inside the sandbox layout.
- **Known limits / assumption**: The evolution agent runs with `--cd` pointed at a bare temp directory (no `.git`); directory-scoping is the mechanism (the coarse codex sandbox cannot express true per-file permissions).
- **Guard**: `RepositoryOrchestratorTransferTests` (sandbox isolation, copy-back, disposal on success + rewrite-failure, OFF-path repo-cwd, size-health baseline + ratchet), `OperationalContextHealthMonitorTests`, `TempSandboxWorkspaceFactoryTests`, CLI `DecisionSessionTests`.
- **Rollback path**: In the backend, `SandboxOperationalContextEvolutionEnabled = false` reverts the evolution to the repository working directory (pre-Stage-2 behavior). The CLI `DecisionSession` mirror ALWAYS sandboxes (it has no feature-flag surface); reverting it requires a code change, not configuration. See Rollback Paths.

## Intentional Divergence: `HandoffService` Behavior and `AwaitingAcceptance`

- **Evidence id**: DIVERGENCE-AWAITING-ACCEPTANCE.
- **Decision class**: Reference architecture change (durable architecture definition changes).
- **Invariant changed**: How a completed agent turn becomes accepted repository work.

**Legacy behavior (retained, unmodified).** In `LoopRelay.Execution`, `HandoffService.ProcessProviderCompletionAsync` validates the produced handoff and transitions `RepositoryExecutionState` to `AwaitingAcceptance`. The human then accepts or rejects the execution output through `ExecutionSessionService.AcceptAsync` / `RejectAsync`, after which commit and push are explicit `AwaitingCommit` / `AwaitingPush` workflow steps. Handoff history is preserved by `HandoffService.ArchivePreviousHandoffAsync`.

**Orchestration-loop behavior (new).** The orchestrator does not use `AwaitingAcceptance`. A completed execution turn rotates the handoff synchronously inside the orchestrator (`.agents/handoffs/handoff.md` → `handoff.NNNN.md`) and proceeds; commit/push happens automatically during Execute Plan (flag-gated). The human gate is **moved downstream** to the Decision Submit gate (`BeginSubmitDecisionsAsync`, reached via `decision/submit`): the operator reviews and edits the proposed governance decisions, and Submit is the single persistence point that advances the loop. Execution output itself is not separately accepted/rejected.

**Why.** The loop is continuous and process-warm; a blocking `AwaitingAcceptance` gate after every execution turn would stall the warm Decision process and conflicts with the `202` fire-and-forget command contract. Moving the human gate to decision submission keeps a real human checkpoint (governance decisions are the highest-leverage human input) without an execution-acceptance state machine.

- **Owner**: `RepositoryOrchestrator` (new); `HandoffService` / `ExecutionSessionService` (legacy, unchanged).
- **Evidence**: `HandoffService.ProcessProviderCompletionAsync` (`AwaitingAcceptance` transition, legacy); `ExecutionSessionService.AcceptAsync`/`RejectAsync` (legacy gate); `RepositoryOrchestrator` handoff rotation + `BeginSubmitDecisionsAsync` (new gate); `DecisionRuntimeEndpoints` Submit mapping.
- **Consumers affected**: Loop UI (no acceptance controls; a Decision review/Submit surface instead). Legacy execution UI retains its acceptance controls.
- **Compatibility impact**: None to the legacy subsystem — it is untouched and remains reachable. The divergence is additive at the system level (a second, parallel execution path).
- **Guard**: `DecisionRuntime ⊥ Execution` layering certification; `BackendEndpointDispositionTests` (the new `/decision/` family is distinct from legacy `/execution-sessions/`); the one-way re-execution guard.
- **Rollback path**: Rollback path 5 — route operators back to the legacy execution-session endpoints, which still implement `AwaitingAcceptance`.

## Rollback Paths

The loop exposes five rollback paths. Paths 2–4 are the m10 `OrchestrationFeatureFlags` (set under configuration `LoopRelay:Orchestration`); each default reproduces today's behavior, so a rollback is an explicit opt-out. A further Stage-2 flag, `SandboxOperationalContextEvolutionEnabled` (default **ON**, unlike the m10 flags), is an additional rollback lever (LOOP-9) for the **backend** `RepositoryOrchestrator` only: set it OFF to revert the sandboxed operational-context evolution to running against the repository working directory. The CLI `DecisionSession` mirror has no flag surface and always sandboxes, so reverting it is a code change rather than configuration.

1. **Disable the Plan Authoring screen.** This is a UI mount gate, not a backend flag: the React `App` keeps the Plan Authoring screen mounted only while an authoring session is active (`isAuthoringSessionActive` latch) or no plan exists. Removing the screen's entry point (or holding the gate closed) prevents the loop from being initiated from the UI; the backend endpoints remain but are undriven. *Restored behavior*: users interact only with the legacy workspace.
2. **Disable persistent planning** — `PersistentPlanningProcessEnabled = false`. Each planning turn runs as a fresh one-shot via `RunOneShotAsync`; Revise re-runs `RevisePlan` as its own one-shot against the persisted plan. *Restored behavior*: no held-open planning process.
3. **Disable Decision reuse / force transfer-only** — `PersistentDecisionProcessReuseEnabled = false` makes every decision run open→seed→propose→close (no warm Decision process). `TransferOnlyDecisionFallbackEnabled = true` forces the router verdict to Transfer *after* evaluation, while the unseeded⇒Continue and execution-gate-deferral safety downgrades still apply. *Restored behavior*: no warm decision reuse, or always-recycle routing.
4. **Disable automatic commit/push** — `AutomaticCommitPushAfterExecuteEnabled = false`. Execute Plan skips the commit/push and its `committed` frame; the `completed` frame keeps shape with `commitSha = null`, and the operator commits/pushes manually. A synchronous confirmation gate was intentionally not added (it would break the `202` Execute Plan contract); the flag is the off-switch.
5. **Return to existing execution/session endpoints.** The legacy `LoopRelay.Execution` execution-session subsystem (`ExecutionEndpoints`, `ExecutionSessionsEndpoints`, `HandoffService`, `AwaitingAcceptance`, `execution-sessions.json`) is unmodified and remains registered. Operators can drive execution entirely through it, bypassing the orchestration loop. *Restored behavior*: the full pre-refactor `AwaitingAcceptance` flow.

Each rollback restores a prior verified behavior without redefining an invariant, satisfying the preferred rollback order in `docs/architecture-decision-governance.md`.

## Known Fallback Behavior (explicit, not the full design)

These paths are deliberately retained as fallbacks. They are documented here so they do not masquerade as the live design.

- **Deterministic `LoopRelay.Decisions` services.** Decision generation/scoring in `LoopRelay.Decisions` is the offline/deterministic path. The live loop authors decisions through the Codex Decision session, not these services; a layering certification asserts the live loop takes no dependency on them. They remain a tested fallback, not the authority.
- **Token estimator fallback.** When observed decision-session token usage is `0`, the router falls back to a deterministic `(len + 3) / 4` estimate over the handoff plus decisions. Observed usage always wins when present. This is a routing heuristic of last resort, asserted by a determinism test, not a token-accounting source of truth.
- **Registry-free router degradation.** If router evaluation faults, routing degrades to `Continue` (warm reuse). Transfer is additionally downgraded to Continue for an unseeded or gate-contended process. These are safe-by-default degradations, not the intended steady-state route.
- **One-shot fallback when persistent processes are disabled.** With persistent planning/decision reuse off (rollback paths 2–3), the loop still functions via one-shot `codex exec --json` turns; warm-process efficiency is lost but correctness is preserved.

## Governance Test Coverage Map

Each new boundary is protected by a named guard; documentation alone does not certify a boundary.

| Boundary | Guard |
| --- | --- |
| Layering (Agents/Core leaves; DecisionSessions ⊥ Execution/Workflow; Execution ⊥ DecisionSessions) | `ArchitectureLayeringTests` |
| No literal prompt bodies in production source | `PromptAuthorityTests` |
| Endpoint family disposition (new `/plan/`, `/decision/`, `/conversation` families distinct from legacy) | `BackendEndpointDispositionTests` |
| Decision Runtime cannot depend on execution/operational orchestration | `DecisionRuntime ⊥ Execution` certification (m5) |
| No orphaned Codex processes across fail/cancel/dispose/recycle | `ProcessLeakDetectionTests` (counting-process harness) |
| Multi-write recovery windows do not corrupt artifacts | recovery-window certification tests + non-corruption theory |
| Per-flag OFF-branch behavior | `RepositoryOrchestratorFeatureFlagsTests` |
| Contract payloads/streams match producers | Contract Oracle (`OrchestrationSnapshotContractTests`, `OrchestrationStreamContractTests`, consumer/freshness/request-boundary suites) |
| Rollback surface (the 4 flag defaults + the legacy execution endpoints) still exists | `OrchestrationGovernanceTests` (m11) |

## Retention and Traceability

- This register is the durable evidence location for the orchestration loop; milestone-scoped evidence remains under `.agents/milestones/`.
- The implemented baseline is `docs/architecture.md`; the contract baseline is `docs/contracts.md` / `docs/contract-endpoint-catalog.md`; mechanism definitions are in `docs/architectural-mechanisms.md`; compatibility obligations are in `docs/compatibility-structure-governance.md`.
- Superseded designs (the registry-based router; the `codex proto` transport) remain traceable through the m1/m7 milestone evidence and are marked superseded here, not silently deleted.
