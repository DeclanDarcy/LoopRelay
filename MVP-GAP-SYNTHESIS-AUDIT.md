# MVP Gap Synthesis Audit — CommandCenter

**Date:** 2026-06-24
**Inputs:** `SEMANTIC-OPACITY-AUDIT.md` (66 findings), `reachability-and-orphaned-capability-audit.md` (6 findings, ~80/158 orphaned routes), and the implemented repository (verified).
**Purpose:** Determine what work remains to turn the existing implementation into a coherent MVP **while preserving the intended architecture**. Not a feature/UX/bug/code-quality audit.

---

## Central Thesis

The two source audits describe **one gap from two angles**:

- **Reachability** says *the pipe is not connected* — an endpoint/service has no Tauri command, no fetch, no hosted-service caller, so nothing in the shipped product can invoke it.
- **Semantic opacity** says *even where the pipe is connected, the meaning is dropped at the render layer* — the data reaches the browser (or is one client away) but the component shows an opaque outcome.

Both converge on a single conclusion the repository confirms:

> **The domain/backend layer is ~90% complete and transparent. The MVP gap is almost entirely at the integration boundary (UI API clients + render layer + a handful of missing trigger endpoints). The work is to *connect and surface already-computed capability*, not to build new capability.**

Verified evidence for the thesis: `src/CommandCenter.UI/src/api/` already holds a clean per-domain client pattern (decisions, execution, reasoning, continuity, …) but is **missing `workflow.ts` and a decision-session/`lifecycle.ts` client entirely**; `types/decisions.ts` is 28 KB of rich types the panels never fully read; and `RepositoryProjectionService` (Middle) already maps `IDecisionSessionObservabilityService` output onto `RepositoryDecisionSessionSummary` on the dashboard projection that the UI consumes for other fields. The data is on the wire; the consumer is missing.

This audit clusters all 72 findings into **8 initiatives**, ordered for architectural leverage.

---

# Part 1 — Initiatives (clustered, not one-per-finding)

| # | Initiative | Source findings folded in | Dominant pattern |
|---|---|---|---|
| 1 | **Workflow Engine Visibility** | F3 + WFL-1..8 | P1 UI-bypass / parallel model |
| 2 | **Decision-Session Governance Visibility** | F2 + SES-1..8 | P1 UI-bypass / missing client + trigger |
| 3 | **Decision Pipeline Activation** | F1 | Missing trigger commands (lifecycle verbs) |
| 4 | **Decision Reasoning Transparency** | GEN-1..10, GOV-1..8, QUA-1..10, EXE-6 | P2 dropped / P3 outcome-without-basis / P4 discarded alternatives / P5 masked veto |
| 5 | **Execution Transparency** | EXE-1..5, EXE-7, EXE-8 | P2 dropped / P6 degraded-flattened |
| 6 | **Reasoning Subsystem Transparency** | REA-1..6 | P3 outcome-without-basis |
| 7 | **Continuity / Operational-Context Transparency** | CON-1..8 | P3/P4 + 2 genuine compute gaps |
| 8 | **Endpoint Consolidation & Dormant-Surface Hygiene** | F4, F5, F6 + dormant-vs-wire decisions | Parallel/duplicate surfaces |

Initiatives 1 and 2 resolve **4 of 5 Criticals** and ~8 Highs at once and share one architectural move (add the missing per-domain client; render the already-projected data). They are the leverage core.

---

## Initiative 1 — Workflow Engine Visibility

### Purpose
Make the autonomous workflow engine observable. Today it advances, recovers, and certifies state headlessly; operators are blind to what it is doing and why.

### Existing Reality
- **Fully implemented & transparent backend:** `WorkflowProjectionService` (CurrentStage, 8-state ProgressState incl. `Blocked`/`Failed`/`Recovering`/`AwaitingGate`/`WaitingForHuman`, BlockingGate, RequiredHumanAction, per-stage Reasoning), `WorkflowGateCatalogService` (gate Type/RequiredAction/SatisfyingCommands/Reason/Evidence), `WorkflowHealthService` (5 dimensions, worst-wins), `WorkflowRecoveryService` (trust-vs-discard timeline), `WorkflowContinuationService` (Advance/Stop + StopReason), `WorkflowCertificationService` (authority-boundary invariants), `WorkflowPreparationService` (Allowed/Refused/Skipped/Duplicate).
- **28 REST routes** in `WorkflowEndpoints.cs` expose all of it.
- **Engine actually runs:** `WorkflowContinuationHostedService` (timer) + `WorkflowRecoveryHostedService` (startup) drive it.
- **Tests exist** (decision-session/workflow certification commits dominate recent history).

