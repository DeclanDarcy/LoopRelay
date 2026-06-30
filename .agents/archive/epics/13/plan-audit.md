# CommandCenter Plan Readiness Assessment

Plan: `.agents/plan.md` | Branch: `next` | Solution: `CommandCenter.slnx`

## Executive Summary

The repository is at roughly **Phase 0-1 (scaffolded)** against the plan's Plan Authoring -> Execution -> Decision lifecycle. The generic, role-agnostic substrate the plan depends on is genuinely built and green: `CommandCenter.Agents` owns the process/session primitives with zero project references and clean namespace isolation; the 11 canonical `.prompt` files compile through the `Lib.Prompts` source generator into `CommandCenter.Core.Prompts` with exactly the plan's signatures (including per-prompt `SourceHash`); the solution builds (13 projects, 0 warnings/errors) and 815-816/816 backend tests pass; and the contract-verification harness is mature.

However, **every milestone-specific lifecycle deliverable from m1 through m12 is unbuilt**, and the two pillars that should already be in place are incomplete:

- The generated prompt catalog has **zero runtime consumers** — the live execution path hand-composes prompt text from string literals (`ExecutionPromptBuilder.cs`), violating Prompt Authority.
- `CommandCenter.DecisionSessions` does **not** depend on `CommandCenter.Agents` (commented-out TODO) and drives no Codex process, breaking the governing invariant that both Execution and DecisionSessions reach Codex only through Agents.

There is no repository orchestrator, no `plan/*` or `decision/*` endpoints, no `PlanAuthoring`/`ExecutingPlan` lifecycle states, no persistent multi-turn sessions (stdin is closed after one write), and no Plan Authoring UI. The current product still runs the **old** milestone-driven execution model gated on `AwaitingAcceptance` — the exact gate the plan says to bypass.

## Per-Milestone Status

| Milestone | Status | Conf. | Note |
|-----------|--------|-------|------|
| m0 Runtime Foundation | partial | 0.90 | Agents runtime + prompt generator real and green, but DecisionSessions not wired to Agents, no governance tests, no provenance model, catalog unconsumed. |
| m1 Persistent Agent Runtime | missing | 0.90 | No IAgentRuntime/registry/WritePromptAsync; `AgentProcess.cs:43` closes stdin after first write, making held-open multi-turn impossible. |
| m2 Repository Orchestrator | missing | 0.90 | No orchestrator type/project, no `plan/status` `{planExists,state}`, no `PlanAuthoring`/`ExecutingPlan` states, no MemoryCache. |
| m3 Plan Authoring Workflow | missing | 0.95 | Only read-only `GET /planning`; no write/revise/stream/execute endpoints, no spec/roadmap writers, no PlanAuthoringScreen. |
| m4 Execute Plan & Operational Turns | missing | 0.90 | No plan/execute bridge; legacy path uses `ExecutionPromptBuilder` and transitions to forbidden `AwaitingAcceptance`. |
| m5 Decision Runtime | missing | 0.90 | DecisionSessions not Agents-backed; StartDecisionSession/GetNextDecisions never rendered; deterministic stack is the only authority. |
| m6 Decision Submit & Continuation Loop | missing | 0.90 | No `decision/submit`, no `ContinueExecution.Render(...)` call site, no orchestrator/iteration counter; rotation exists but disconnected. |
| m7 Router Reuse & Transfer | missing | 0.86 | Continue/Transfer is a registry/file simulation on economics/coherence scores; no live process, no `operational_delta.md`, tokens not routed. |
| m8 Contracts, Artifacts, Provenance | missing | 0.85 | Per-turn provenance entirely absent; new contract families/endpoints absent; all-writes-via-store invariant violated by `DecisionGovernanceService`. |
| m9 Product Integration | missing | 0.95 | No PlanAuthoringScreen, no visibility gate, no new lifecycle states; UI is the legacy 7-tab dashboard. |
| m10 Hardening & Certification | partial | 0.85 | Build/test/contract baseline green, but no feature flags, recovery/stress/leak tests, or the lifecycle it certifies. |
| m11 Governance & Documentation | missing | 0.90 | (Re-audited) `docs/` framework predates the new architecture; `docs/architecture.md` still documents the `AwaitingAcceptance` gate the new design bypasses (obsolete → rewrite). No new-lifecycle arch doc, no prompt-catalog/provenance doc, no rollback paths, no backend governance tests. |
| m12 Deferred Non-Goals & Final DoD | missing | 0.88 | Non-goals correctly deferred; Final DoD unmet because m1-m11 lifecycle does not exist. |

## Cross-Cutting Concerns

| Concern | Status | Note |
|---------|--------|------|
| inv — Governing Invariant | partial | Negative isolation holds (DecisionSessions does not reference Execution/Git). Positive clauses fail: only Execution is Agents-wired; DecisionSessions has no Agents ref and no Codex code; `SessionRole`/`AgentSessionSpec`/`SandboxProfile`/`EffortProfile` are dead code. |
| prompt — Prompt Authority | partial | Generator + 11-type catalog verified by reflection with exact signatures + `SourceHash`. But zero consumers; `CommandCenter.Agents` does not reference Core; live `ExecutionPromptBuilder.cs` composes literals and has diverged from `StartExecution.prompt`; no provenance model; no governance tests. |
| artifact — Artifact Protocol | partial | `IArtifactStore`/`FileSystemArtifactStore`/`ArtifactService`/`ArtifactRotationService` real and tested with `handoff.000N`/`decisions.000N` rotation. No `specs/roadmap.md`/`s{n}.md` writer, no `operational_delta.md`, no plan.md->operational_context copy; `AwaitingAcceptance` not bypassed; plan's root-level `handoff.md` path contradicts canonical `.agents/handoffs/handoff.md`. |
| api — API & Streams | missing | All 8 plan endpoints absent (route literals appear only in `.agents/` docs). Sole production SSE is session-scoped `/api/execution-sessions/{sessionId}/events/stream` — single event type, no Last-Event-ID replay, not repository-scoped. |
| ui — Plan Authoring UI | missing | No `features/planning/PlanAuthoringScreen.tsx`, no authoring state machine, no Write/Revise/Execute/Submit controls. Only `types/planning.ts` (a trivial 4-line `PlanningMilestone` type) exists, unrelated to authoring. |

