# Phase 10 - Hardening and Certification

Goal: burn down the concrete risks called out by the design before enabling the flow by default.

## Design note (m10)

A six-lens read-only gap analysis found that MOST of this milestone's hardening was already present from
m1-m9: the orchestrator is restart-safe (disk-derived handoff/decision sequences, plan status, the one-way
re-execution guard), dispose-correct (`lifetimeCts.Cancel` -> drain runs -> complete streams, gate-guarded
null-then-dispose, registry `mutationGate` across child disposal), and already KILLS its Codex processes on
every fail/cancel/dispose path (`AgentProcess.DisposeAsync` -> `Process.Kill(entireProcessTree)`). The effort
values (`xhigh`/`medium`) and the read-only/approvals-never Decision sandbox were already produced and partly
tested. So m10's real work was a NARROW set of additive production fixes, a BROAD certification-test layer, and
— the headline — making the full backend suite **reproducibly green** rather than "green modulo one tolerated
flake". It is backend-only: no prompt/Rust/wire/UI change, and the m8 contract goldens + the three frozen UI
type files were left byte-identical.

Production changes (additive; a default-constructed `OrchestrationFeatureFlags` reproduces today's behavior
byte-for-byte, so no existing test/golden/stream-frame changed):
- **Feature flags.** New `OrchestrationFeatureFlags` (sealed record, four bools, registered `TryAddSingleton`,
  optionally bound from config `CommandCenter:Orchestration`, threaded through the registry into the
  orchestrator): `PersistentPlanningProcessEnabled` (default true), `PersistentDecisionProcessReuseEnabled`
  (true), `TransferOnlyDecisionFallbackEnabled` (false), `AutomaticCommitPushAfterExecuteEnabled` (true). Four
  isolated branch points in `RepositoryOrchestrator`: planning held-open vs `RunOneShotAsync` (one-shot Revise
  re-runs `RevisePlan` against the persisted plan); decision reuse vs open->seed->propose->close per run;
  forced `route=Transfer` after `Evaluate` (the `!decisionSeeded`->Continue downgrade + execution-gate deferral
  still apply); skip the publish + `committed` frame when auto-commit is off (the `completed` frame keeps shape
  with `commitSha=null`). Each branch sits INSIDE the existing `runState`/`decisionState` gate claims.
- **Process-leak fixes.** (1) The orchestrator opened sessions via `OpenSessionAsync` (which registers them in
  `AgentSessionRegistry`) but disposed them directly and never deregistered, so the registry accumulated dead
  entries. New additive `IAgentRuntime.CloseSessionAsync(IAgentSession)` deregisters (reconstructs the key ->
  `AgentSessionRegistry.RemoveAsync`) AND disposes; the orchestrator calls it at all held-open teardown sites.
  (2) New `OrchestratorShutdownHostedService` (`AddHostedService`) disposes the registry in `StopAsync`,
  bounded by the stop token, best-effort — an explicit app-shutdown reap, since DI-container disposal only runs
  on graceful exit. (3) `DELETE /api/repositories/{id}` now calls `registry.RemoveAsync(id)` before the config
  rewrite, tearing down a live orchestrator + its Codex processes on removal (the NoContent contract and route
  disposition are unchanged). Pure UI re-selection (no backend hook) reuses the warm process by design.