### Remaining Gap
- **No UI API client** for `/workflow/*` (verified: no `workflow.ts` in `src/api/`, zero `/workflow` references in non-test UI).
- The only workflow UI (`WorkflowRail`/`ExecutionWorkflowRail`) **re-derives a parallel 5-step model client-side** from `repositoryState` in `lib/executionWorkflow.ts`, bypassing the engine. The canonical 7-stage graph (with Decision and OperationalContext gates) collapses to 5 steps that cannot express "blocked on a decision gate."

### Recommended Architecture
Add `src/api/workflow.ts` following the existing per-domain client pattern. **Drive the existing rail from `GET /workflow`** (projection) instead of `executionWorkflow.ts`. Add three read panels consuming already-exposed endpoints: a **gate panel** (`/workflow/gates` → reason + satisfying command + evidence), a **health badge + dimension list** (`/workflow/health`), and a **workflow-certification panel** mirroring the existing decision/reasoning certification panels (`/workflow/certification`). Surface continuation `Outcome`+`StopReason` and recovery `DiscardedArtifacts` near the active stage.

### Existing Components To Reuse
- **Services (become the authority):** `WorkflowProjectionService`, `WorkflowGateCatalogService`, `WorkflowHealthService`, `WorkflowRecoveryService`, `WorkflowContinuationService`, `WorkflowCertificationService`, `WorkflowPreparationService`, and the 6 projection facets (`IWorkflowExecution/Handoff/Decision/OperationalContext/Git/DecisionSessionService`).
- **Endpoints:** the 28 `WorkflowEndpoints.cs` routes (read surfaces) — reuse directly.
- **UI:** the existing `WorkflowRail`/`ExecutionWorkflowRail` shell — re-point its data source.
- **Certification panel pattern:** existing decision/reasoning certification panels are the template.

### Components To Avoid Creating
- **Do not keep or extend `lib/executionWorkflow.ts`** as a parallel model — it is the duplicate to retire once the rail reads the real projection.
- Do not build a second workflow engine, state machine, or client-side stage derivation.
- Do not add a new `POST /workflow/recover` consumer path — startup recovery already runs; the manual `/recover` is redundant (treat as debug/dormant).

### Dependencies
None blocking. Independent of all other initiatives (the 6 projection facets read other domains but the engine already composes them).

### MVP Value
Highest leverage single initiative: resolves 1 Critical (WFL-1/WFL-2) + 6 Highs, and turns a headless automation into an observable one. Without it, the product's core promise — a governed, self-advancing workflow — is invisible and therefore untrustworthy.

---

## Initiative 2 — Decision-Session Governance Visibility

### Purpose
Give the user a window into the decision-session lifecycle: whether a transfer is recommended and permitted, the coherence/transfer-pressure driving it, per-dimension health, certification, recovery, and the influence trace.

### Existing Reality
- **Entire subsystem implemented, DI-wired, tested:** metrics/economics/coherence analytics, `DecisionSessionLifecyclePolicy` (transfer-vs-reuse with contributing factors), `DecisionSessionTransferEligibilityService`, `DecisionSessionTransferService` (full Active→…→Transferred state machine), continuity capture/integration, `DecisionSessionObservabilityService` (projection, health, influence trace), `DecisionSessionCertificationService`, `DecisionSessionRecoveryService`.
- **31 REST routes** in `DecisionSessionEndpoints.cs`.
- **Already on the wire:** `RepositoryProjectionService.BuildDecisionSessionSummaryAsync` (verified, lines 122–167) consumes `IDecisionSessionObservabilityService` and maps decision/eligibility/coherence/transfer-pressure/health/lineage onto `RepositoryDecisionSessionSummary`, a field of the dashboard projection the UI **already fetches** (it reads sibling fields `continuitySummary`/`reasoningSummary`).
- **Recovery runs** at startup via `DecisionSessionRecoveryHostedService`.

### Remaining Gap
- **Zero UI** consumes any decision-session field (verified: no component/hook/api references `decision-session`); no `lifecycle.ts`/`decisionSessions.ts` client in `src/api/`.
- **One missing trigger:** `DecisionSessionTransferService.ExecuteAsync` (state-mutating) has **no POST endpoint and zero external callers** — only GET history exists. Same for user-initiated persisted recovery (`RecoverAsync` runs only out-of-band at startup; the `/recovery` GET is persist:false).