## Risks & Contradictions

**Highest-severity risks**
1. **Invariant half-met.** `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj` (lines 26-27) keeps the Agents `ProjectReference` commented behind a `TODO(refactor-plan Phase 3)`; the Decision role is not Codex-backed (decisions come from deterministic `DecisionGenerationService`).
2. **Prompt Authority unadopted and diverged.** Generated `CommandCenter.Core.Prompts` types have zero callers; `src/CommandCenter.Execution/Services/ExecutionPromptBuilder.cs` is the live, DI-registered path built from literals, with content materially different from `StartExecution.prompt`.
3. **Persistent sessions impossible as built.** `src/CommandCenter.Agents/Services/AgentProcess.cs` (~line 43) calls `StandardInput.Close()` after a single write — a redesign, not just wiring, is needed for m1.
4. **Build fragility.** `CommandCenter.Core.csproj` references `Lib.Prompts` at an out-of-repo path (`C:/kernritsu/dotnet-libraries/Lib.Prompts` via `..\..\..`); a clone of CommandCenter alone fails to build the generator.
5. **No governance tests.** Nothing forbids prompt literals outside `.prompt` files or enforces Agents dependency isolation; regressions pass CI silently. The generated catalog is also not emitted to disk, so signature breaks are invisible to consuming builds.
6. **Durability gaps.** `IArtifactStore.WriteAsync` is a single non-atomic `File.WriteAllTextAsync` (no temp-then-rename); `DecisionGovernanceService` writes `.agents` via raw `File`/`Directory` IO, violating all-writes-via-store.

**Key contradictions (plan prose vs. code)**
- Plan line 30: "Execution and DecisionSessions both depend on CommandCenter.Agents" — only Execution does.
- Plan line 46: `CommandCenter.Core.Prompts` is the canonical prompt API in use — no code consumes it.
- Plan line 62: every agent turn records prompt name/type/`SourceHash`/role/phase/artifact identities — no provenance model exists.
- Artifact Protocol: flow "intentionally bypasses the AwaitingAcceptance gate" — `HandoffService.cs` (~line 97) still sets `RepositoryExecutionState.AwaitingAcceptance`; no decision Submit gate exists.
- API & Streams: 8 repository-scoped endpoints described as conventional — zero exist; the only SSE is session-scoped with no reconnect/replay.

## Recommended Next Step

Per the plan's sequential Build Order, finish **m0** before anything downstream. Concrete, file-cited actions:

1. **Wire DecisionSessions to the shared runtime.** Activate the commented `ProjectReference` to `CommandCenter.Agents` in `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj` (lines 26-27) so the Decision role can reach Codex only through Agents (satisfies inv + m0 focus + plan line 30).
2. **Add governance/architecture tests** under `tests/CommandCenter.Backend.Tests` that (a) forbid canonical prompt literals outside `src/CommandCenter.Core/Prompts/*.prompt` / generated output, and (b) assert `CommandCenter.Agents` does not depend on Execution/DecisionSessions/Decisions/Workflow/Continuity/Backend/UI and that DecisionSessions does not depend on Execution. m0's Certification requires these.
3. **Adopt the canonical catalog.** Replace literal composition in `src/CommandCenter.Execution/Services/ExecutionPromptBuilder.cs` with `CommandCenter.Core.Prompts.StartExecution.Render(...)` / `ContinueExecution.Render(...)`, giving the generated catalog its first runtime consumer and resolving the Prompt Authority contradiction (plan line 46).
4. **Introduce a prompt-provenance model** capturing prompt name, generated type, `SourceHash`, session role, workflow phase, and input/output artifact identities (plan line 62 / m0 line 26), beyond the generator's per-type `SourceHash` constant.
5. **De-risk the build** by vendoring `Lib.Prompts` as a versioned NuGet `PackageReference` instead of the out-of-repo relative `ProjectReference` in `src/CommandCenter.Core/CommandCenter.Core.csproj`, and enable `EmitCompilerGeneratedFiles` so generated prompt sources are auditable.

Only after m0 is internally certifiable should work proceed to **m1** — and m1 must begin by removing the `StandardInput.Close()` single-write limitation in `src/CommandCenter.Agents/Services/AgentProcess.cs`, since held-open multi-turn sessions are the foundation for m3-m7.

---

*Methodology: produced by an 18-item fan-out (m0-m12 milestones + 5 cross-cutting invariants), each audited against real source then independently re-checked by an adversarial verifier; corrected statuses reflect the verifier. 36 agents, ~1.94M tokens, 846 tool calls. m11 was re-audited separately after its original verifier exceeded the StructuredOutput retry cap.*

*Correction (post-audit verification): the `DecisionSessions -> Agents` dependency is confirmed **absent** — `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj` lines 26-27 are a commented `TODO(refactor-plan Phase 3)`, not an active `ProjectReference`. Only `CommandCenter.Execution` references `CommandCenter.Agents`. The governing invariant remains half-met.*
