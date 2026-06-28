# Phase 12 - Deferred Non-Goals and Final Definition of Done

Goal: close the implementation against the original design and explicitly defer broader product expansion.

## Design note (m12)

m12 is the capstone milestone (the file name "adaptive-engineering-intelligence" is stale — that phrase is the
FIRST listed non-goal, not the milestone's subject; the title and body are authoritative, as with m8 and m11).
It builds no features. Its bar is faithfulness: prove every Definition-of-Done acceptance criterion is satisfied
by the IMPLEMENTED system, and prove every explicit non-goal was genuinely NOT built. The only code added is one
additive test (`FinalAcceptanceTests`); no prompt, Rust, UI, wire, or orchestration production-code change.

A five-lens read-only audit verified each criterion against real source, followed by an adversarial
default-refute pass (hunting for a missing step, wrong prompt, broken order, UI inferring authority, a non-goal
that was in fact built, or a tautological guarantee). Outcome: all 14 Final Acceptance criteria met (under
default `OrchestrationFeatureFlags`), all 6 non-goals honored, and ZERO implementation defects. A second,
single-doc adversarial faithfulness review checked the certification's transcription against source. The durable
record is `docs/final-acceptance.md`.

Three faithful nuances were recorded rather than glossed:
- FA-5: the eight-step Execute-Plan order holds under default flags; commit/push is gated by
  `AutomaticCommitPushAfterExecuteEnabled` (default true) — m10 rollback path 4. With the flag off the publish
  step and its `committed` frame are skipped (the designed off-switch, not a contract break).
- NG-2 / NG-3: three pre-existing, repository-scoped subsystems use adjacent vocabulary but are neither
  platform-wide nor wired into the loop — `CommandCenter.Decisions` (`DecisionDiscoveryService`/
  `RecommendationService`, scoped by `repositoryId`, the deterministic/offline fallback), `CommandCenter.Reasoning`
  (`ReasoningGraphService`, a per-repo lineage view), and `CommandCenter.Continuity` (`CompressionTrend`,
  per-proposal metrics). The orchestration loop adds none of them and consumes none of them — verified
  structurally: `CommandCenter.Orchestration`'s compiled manifest does NOT reference `CommandCenter.Reasoning`,
  and its source has zero references to those discovery/recommendation/graph services.

Deliverables:
- `docs/final-acceptance.md` (new) — the capstone certification: the 14 acceptance criteria and 6 non-goals
  mapped to verified source anchors, the three nuances, and the satisfied Completion Statement.
- `tests/CommandCenter.Backend.Tests/Orchestration/FinalAcceptanceTests.cs` (new) — pins the four m12-specific
  boundaries no earlier milestone test guarded: (1) the five-method Completion-Statement command surface on
  `RepositoryOrchestrator`; (2) non-goal isolation — the loop's compiled manifest does not reference
  `CommandCenter.Reasoning`; (3) compositional delegation — the constructor takes `IAgentRuntime`,
  `IArtifactStore`, `IPlanArtifactPublisher`, `IDecisionSessionRouter` (NG-6); (4) the generated eleven-prompt
  catalog is complete and exposes `.Text`/`.Render` (FA-12 / NG-5). Pure unit class (no host boot), so it stays
  outside the `ProcessEnvironment` serialized collection.

Verification: full backend suite **1133 passed / 1 skipped / 0 failed** (m11 was 1129 → +4 from
`FinalAcceptanceTests`; the m10 `ProcessEnvironment` reproducibility holds; the 1 skip is the live-only
`[Fact(Skip)]` app-server check). Backend-only and additive: the three m8-frozen UI type files, the m8 contract
goldens, the prompts, the Rust shell, and the UI are byte-untouched → UI stays m9's 420/420 by construction;
cargo not exercised.

## Non-Goals For This Plan

- [x] Do not build a Repository Knowledge platform as part of this flow.
- [x] Do not add adaptive engineering intelligence, opportunity discovery, recommendation generation, or platform-wide learning.
- [x] Do not add knowledge graph, lineage explorer, repository query surface, or trend analysis unless separately approved after this design is complete.
- [x] Do not let the UI infer lifecycle legality, decision validity, router behavior, prompt selection, or artifact authority.
- [x] Do not let prompts become semantic authority; they remain generated communication mechanisms.
- [x] Do not let the orchestrator become a domain service for Execution, Decisions, Continuity, Git, Workflow, or contracts.

## Final Acceptance

- [x] From a repository with no `.agents/plan.md`, Plan Authoring is shown.
- [x] Roadmap and Specs are written to `.agents/specs`.
- [x] Write Plan uses the correct generated prompt and creates `.agents/plan.md`.
- [x] Revise Plan uses `RevisePlan.Render(feedback)` in the same planning process.
- [x] Execute Plan closes planning, copies operational context, caches plan text, extracts milestones, commits/pushes, sets `ExecutingPlan`, starts execution, and rotates `handoff.0001.md`.
- [x] Decision session starts in a separate zero-permission Codex process.
- [x] `GetNextDecisions.Render(handoff)` streams proposed decisions that become editable after completion.
- [x] Submit persists edited decisions and runs `ContinueExecution.Render(plan, handoff, decisions)`.
- [x] Each continuation produces and rotates the next handoff.
- [x] Router `Continue` reuses the warm Decision process.
- [x] Router `Transfer` writes operational delta, rewrites operational context, starts a fresh Decision process, and resumes decision streaming.
- [x] All prompt text comes from generated `CommandCenter.Core.Prompts` classes.
- [x] Execution and DecisionSessions reach Codex only through `CommandCenter.Agents`.
- [x] Full certification commands and relevant contract/governance suites pass.

## Completion Statement

The design is complete when the user can stay on one repository screen from initial roadmap/spec authoring
through repeated decision-mediated execution turns, with persistent planning and decision Codex processes
where required, faithful artifact writes under `.agents`, generated prompt provenance for every agent turn,
and router-driven reuse or transfer of the active Decision process.

**Satisfied.** Each clause maps to verified behavior (`docs/final-acceptance.md`): the single-screen flow
(FA-1, the `isAuthoringSessionActive` mount latch, m9's in-place lifecycle); persistent planning + decision
processes (FA-4, FA-6, FA-10; held-open `CodexAppServerSession`); faithful `.agents` artifact writes (FA-2,
FA-5, FA-8, FA-9); generated prompt provenance per turn (`PromptProvenance` for every planning/operational/
decision/transfer turn); and router-driven reuse or transfer (FA-10, FA-11). The CommandCenter refactor
(m0–m12) is complete.