### Recommended Architecture
Two moves, both additive at the boundary:
1. **Render what is already projected** — bind a **decision-session governance panel/inspector** to the existing `RepositoryDecisionSessionSummary` (no new fetch needed for the headline view).
2. **Add the deep clients + two triggers** — `src/api/decisionSessions.ts` for `/lifecycle/*`, `/transfers/*`, `/certification`, `/lifecycle/health`, `/lifecycle/influence`; add the **missing `POST` transfer endpoint** returning `DecisionSessionTransferResult`, and a **`POST` persisted-recovery endpoint**. Co-locate the lifecycle decision's `ContributingFactors` with the verdict (SES-3) and surface a typed **provisional/policy-unavailable** flag instead of a synthesized Transfer default (SES-4).

### Existing Components To Reuse
- **Projection seam (become the authority for the summary view):** `RepositoryProjectionService.BuildDecisionSessionSummaryAsync` + `RepositoryDecisionSessionSummary` — already computed; reuse directly.
- **Services:** all `CommandCenter.DecisionSessions` services, especially `DecisionSessionObservabilityService` (projection/health/influence), `DecisionSessionLifecyclePolicy`, `DecisionSessionTransferService`, `DecisionSessionCertificationService`.
- **Endpoints:** the 31 `DecisionSessionEndpoints.cs` routes for the deep views.
- **Hosted service:** keep `DecisionSessionRecoveryHostedService` (legitimate startup infra).

### Components To Avoid Creating
- Do not build a parallel session model or recompute coherence/transfer-pressure/economics client-side.
- Do not duplicate the dashboard fetch — the summary already rides the projection the UI loads.
- Do not invent a new analytics store; the snapshot/rebuild machinery exists.

### Dependencies
None blocking. The headline summary view depends only on existing projection wiring. The deep panels depend on the two new trigger endpoints (small additions).

### MVP Value
Resolves 1 Critical (SES-1/SES-2) + 4 Highs. Converts a fully-built but inert subsystem (high maintenance cost, zero delivered value) into a governance surface, and makes the one genuinely missing capability — *executing* a transfer — reachable.

---

## Initiative 3 — Decision Pipeline Activation

### Purpose
Make the decision pipeline functional end-to-end. The candidate→proposal→review→resolution→supersede/archive lifecycle is fully implemented and unit-tested, but its **state-advancing verbs have no Tauri command**, so a user cannot advance a candidate to a proposal, run discovery, transition a proposal through review, or supersede/archive a decision.

### Existing Reality
- `DecisionDiscoveryService` (discover/promote/dismiss/expire/duplicate), `DecisionGenerationService.GenerateProposalAsync` (the **bridge** from discovery to the reachable review UI), `DecisionReviewService` (viewed/needs-refinement/ready-for-resolution + notes), `DecisionRefinementService` (revisions/comparison), `DecisionResolutionService` (supersede/archive) — all implemented, tested, and exposed across ~18 orphaned routes in `DecisionEndpoints.cs`.
- The **read half is reachable**: `list_decision_candidates` and `build_decision_context` commands exist; the review/refine/resolve/governance/quality/certification panels exist.

### Remaining Gap
- **18 lifecycle-action endpoints have no Tauri command** (verified by negative grep of every candidate verb). The reachable review UI therefore has **no live input** — `GenerateProposalAsync` is the missing link between discovery and the panels.

### Recommended Architecture
Add Tauri commands in `main.rs` (and minimal UI affordances) for the verbs the product must support: `discover`, `promote candidate→proposal`, `dismiss/expire/duplicate`, the three proposal **review transitions**, `supersede`, `archive`. Drive UI eligibility from **`DecisionLifecycleRules` as the authority** (GOV-6) rather than the duplicated client-side guards. Formally **document or defer** verbs the MVP will not expose (e.g. notes, revision comparison) rather than leaving them silently orphaned.

### Existing Components To Reuse
- **Services (reuse directly, become authority):** `DecisionDiscoveryService`, `DecisionGenerationService`, `DecisionReviewService`, `DecisionRefinementService`, `DecisionResolutionService`, `DecisionLifecycleRules`.
- **Endpoints:** the 18 lifecycle-action routes — reuse directly.
- **UI:** the existing review/refinement/resolution panels — feed them live input.

### Components To Avoid Creating
- Do not build a parallel decision workflow engine or a second lifecycle state machine — `DecisionLifecycleRules` is the single source of legal transitions.
- Do not re-derive "what's allowed next" in the client; consume the rules (see GOV-6).

### Dependencies
None blocking, but it is the **functional prerequisite** for Initiative 4 to be meaningful: transparency over a pipeline the user cannot drive is moot. Sequence 3 immediately before 4.

### MVP Value
Highest *functional* impact of the decision domain: turns an apparently-complete but inert pipeline into a usable one. This is the difference between "the decision feature looks done" and "the decision feature works."