- **Item-5 decision (commit/push gate).** `AutomaticCommitPushAfterExecuteEnabled` defaults ON (preserves
  today's behavior, keeps the `plan/execute` 202 fire-and-forget contract). A SYNCHRONOUS user-confirmation
  gate is intentionally NOT added in m10 because it would break that 202 contract; the flag IS the off-switch —
  set it false to suppress auto commit/push and let a human commit/push manually. Recorded as code comments at
  the flag and the publish site.

Certification tests (test-only, additive): app-server wire-level effort=`xhigh` + sandbox=`read-only` /
approvalPolicy=`never` captured from the real frame, the `medium` value, and a no-MCP/no-tools regression
guard; a counting fake `IAgentProcess` + fake launcher driving the REAL `AgentRuntime`/`AgentSessionRegistry`,
asserting live-process-count returns to 0 across failed/cancelled/faulted-turn, close, Transfer recycle,
orchestrator dispose, registry dispose, and the duplicate-open window, plus held-open long-output(5000) /
idle-reuse / cancel-mid-turn (process NOT killed by a turn cancel) / process-death (fail-fast) behavior; an
additive `FakeArtifactStore` write/delete failure hook driving the six multi-write recovery windows plus a
cross-cutting non-corruption `[Theory]`; a layering assertion that the live loop takes no dependency on the
deterministic `CommandCenter.Decisions` services, a regenerate-twice determinism assertion, the exact
`(len+3)/4` estimator-fallback (observed always wins), and a >=6-cycle decision-loop/transfer stress (with an
observed==0 deterministic-routing variant).

Suite reproducibility (the core certification deliverable). The full suite was NOT reproducibly green under
xUnit's default parallel execution — a five-lens adversarial review (14 findings, **0 confirmed production
defects**: the production logic and the new tests held up) empirically reproduced a 7-failure run caused by
classes that boot a real Kestrel host and/or mutate process-global state (`COMMAND_CENTER_CONFIGURATION_PATH`
/ `COMMAND_CENTER_EXECUTION_SESSIONS_PATH`, shared `configuration.json`, ephemeral ports/thread-pool) racing
under load. Fixed additively with `[CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]`
serializing every host-booting / env-mutating class against the rest of the suite (independent classes stay
parallel). The lone residual — the long-documented live-HTTP SSE-replay cold-start timing flake — was fixed
with a deterministic block-until-expected-frame read plus 60s cold-start headroom (no assertion weakened).
Result: **three consecutive full-suite runs at 1126 passed / 1 skipped / 0 failed** — the first milestone where
"Full backend tests pass" is reproducibly true rather than tolerated-with-an-isolation-caveat. The single skip
is a `[Fact(Skip)]` placeholder for the live-only check (real codex-cli 0.139 accepting `xhigh`/`read-only` on
a live app-server thread needs `codex login`; it is a manual/CI-gated runbook item, not a CI blocker).

## Implementation

- [x] Validate the exact Codex effort config values for ExtraHigh (`xhigh`) and Medium (`medium`) — pinned on
  the actual exec arg-builder and the held-open app-server turn/start frame, plus the opened-spec levels.
- [x] Validate the strictest practical Decision sandbox: read-only, approvals never, and no MCP/tools (the
  absence of any tool/MCP field is now a guarded regression, not just an omission).
- [x] Validate persistent process behavior under long output, idle periods, repeated turns, cancellation,
  failed prompts, and application shutdown (held-open session tests + the shutdown hosted-service test).
- [x] Add feature flags for:
  - [x] persistent planning process;
  - [x] persistent Decision process reuse;
  - [x] transfer-only Decision fallback;
  - [x] automatic commit/push after Execute Plan.
- [x] Decide and document whether automatic commit/push needs an explicit user confirmation gate before default
  enablement (decision: flag-default-ON; synchronous gate intentionally deferred — it would break the 202
  fire-and-forget contract; the flag is the off-switch).
- [x] Add recovery tests around multi-write windows:
  - [x] specs written but plan missing;
  - [x] plan exists but milestones missing;
  - [x] operational context copied but commit failed;
  - [x] handoff exists but rotation failed;
  - [x] decisions persisted but continuation failed;
  - [x] operational delta exists but context update failed.
- [x] Add process leak detection and cleanup on repository deselection, cancellation, failure, and shutdown
  (registry-deregistration leak fix + shutdown hosted service + DELETE-repo teardown; a counting-process leak
  asserter covers every terminal path; pure UI re-selection reuses the warm process by design).
- [x] Keep deterministic decision services and token estimator as tested fallback behavior (layering +
  determinism + exact `(len+3)/4` fallback assertions; the live loop authors via Codex, not these services).
- [x] Add stress tests for repeated decision loops and transfer cycles (>=6-cycle stress + observed==0 variant).

## Certification

- [x] Full backend tests pass — reproducibly: 3 consecutive full-suite runs at 1126 passed / 1 skipped / 0
  failed (the previously-tolerated parallel flake is eliminated, not merely re-run-in-isolation).
- [x] Relevant UI build, lint, unit, and E2E tests pass — m10 touched no UI file (the three frozen types stay
  byte-identical), so m9's verified 420/420 vitest + build/lint/typecheck hold by construction.
- [x] Shell tests pass if shell or sidecar behavior changed — no Rust/Shell change, so the condition does not
  trigger (cargo not exercised).
- [x] Contract and architecture suites pass (Contract Oracle / consumer / freshness / pipeline / request-
  boundary + ArchitectureLayering / PromptAuthority / BackendEndpointDisposition all green within the suite).
- [x] No failed or cancelled run leaves orphaned Codex processes (counting-process leak asserter: live count
  returns to 0 across failed/cancelled/faulted/close/recycle/dispose/registry-dispose/duplicate-open).
- [x] No failed or cancelled run corrupts repository artifacts (six recovery windows + non-corruption theory:
  written artifacts stay readable, ordinals recompute from disk, the gate releases, and a retry reaches green).
- [x] Feature flags and fallback paths are documented and tested (this design note + per-flag OFF-branch tests
  + the deterministic-fallback certification).