---

## Initiative 4 — Decision Reasoning Transparency

### Purpose
Surface the **why** behind every decision conclusion — ranking basis, discarded alternatives, masked veto rules, governance/authority state — almost all of which is already computed and (per `types/decisions.ts`) already transmitted to the browser, then dropped at render.

### Existing Reality
Backend computes, and in most cases serializes, the full justification:
- **Recommendation/options (GEN):** `RecommendationService` (per-option `Score`, `Rank`, `ScoreExplanation`, `Mode`, `SupportingFactors`/`Concerns`/`Assumptions`/`AlternativeExplanations`), `OptionComparisonService` (relative strengths/weaknesses, unique advantages/risks, disqualifying constraints), `OptionGenerationService`/`OptionValidationService` (rejected options + reasons in `GenerationDiagnostics`).
- **Governance/lifecycle (GOV):** `DecisionResolutionService` (4 stale-authority states + null-package downgrade + `recommendationDiverged`), `RefinementAnalysisService` (directive inference), `DecisionCertificationService` (authority-boundary/execution-consumption gates), `DecisionLifecycleRules` (legal transitions).
- **Quality/projection (QUA):** `DecisionQualityAssessmentService` (0–100 score + critical-signal veto + threshold bands), `DecisionQualityReportService`/`HumanAuthoringBurdenService` (effective-burden max-weight rule, trend), `DecisionQualitySignalService` (stability thresholds), `DecisionProjectionService` (projection-kind classification, excluded/superseded decisions, conflicts) — the last also drives `ExecutionDecisionInfluencePanel` and `EXE-6`'s launch-blocking conflicts.

### Remaining Gap
View-layer drops (P2/P3/P4/P5): the viewer renders only `optionId`/`rationale`/evidence; scores/explanations/rejected options/disqualifying constraints/excluded-and-conflicting decisions/quality-score/veto/thresholds are never read. A few need a thin **projection extension** (collapse-then-render lost data: GEN-4's review projection, GOV-2's `recommendedOptionId` persistence, GOV-5's reason-specific `detail`).

### Recommended Architecture
This is mostly render-layer work on data already on the wire. Add to the decision panels: an **Option Evaluations table** (score/rank/score-explanation), a **Disqualified / Rejected-options** section (P4), an **Excluded/Superseded/Conflicting decisions** section in the influence panel (QUA-7/8, EXE-6), a **quality score + per-signal breakdown + veto flag + threshold legend** (QUA-1..4), a typed **resolution-authority state** with an acknowledge-stale control (GOV-1), and the **legal-next-transition set** from `DecisionLifecycleRules` (GOV-6). Where data is collapsed before transmission, widen the **projection** (not the domain): emit the matched keyword on each projected statement (QUA-6), the burden rule + contributing signals (QUA-4), reason-specific certification `detail` scoped to actual violators (GOV-5).

### Existing Components To Reuse
- **Services as authority:** `RecommendationService`, `OptionComparisonService`, `OptionValidationService`, `DecisionQualityAssessmentService`, `DecisionProjectionService`, `DecisionResolutionService`, `DecisionCertificationService`, `DecisionLifecycleRules`.
- **Models/types:** `DecisionRecommendation`, `OptionEvaluation`, `DecisionTradeoffComparison`, `GenerationDiagnostics`, `DecisionQualityAssessment` — and the 28 KB `types/decisions.ts` that already carries most fields.
- **UI:** existing `DecisionProposalViewer`, `DecisionOptionComparison`, `DecisionResolutionPanel`, `DecisionQualityPanel`, `DecisionGovernancePanel`, `DecisionCertificationPanel`, `ExecutionDecisionInfluencePanel` — extend, don't replace.

### Components To Avoid Creating
- **Do not recompute scores, classifications, burdens, or conflicts in the UI** — surface the backend's already-computed basis. Recomputation would create a parallel reasoning model (the exact P1 anti-pattern).
- Do not duplicate `HumanAuthoringBurdenService` logic (already duplicated once between it and `DecisionQualityReportService` — consolidate, see Part 4).
- Do not build a second projection of decision influence.

### Dependencies
**Initiative 3** (a pipeline the user can drive). Otherwise independent.

### MVP Value
Resolves 1 Critical (GEN-5) + ~12 Highs. This is the domain where the product's trustworthiness lives: a governance tool that shows verdicts without basis is indistinguishable from a black box. Mostly view-layer, so very high value per unit effort.

---

## Initiative 5 — Execution Transparency

### Purpose
Surface the outcome-determining inputs and degraded states of an execution run: what actually went into the prompt, what recovery/push/handoff decisions were made, and the real monitoring health.

### Existing Reality
- `ExecutionPromptBuilder`/`ExecutionSessionService` capture `ExecutionPromptMetadata` (exact `IncludedArtifactPaths`, totals, `DirtyRepository`) — serialized by `GET /api/execution-sessions/{id}` but no UI reads it.
- `ExecutionSessionService` recovery (reattach vs orphan-fail), push-failure retry state (`PushAttemptedAt`+`FailureReason` while `AwaitingPush`), `HandoffService` archival branches, `ExecutionMonitoringService` (exit-code/retention/staleness signals) — all computed.
- `pushAttemptedAt` already mapped in `App.tsx`; git path origin (`ExecutionGenerated`/`PreExisting`) already labeled.

### Remaining Gap
- **Prompt manifest never rendered** (EXE-1, High): the single most outcome-determining input is invisible; the Context Diagnostics panel shows only the pre-launch *preview*, which can differ from the live launch.
- Recovery (EXE-2) and push-retry (EXE-3) reach the UI only as generic event text / a 409; the push endpoint **re-throws** so the updated summary never returns. Monitoring health is a literal **"Not projected" stub** (EXE-8).

### Recommended Architecture
Render-layer + one endpoint fix. Add a **"Prompt Composition"** section on `ExecutionSessionPanel` bound to `session.promptMetadata`. **Return the failed push summary** (don't only throw) and render `pushAttemptedAt`+`failureReason` as a retry warning. Add a **recovery banner** distinguishing reattach-success from orphan-fail. Replace the monitoring stub with a derived health summary (exit code, stale-activity flag, retention-trimmed indicator). Per-precondition hints for disabled git actions (EXE-4); a pre-existing-paths-selected warning (EXE-7).

### Existing Components To Reuse
- **Services as authority:** `ExecutionPromptBuilder`, `ExecutionSessionService`, `ExecutionMonitoringService`, `HandoffService`, `GitService`.
- **Models/types:** `ExecutionPromptMetadata`, `types/execution.ts` (`pushAttemptedAt` already present).
- **UI:** `ExecutionSessionPanel`, `ExecutionTab`, `GitWorkflowPanel`, `GitWorkflowEvidence`, `ExecutionContextValidationList`, `status.ts`.

### Components To Avoid Creating
- Do not re-assemble the prompt in the UI — render the persisted manifest captured at launch.
- Do not create a parallel monitoring/health model — surface `ExecutionMonitoringService` output.

### Dependencies
None. Independent; can run in parallel with 4/6/7.

### MVP Value
EXE-1 alone (prompt manifest) is high MVP value: without it the user cannot confirm which decisions/handoff/context produced a result — the audit trail has a hole exactly where it matters most.

---

## Initiative 6 — Reasoning Subsystem Transparency

### Purpose
Attach a stated basis to each reasoning heuristic so its verdicts stop reading as fact: materialization recommendation, reconstruction confidence, capture provenance, and ownership boundaries.

### Existing Reality
- `ReasoningMaterializationReviewService` (threshold function: failed≥2→AddReadModelReport, repeated≥3→AddDerivedCache; literal outcome enum), `ReasoningReconstructionService` (3-branch confidence; Backward/Forward traversal), `DecisionReasoningCaptureService` (Manual/Assisted/Inferred modes, idempotency skips), runtime ownership-boundary blocks. All computed; many relabeled euphemistically in the UI mapping.

### Remaining Gap
The UI translates real enums into vague phrases ("Materialization pressure observed") and shows bare labels ("Medium" confidence) with no branch/threshold/keyword/direction basis (REA-1..6). Boundaries surface only as static banners or raw exception text, never tied to the specific blocked action (REA-5).

### Recommended Architecture
Stop masking enums; emit a **decision-basis line** beside each verdict. Show the literal materialization outcome + counts + threshold (REA-1); emit a `confidenceRationale` from `CalculateConfidence` (REA-2); add a traversal-direction diagnostic (REA-3); derive a **Manual/Assisted/Inferred badge** from `sourceKind` + an "inferred from {transition}" line (REA-4); attach a **boundary/rule identifier** to `ReasoningValidationException` and render a structured "blocked by reasoning ownership boundary: X" notice (REA-5).

### Existing Components To Reuse
- **Services as authority:** `ReasoningMaterializationReviewService`, `ReasoningReconstructionService`, `DecisionReasoningCaptureService`, `ReasoningManualCaptureService`, `ReasoningRelationshipService`.
- **UI:** `ReasoningMaterializationReviewPanel`, `ReasoningReconstructionPanel`, `ReasoningQueryPanel`, `ReasoningEventFeed`.
- **Docs:** `reasoning-authority-boundary.md`, `reasoning-ownership-boundaries.md` — the rule text already exists.

### Components To Avoid Creating
- Do not reclassify or recompute confidence/materialization in the UI — surface the branch the service already took.
- Do not introduce a parallel capture-mode taxonomy; reuse `sourceKind`.

### Dependencies
None. Independent.

### MVP Value
Reasoning is the explainability spine; a heuristic presented as an architectural verdict ("promote to first-class entity") without its basis is the highest-trust-risk form of opacity. Low effort (mostly stop-relabeling + one rationale string each).

---

## Initiative 7 — Continuity / Operational-Context Transparency

### Purpose
Make the assimilation pipeline honest about what understanding it keeps, drops, and cannot reconcile — the gatekeeping that decides what survives into operational context.

### Existing Reality
- `DecisionAnalysisService` (taxonomy Architectural/Strategic/Tactical/Historical; only Architectural+Strategic assimilated; contradiction detection; consequence extraction), `OperationalContextGenerationService` (assimilation with `Take(8)`/`Take(3)` caps), `ContinuityDiagnosticsService`/`UnderstandingCompressionService` (resolved-vs-lost, noise keep/drop), `UnderstandingDiffService` (changed-vs-add/remove).

### Remaining Gap
Two kinds:
- **Pure surfacing (P3/P4):** taxonomy label + classified-out signals (CON-1), truncation warning (CON-2), extracted consequences (CON-3), all contradictions as a typed pair (CON-4 — also a 1-line `yield break` removal), resolved/lost item lists with evidence (CON-5), noise-removal reason category (CON-8).
- **Genuine compute gaps (the only non-surfacing work in this audit):** `ModifiedItemCount` is **hardcoded to 0** (CON-6) and `UnderstandingDiffService` detects "changed" only for decision rationale (CON-7) — both misrepresent in-place edits as delete+insert.

### Recommended Architecture
Surface the taxonomy decision (`DecisionAnalysisResult.Signals` with taxonomy+assimilated flag) and a "decisions considered/assimilated" breakdown on the proposal; emit a truncation warning ("8 of 12 durable decisions assimilated; 4 omitted"); model contradictions as a typed list and render a "Conflicts detected" section; extend `ContinuityTrend` with lost/resolved item lists. For CON-6/CON-7, **implement modification detection** (identity/decision-key matching) **or remove the always-zero field** so the accounting is not silently incomplete — a deliberate decision, not a silent stub.

### Existing Components To Reuse
- **Services as authority:** `DecisionAnalysisService`, `OperationalContextGenerationService`, `ContinuityDiagnosticsService`, `UnderstandingCompressionService`, `UnderstandingDiffService`.
- **Models:** `DecisionSignal` (incl. unused `Consequences`), `ContinuityTrend`.
- **UI:** existing operational-context proposal + compression/evolution panels (the audit confirms these are otherwise well-surfaced).

### Components To Avoid Creating
- Do not add a parallel taxonomy classifier or a second diff engine — extend the existing services' output.

### Dependencies
None. Independent. CON-6/CON-7 are the only items that require backend compute rather than projection/render.

### MVP Value
Medium: the assimilation pipeline silently shapes persisted understanding; surfacing keep/drop/contradiction prevents the user from trusting a proposal that quietly dropped 4 of 12 durable decisions. CON-6's permanent `Modified: 0` is an active correctness/honesty defect worth closing before MVP.

---

## Initiative 8 — Endpoint Consolidation & Dormant-Surface Hygiene

### Purpose
Remove parallel/duplicate read paths and formally classify deliberately-dormant capability, so the surface area the MVP ships is honest and minimal.

### Existing Reality
- `GET …/planning` (F4) — redundant; `IPlanningService` already reaches the UI via the workspace projection (`get_repository_workspace`).
- `GET …/repositories/{id}/artifacts` (F5) — duplicate of the consumed `…/workspace`.
- `GET …/execution-sessions/{id}/events` non-stream (F6) — superseded by the consumed `…/events/stream`.
- `POST …/workflow/recover` — duplicates startup recovery (see Initiative 1).
- `lib/executionWorkflow.ts` — parallel workflow model (retired by Initiative 1).
- Duplicated effective-burden logic across `HumanAuthoringBurdenService` and `DecisionQualityReportService` (QUA-4).

### Remaining Gap
These are not wired and not needed once 1–7 land, but they must be **explicitly resolved** (remove vs document-as-debug/dormant) rather than left as ambiguous orphans.

### Recommended Architecture
After Initiatives 1–3 settle which surfaces are canonical: **remove** F4/F5 duplicates and `lib/executionWorkflow.ts`; **document** F6 and `/workflow/recover` as internal debug/replay; **consolidate** the duplicated burden-weight logic into one service that both report and signal paths consume.

### Existing Components To Reuse
The canonical surfaces chosen in 1–3 (`/workspace`, `/events/stream`, real `/workflow` projection) become the single path.

### Components To Avoid Creating
Nothing new — this initiative only deletes/documents/consolidates.

### Dependencies
**Initiatives 1, 2, 3** must decide what is canonical before anything is deleted. Sequence last.

### MVP Value
Low direct value, high hygiene: prevents the MVP from shipping two ways to read the same data and reduces the "appears-operational but inert" surface that inflates maintenance cost.

---

# Part 3 — Integration Gap Ledger (backend exists, consumer missing)

The dominant MVP pattern. Every row is *built and tested* but not consumed:

| Capability | Backend | API route | Projection | UI consumer | Missing link |
|---|---|---|---|---|---|
| Workflow projection/gates/health/recovery/continuation/cert | ✅ | ✅ (28) | ✅ | ❌ | `api/workflow.ts` + rail re-point (Init 1) |
| Decision-session lifecycle/transfer/cert/influence | ✅ | ✅ (31) | ✅ (`RepositoryDecisionSessionSummary`, already on dashboard) | ❌ | render summary + `api/decisionSessions.ts` + 2 POSTs (Init 2) |
| Decision lifecycle verbs (discover/promote/review/supersede/archive) | ✅ | ✅ (18) | — | ❌ | Tauri commands (Init 3) |
| Recommendation reasoning / option scores / rejected / disqualified | ✅ | ✅ (on `types/decisions.ts`) | partial | ❌ render | view-layer + thin projection widen (Init 4) |
| Excluded/superseded/conflicting decisions (influence) | ✅ | ✅ | ✅ | ❌ render | influence-panel sections (Init 4) |
| Prompt manifest | ✅ | ✅ (`GET /…/{id}`) | — | ❌ render | `ExecutionSessionPanel` section (Init 5) |
| Push retry state | ✅ | ⚠ re-throws | — | ⚠ mapped, unrendered | return summary + render (Init 5) |
| Reasoning heuristic basis / capture mode | ✅ | ✅ | — | ⚠ relabeled | basis lines + badges (Init 6) |
| Taxonomy / truncation / contradictions / consequences | ✅ (mostly) | ❌ not serialized | — | ❌ | serialize + render (Init 7) |

---

# Part 7 — Operational Reachability Classification

| Class | Capability | Target MVP state |
|---|---|---|
| **Operational** | Repository, Artifact(+rotation), Continuity, OperationalContext, Decision context/review/refine/resolve/governance/quality/cert, Reasoning, Execution session/monitoring, Git, Planning-via-workspace | keep |
| **Integrated** | Dashboard projection (already carries decision-session summary) | extend consumption (Init 2) |
| **Backend-only → move to Integrated** | Workflow projection/gates/health/recovery/continuation/cert; Decision-session analytics/lifecycle/transfer/cert/influence; Decision lifecycle verbs | Initiatives 1, 2, 3 |
| **Dormant (keep as infra)** | Workflow continuation/recovery hosted services; decision-session/execution recovery hosted services; non-stream events endpoint | document (Init 8) |
| **Unreachable → remove/consolidate** | `GET /planning`, bare `/artifacts`, `lib/executionWorkflow.ts`, manual `/workflow/recover`, duplicated burden logic | Initiative 8 |

The roadmap moves **backend-only** capability into **integrated** for MVP (Initiatives 1–3) and resolves **unreachable** duplicates last (Initiative 8). No capability is downgraded; recovery hosted services stay dormant-by-design.

---

# Final Deliverables

## Recommended Initiative Order (architectural leverage)

1. **Workflow Engine Visibility** — 1 Critical + 6 Highs; data on wire; retires the only parallel model.
2. **Decision-Session Governance Visibility** — 1 Critical + 4 Highs; summary already projected onto the dashboard; one genuinely missing trigger.
3. **Decision Pipeline Activation** — functional prerequisite that makes the decision domain drivable; unblocks #4.
4. **Decision Reasoning Transparency** — 1 Critical + ~12 Highs; mostly view-layer on already-transmitted data; depends on #3.
5. **Execution Transparency** — prompt manifest (High) is the audit-trail hole; independent, parallelizable.
6. **Reasoning Subsystem Transparency** — low-effort basis lines; high trust value; independent.
7. **Continuity / Operational-Context Transparency** — surfacing + the only two genuine compute fixes (CON-6/7); independent.
8. **Endpoint Consolidation & Dormant-Surface Hygiene** — last; depends on 1–3 naming the canonical surfaces.

1–2 are the leverage core (4 of 5 Criticals, ~8 Highs, both already on the wire). 5–7 parallelize freely. 8 closes out.

## Consolidation Opportunities (many findings → one roadmap item)

- **F3 + WFL-1..8 → Initiative 1.** Reachability and opacity describe the same headless engine; one client + one rail re-point resolves both.
- **F2 + SES-1..8 → Initiative 2.** Same subsystem, same fix (render the projected summary + add the deep client/triggers).
- **GEN + GOV + QUA + EXE-6 → Initiative 4.** All decision-domain "surface the already-computed basis"; one set of panel extensions over one set of services.
- **A reusable "decision-basis / explainability" UI primitive** recurs in Initiatives 4, 5, 6, 7 (pattern P3) — build it once (matched-keyword / crossed-threshold / branch-taken line) and reuse across domains.
- **A reusable "discarded alternatives" section** (pattern P4 — rejected options, excluded/superseded/conflicting decisions, classified-out signals, truncated assimilations) — one component, four consumers.
- **A reusable "degraded/provisional state badge"** (pattern P6 — snapshot-rebuilt, policy-unavailable, recovery-discarded, push-failed, monitoring-stub) — one typed-state treatment across Init 2/5/7.
- **Burden-weight logic** duplicated across `HumanAuthoringBurdenService` and `DecisionQualityReportService` → consolidate to one authority (Init 8).

## Architecture Preservation Review

The roadmap is *additive at the consumption boundary* and preserves every intended relationship:

- **Authority boundaries.** Lifecycle still owns lifecycle; workflow/observability still *consume* it; repository summaries still consume observability; certification still *observes*. Verified intact: `RepositoryProjectionService` consumes `IDecisionSessionObservabilityService` (not the reverse). No initiative inverts a relationship — UI consumes projections; it never writes lifecycle state except through existing trigger services (the two new POSTs in Init 2 call existing `ExecuteAsync`/`RecoverAsync`, they do not reimplement them).
- **Determinism.** No initiative touches the deterministic services; surfacing the *basis* of a heuristic (matched keyword, threshold, branch) exposes determinism rather than altering it.
- **Semantic correctness.** Every "Recommended Architecture" surfaces the value the backend already computed; the explicit anti-pattern (recompute in UI) is forbidden in every "Components To Avoid Creating," eliminating parallel models (the root cause P1).
- **Rebuildability.** Recovery/snapshot-rebuild machinery is untouched and kept dormant-by-design; CON-6/CON-7 are the only compute changes and are confined to diff accounting, not persistence or recovery.
- **Observability.** The roadmap *increases* it (workflow health, decision-session certification, influence traces) by consuming existing observability services, never by adding a second telemetry path.
- **Workflow & lifecycle boundaries.** The parallel `executionWorkflow.ts` model is retired in favor of the canonical projection; `DecisionLifecycleRules` becomes the single source of legal transitions; no parallel workflow engine or lifecycle model is introduced.

## MVP Completion Assessment

- **Implemented (~80–90% by capability):** the domain/backend layer is near-complete and transparent — 9 backend projects, 158 routes, all services DI-wired and unit-tested, projections computed, recovery/certification present. The opacity audit's own non-findings confirm the serialization layer is sound.
- **Remaining (the MVP gap, ~10–20%, concentrated at one seam):** ~80 orphaned routes and 66 opacity findings reduce to **2 missing API clients, ~12 missing Tauri trigger commands, 2 missing POST endpoints, a set of view-layer renders over already-transmitted data, and 2 genuine compute fixes (CON-6/CON-7).** Roughly **85% of remaining work is integration/render, ~15% is small additive endpoints/commands, <5% is new computation.**
- **Architectural risk: LOW.** The roadmap connects and surfaces; it does not redesign domains, services, lifecycle, projections, persistence, recovery, or observability. The single structural change is *removing* a parallel model (`executionWorkflow.ts`), which reduces risk. The main execution risk is scope discipline in Initiative 4 (resist recomputing in the UI).
- **Highest-leverage remaining work:** **Initiatives 1 and 2** — the two invisible subsystems whose data is already on the wire. Completing them resolves 4 of 5 Criticals and the bulk of the Highs for a cost dominated by adding two API clients and rendering existing projections. This is the elegant-evolution core: the MVP is reached primarily by *finishing the last mile of integration*, not by building more system.
