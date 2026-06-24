# Semantic Opacity Audit — CommandCenter

**Scope:** Entire repository (~45,500 LOC C# across 9 backend projects + React/TS UI + REST endpoints).
**Method:** Gap analysis per domain — what each domain's services *compute* (state, decisions, transitions, constraints, evidence, uncertainty) vs. what actually *reaches the user* through endpoints, projections, and UI components. A concept is a finding only when it is computed/used internally but not surfaced (or surfaced only as an opaque outcome). All findings were verified against the actual exposure surface.

**Focus:** Semantic transparency, not implementation transparency. Database tables, paths, cache keys, DI, serialization, threading, and framework internals were treated as legitimate internal detail and excluded.

---

## Executive Summary

### Total findings by severity

| Severity | Count |
|---|---|
| **Critical** | 5 |
| **High** | 27 |
| **Medium** | 26 |
| **Low** | 8 |
| **Total** | **66** |

### Highest-risk opacity areas

1. **Workflow engine (Critical).** The authoritative workflow projection — current stage, 8-state progress (`Blocked`/`Failed`/`Recovering`/`AwaitingGate`/`WaitingForHuman`/…), blocking gate, required human action, per-stage reasoning, gate-satisfying commands, health dimensions, recovery discards, continuation stop-reasons, and certification failures — is fully computed and exposed over REST, but **no UI surface consumes it**. The only user-facing workflow view (`WorkflowRail`/`ExecutionWorkflowRail`) re-derives 5 hardcoded steps client-side from `repositoryState` alone (`executionWorkflow.ts`), bypassing the engine entirely. The user cannot see *why* the workflow is blocked, *what* unblocks it, or that its state was silently rebuilt/discarded.

2. **Decision Sessions (Critical).** The entire decision-session lifecycle — active session, lifecycle decision (transfer vs. reuse), transfer eligibility, coherence, transfer pressure, per-dimension health, certification, recovery path selection, and the full influence trace — is computed and even mapped onto the dashboard projection, yet **never rendered in any UI**. The actual transfer operation (`ExecuteAsync`) has no trigger endpoint and its multi-step result is discarded. This is a complete subsystem the user has no window into.

3. **Decision recommendation & option selection (Critical/High).** The ranking basis is hidden: per-option scores, the explainable score breakdown, disqualifying constraints that silently eliminate options, rejected options and their validation reasons, the categorical "recommendation withheld" reasons, and the full "why each alternative lost" set are computed — most even cross the API boundary in the TS types — but are dropped before render.

4. **Heuristic conclusions presented as fact (High/Medium, cross-cutting).** Quality scores, materialization recommendations, reconstruction confidence, projection-kind classification, and rating bands are all keyword/threshold heuristics whose outcome label is shown while the basis, the threshold, and the fact that it is a heuristic are hidden.

### Architectural patterns causing opacity

- **P1 — UI bypass (dominant root cause).** Entire subsystems compute rich, structured reasoning and expose it over REST, but the UI either has no API client for those endpoints (Workflow, Decision Sessions) or re-derives a simpler *parallel* model client-side. The backend is transparent; the opacity is created at the UI boundary. Root cause of all 5 Criticals and many Highs.
- **P2 — Transmitted-then-dropped.** The data reaches the browser (present on the TS types) but components render only a subset (recommendation reasoning, option evaluations/scores, `pushAttemptedAt`, `promptMetadata`). Serialization is transparent; the render layer silently discards.
- **P3 — Outcome without basis.** A derived label/score/verdict is shown, but the inputs, the threshold that was crossed, the matched keyword, and the "this is heuristic" caveat are withheld (projection classification, materialization thresholds, quality scoring, confidence tiers, rating bands).
- **P4 — Discarded alternatives.** The "what was considered and thrown out, and why" is computed and then dropped almost everywhere: rejected options, excluded/superseded decisions, detected conflicts, disqualifying constraints, second-and-later contradictions, truncated assimilations.
- **P5 — Masked veto/override rules.** A derived rule silently overrides the surface value with no flag: critical-signal forces rating to Poor, governance-block masquerades as `AwaitingResolution`, priority directive tilts scores, policy-unavailable fails toward `Transfer`.
- **P6 — Degraded/provisional state flattened.** Uncertainty and recovery events are demoted to warning strings or stubs rather than typed states: snapshot rebuilds, recovery discards, `ModifiedItemCount = 0`, "Monitoring warnings: Not projected", policy-unavailable defaults.

### Recommended remediation order

1. **Make the two invisible subsystems visible (resolves 4 of 5 Criticals + ~8 Highs at once).** The Workflow and Decision-Session data is *already on the wire*. Add UI API clients for `/workflow/*` and `/lifecycle/*` + `/transfers/*`, drive the workflow rail from the real projection, and add a decision-session governance panel + a workflow gate/health/recovery panel. Highest leverage per unit effort. (WFL-1..6, SES-1..8)
2. **Render the transmitted-but-dropped decision fields (P2).** Surface recommendation reasoning, the per-option evaluation/score table + score explanation, rejected options, disqualifying constraints, and excluded/conflicting decisions in the decision panels. Mostly view-layer work. (GEN-1..7, GOV-1..2, QUA-1, QUA-7, QUA-8, EXE-1, EXE-3)
3. **Attach a "decision basis" to every heuristic outcome (P3).** Emit the matched keyword / crossed threshold / score decomposition / confidence rationale alongside each derived label, and stop relabeling enums euphemistically. (QUA-1..6, REA-1..3, REA-6, GEN-8, GOV-3)
4. **Promote masked overrides and degraded states to typed, flagged states (P5/P6).** Surface veto/override flags, provisional/policy-unavailable markers, snapshot-rebuild and recovery-discard events, and replace stubbed health readouts. (QUA-2, WFL-8, SES-4, SES-8, CON-6, EXE-2, EXE-8)
5. **Surface discarded alternatives and constraints across domains (P4).** Add "rejected/excluded/conflicting/disqualified" sections wherever the system narrows a set. (GEN-5, GEN-6, GEN-9, QUA-7, QUA-8, CON-1, CON-2, CON-4)

---

## Findings

Finding IDs use a domain prefix: **GEN** (decision generation/options/recommendation), **GOV** (decision governance/lifecycle), **QUA** (decision quality/burden), **SES** (decision sessions), **WFL** (workflow), **REA** (reasoning), **CON** (continuity/operational context), **EXE** (execution).

---

### Decision Generation / Options / Recommendation / Tradeoffs

#### GEN-1 — Recommendation reasoning is computed and transmitted but never rendered
- **Severity:** High
- **Location:** `src/CommandCenter.UI/src/features/decisions/DecisionProposalViewer.tsx:83-92`; source `src/CommandCenter.Decisions/Services/RecommendationService.cs:74-115`; model `src/CommandCenter.Decisions/Models/DecisionRecommendation.cs:10-24`; type `src/CommandCenter.UI/src/types/decisions.ts:243-255`
- **Hidden Semantic Concept:** The full reasoning behind a recommendation — `SupportingFactors`, `Concerns`, `Assumptions`, `AlternativeExplanations`, `Mode`, and `OptionEvaluations` (each with `Score`, `Rank`, `ScoreExplanation`, `Constraints`).
- **Why It Matters:** This is the system's actual justification for preferring one option. Without it the user sees a verdict ("prefer X") and a single free-text rationale, but cannot see why X beat the others, what concerns qualify it, or what was assumed.
- **Current Visibility:** Viewer renders only `recommendation.optionId`, `rationale`, and evidence. The richer fields are serialized to the client but never read by any component.
- **Missing Visibility:** Supporting factors, concerns, assumptions, alternative explanations, and the per-option evaluation table.
- **Recommended Exposure Mechanism:** Render the factor/concern/assumption/alternative lists in the recommendation card, plus an "Option Evaluations" table (rank, score, constraints, summary).

#### GEN-2 — Per-option score and explainable score breakdown are hidden
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/RecommendationService.cs:185-202` (`Score`), `:332-343` (`ScoreExplanationFor`); model `OptionEvaluation.cs:10-12`; not rendered anywhere
- **Hidden Semantic Concept:** Each option receives a numeric `Score`, a human-readable `ScoreExplanation` ("Score N = benefits + consequences + strengths + priority adjustment − costs − risks − dependencies − disqualifying constraints"), and a `Rank` — the ranking basis that determines the recommendation.
- **Why It Matters:** Ranking is the core function of this slice. The code goes out of its way to produce a defensible explanation string the user never sees.
- **Current Visibility:** None in UI; `score`/`rank`/`scoreExplanation` exist on the TS type but `optionEvaluations` is never accessed.
- **Missing Visibility:** Each option's score, rank, and score-component explanation.
- **Recommended Exposure Mechanism:** Score + rank badges on each option card; `scoreExplanation` as expandable detail.

#### GEN-3 — "No recommendation" outcome and its specific cause are not distinctly surfaced
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/RecommendationService.cs:28-72,247-275`; UI `DecisionProposalViewer.tsx:55,83`, `DecisionOptionComparison.tsx:36-40`
- **Hidden Semantic Concept:** The system can withhold a recommendation for four semantically different reasons (every option disqualified / insufficient evidence / unresolved contradiction requiring human review / excessive uncertainty or tradeoff parity), distinguished by `Mode = NoRecommendation`.
- **Why It Matters:** These are materially different states demanding different user action; the distinction drives whether a human must intervene.
- **Current Visibility:** Generic `diagnostics.hasRecommendation` flag; the comparison just omits the "Recommended" badge. The reason lives only inside free-text rationale; `Mode` is not rendered.
- **Missing Visibility:** Explicit "Recommendation withheld" state, the categorical reason, and that human review is required.
- **Recommended Exposure Mechanism:** Render `recommendation.mode`; on `NoRecommendation` show a dedicated banner with the categorical reason and next step.

#### GEN-4 — Option comparison drops relative strengths/weaknesses, unique advantages/risks, disqualifying constraints
- **Severity:** High
- **Location:** projection `src/CommandCenter.Decisions/Services/DecisionReviewService.cs:73-93`; full data `OptionComparisonService.cs:32-39,45-234`; models `DecisionTradeoffComparison.cs:3-10` vs. `DecisionOptionComparisonItem.cs:3-9`; UI `DecisionOptionComparison.tsx:41-43`
- **Hidden Semantic Concept:** Per option, `RelativeStrengths`, `RelativeWeaknesses`, `UniqueAdvantages`, `UniqueRisks`, `DisqualifyingConstraints` are computed; the review projection collapses each option to only `Benefits`/`Costs`/`IsRecommended`/`Evidence`.
- **Why It Matters:** This is the explicit cross-alternative comparison reasoning — exactly what a selection decision is about — discarded before reaching the comparison view.
- **Current Visibility:** Title, description, recommended badge, benefits, costs, evidence. Relative/unique/disqualifying fields never reach the client.
- **Missing Visibility:** Relative strengths/weaknesses, unique advantages/risks, disqualifying constraints per option.
- **Recommended Exposure Mechanism:** Add these fields to the projected comparison item from stored `TradeoffComparisons`; render as labeled sections.

#### GEN-5 — Disqualifying constraints that exclude an option from recommendation are invisible
- **Severity:** Critical
- **Location:** `src/CommandCenter.Decisions/Services/RecommendationService.cs:22-24,200,308-310`; `OptionComparisonService.cs:210-234`; absent from `DecisionReviewService.cs:73-93` and `DecisionOptionComparison.tsx`
- **Hidden Semantic Concept:** An option carrying any `DisqualifyingConstraint` is removed from the viable set entirely (`viable = evaluations.Where(e => e.Constraints.Count == 0)`) and penalized −100/constraint; the constraint text states it is "not execution guidance until explicitly resolved by a human."
- **Why It Matters:** A hard gate silently eliminates alternatives. A user cannot tell that an option was *disqualified by constraint* vs. *merely scored lower* — an outcome-determining distinction with an explicit human-resolution requirement.
- **Current Visibility:** None. The excluded option still appears with benefits/costs and no indication it was disqualified or why.
- **Missing Visibility:** Per-option "Disqualified" status, the constraint statements, and the human-resolution requirement.
- **Recommended Exposure Mechanism:** Surface `Constraints`/`DisqualifyingConstraints` as a prominent "Disqualified — requires human resolution" marker.

#### GEN-6 — Rejected (validation-failed) options and rejection reasons are dropped from the proposal view
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/OptionGenerationService.cs:20,38-47,68-81`; validation `OptionValidationService.cs:9-58`; stored as `GenerationDiagnostics` (`DecisionGenerationService.cs:130`) but not rendered in `DecisionProposalViewer.tsx`
- **Hidden Semantic Concept:** Candidate options are validated and some rejected (MissingTitle/Description, NonActionable, MissingEvidence, EvidenceUnrelated, Duplicate). Rejection reasons are placed in `GenerationDiagnostics`.
- **Why It Matters:** These are discarded alternatives the system considered and threw out. The user sees only survivors and cannot tell alternatives were generated and rejected, nor why.
- **Current Visibility:** Rejected options never become `proposal.Options`; the `GenerationDiagnostics` "Rejected …" strings are persisted but not rendered (viewer shows a different `diagnostics.warnings` field).
- **Missing Visibility:** The set of rejected options and their validation-issue reasons; duplicate-rejection count.
- **Recommended Exposure Mechanism:** Render `proposal.generationDiagnostics` / a "Rejected options" section with each option's validation issues.

#### GEN-7 — Tradeoff impact, risk severity, and "unknown" uncertainty flags are flattened to plain strings
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/DecisionGenerationService.cs:96` (collapses to Benefit/Cost strings); models `DecisionBenefit.cs:6-8` (`Impact`), `DecisionRisk.cs:6-8` (`Severity`,`IsUnknown`); UI `DecisionProposalViewer.tsx:165-169`
- **Hidden Semantic Concept:** Analyzed options carry graded `TradeoffImpact` (Low/Medium/High/Blocking) and `TradeoffSeverity` plus an explicit `IsUnknown` risk flag; scoring treats `Blocking` and unknown risks specially (`RecommendationService.cs:195-198,291-297`).
- **Why It Matters:** Magnitude of a benefit/cost and whether a risk is a known quantity vs. acknowledged unknown is decision-relevant uncertainty. Plain "Benefit/Cost" strings make a Blocking cost look identical to a Low one.
- **Current Visibility:** Plain Benefit/Cost text; `AnalyzedOptions` stored but not projected; TS type has no impact/severity field.
- **Missing Visibility:** Impact/severity grades and the `IsUnknown` risk flag.
- **Recommended Exposure Mechanism:** Surface impact/severity/unknown as badges in tradeoff and option views.

#### GEN-8 — Priority-directive adjustments silently change option scores
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/RecommendationService.cs:154-170,346-370`
- **Hidden Semantic Concept:** An "IncreasePriority"/"DecreasePriority" directive in the generation context adjusts each option's score (+2/−2/−1) and injects synthetic strengths/weaknesses, changing the ranking.
- **Why It Matters:** An external directive materially alters which option is recommended; the user should know the ranking was tilted by a directive rather than intrinsic tradeoffs.
- **Current Visibility:** Only inside the unexposed `ScoreExplanation` ("+ priority adjustment N") and injected strings — none rendered.
- **Missing Visibility:** That a priority directive applied, its direction, and per-option effect.
- **Recommended Exposure Mechanism:** Once evaluations are surfaced (GEN-2), call out the priority-adjustment component and its source directive.

#### GEN-9 — Candidate signals driving recommendation suppression are shown only as a count
- **Severity:** Medium
- **Location:** UI `src/CommandCenter.UI/src/features/decisions/DecisionCandidateBrowser.tsx:142-144`; consumed in `RecommendationService.cs:285-289`, `OptionComparisonService.cs:222-223`
- **Hidden Semantic Concept:** Candidate `Signals` include kinds like "Contradiction"/"Constraint"; an unresolved contradiction forces NoRecommendation, a constraint can produce disqualifying constraints.
- **Why It Matters:** These signals are the upstream evidence that later blocks/shapes the recommendation; reducing them to a count hides, e.g., a contradiction that will prevent any recommendation downstream.
- **Current Visibility:** `selectedCandidate.signals.length` only; kinds/statements not shown.
- **Missing Visibility:** Signal kinds and statements, especially Contradiction/Constraint.
- **Recommended Exposure Mechanism:** List candidate signals (kind + statement), flagging contradiction/constraint signals.

#### GEN-10 — Recommendation rationale embeds only the first losing alternative
- **Severity:** Low
- **Location:** `src/CommandCenter.Decisions/Services/RecommendationService.cs:88-98,102` (only `alternatives[0]` enters `Rationale`); full set in `AlternativeExplanations` (`:109`)
- **Hidden Semantic Concept:** Each non-winning option has a generated "{id} lost because {LosingRationale}" explanation; only the first is concatenated into the rendered rationale.
- **Why It Matters:** The complete "why each alternative lost" set is the comparative justification; the user effectively sees the loss reason for at most one alternative.
- **Current Visibility:** Single embedded clause in `rationale`; `alternativeExplanations` reaches the client but is not rendered.
- **Missing Visibility:** The per-alternative "lost because" explanations beyond the first.
- **Recommended Exposure Mechanism:** Render the full `alternativeExplanations` list (covered by GEN-1).

---

### Decision Governance / Lifecycle / Resolution / Refinement / Certification

#### GOV-1 — Stale-authority rejection blocks resolution but the "authority is stale" state is never shown beforehand
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/DecisionResolutionService.cs:353-417` vs. `src/CommandCenter.UI/src/features/decisions/DecisionResolutionPanel.tsx:87-107`
- **Hidden Semantic Concept:** Resolution is a governance gate: if the reviewed package/proposal fingerprint no longer matches current content, resolution is blocked as "stale authority" unless `AcknowledgeStaleAuthority` is set. Four distinct stale outcomes exist, plus a *silent downgrade* (`:399-405`) where a mismatched package is dropped to `null` so the decision is recorded with no authority package.
- **Why It Matters:** The user is asserting human authority. Whether that authority rests on the exact reviewed artifact, a silently-dropped package, or an acknowledged-stale override changes what the resolved decision *means*.
- **Current Visibility:** Panel computes only `packageMatchesProposal` and shows a generic warning; never sends `acknowledgeStaleAuthority`; the four stale errors surface only as a raw conflict string; the null-package downgrade is invisible.
- **Missing Visibility:** Which stale condition occurred; that an override is possible/required; that the package linkage was dropped.
- **Recommended Exposure Mechanism:** Surface a typed "resolution authority state" (Current / StaleProposal / StalePackage / PackageDropped / OverrideRequired) and render an explicit acknowledge-stale control.

#### GOV-2 — Recommendation divergence is recorded but the recommended-vs-chosen delta is never captured or shown
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/DecisionResolutionService.cs:78-81,103`; UI `DecisionResolutionPanel.tsx:336,175-179`
- **Hidden Semantic Concept:** The system derives "the human chose an option other than the system's recommendation" and persists `recommendationDiverged` — a governance signal that a human overrode machine reasoning.
- **Why It Matters:** Later readers see only an "Override recorded" boolean badge; they cannot see what was recommended vs. chosen, nor the superseded rationale.
- **Current Visibility:** Generic pre-resolution note + post-resolution "Override recorded" badge; the recommended option id is not persisted with the divergence flag.
- **Missing Visibility:** The recommended→selected option pair and the superseded recommendation rationale.
- **Recommended Exposure Mechanism:** Persist `recommendedOptionId` + rationale on `DecisionResolution`; render the delta on the resolved-decision card.

#### GOV-3 — Refinement directive inference and regeneration-scope decision expose no "why"
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/RefinementAnalysisService.cs:42-77,93-136`
- **Hidden Semantic Concept:** The service decides *what will be regenerated* (options/tradeoffs/recommendation/full) by pattern-matching reviewer guidance against fixed keyword lists, then cascades into scope booleans, with an unmatched-guidance fallback to ClarifyGoal (`:106-109`).
- **Why It Matters:** The regeneration scope determines how much of the proposal is rewritten. A reviewer can't tell that "blocking" silently triggered IncreasePriority (cascading into tradeoff + recommendation re-evaluation), nor that unmatched guidance fell back to ClarifyGoal.
- **Current Visibility:** Resulting directive types/summaries + scope chips are shown; the triggering keyword and the no-match fallback are indistinguishable.
- **Missing Visibility:** The matched term per directive and an explicit "guidance unrecognized; defaulted to clarify" signal.
- **Recommended Exposure Mechanism:** Include the matched keyword (or "fallback/unmatched") in each directive and render it.

#### GOV-4 — HumanAuthoringBurden classification is a binary judgment with hidden criteria
- **Severity:** Low
- **Location:** `src/CommandCenter.Decisions/Services/DecisionRefinementService.cs:478-492`
- **Hidden Semantic Concept:** Each refinement is concluded `FullRewrite` or `MinorEdit` based solely on whether any of five generated-content fields changed.
- **Why It Matters:** The label is shown as authoritative but its basis (which changed field triggered "FullRewrite") is invisible, so the user can't reconcile "I only edited context" with a "FullRewrite" verdict.
- **Current Visibility:** Label value rendered; classification rule and triggering field not.
- **Missing Visibility:** Which changed field(s) drove the classification.
- **Recommended Exposure Mechanism:** Pair the burden label with the deciding changed-field list (already available in the revision).

#### GOV-5 — Certification evidence checks encode authority/eligibility constraints whose failure reasons are opaque
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/DecisionCertificationService.cs:161-197,283-294,384-390`; UI `DecisionCertificationPanel.tsx:144-178,194-197`
- **Hidden Semantic Concept:** Composite gates compute non-obvious booleans: `authority-boundaries` fails if a resolved decision's `ResolvedBy` is a *system* actor (IsSystemAuthority); `execution-consumption` passes only when projection counts exactly correlate with accepted-resolved-decisions-without-blocking-findings — a compound equivalence that can fail for two opposite reasons (orphan projection vs. missing projection).
- **Why It Matters:** On failure, certification flips to Blocked, but the user sees a static one-line detail and *every* decision id attached indiscriminately, not the offending decision nor which side of the equivalence broke. The rule "system actors may not establish authority" is never stated where the user can act.
- **Current Visibility:** Area, static `detail` (identical on pass/fail), pass/fail, and all decision ids.
- **Missing Visibility:** The specific failing artifact(s) per check and the human-readable rule violated.
- **Recommended Exposure Mechanism:** Make `detail` reason-specific on failure; scope `relatedDecisionIds` to actual violators.

#### GOV-6 — Decision lifecycle transition rules are never exposed; users discover them via 409 conflicts
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/DecisionLifecycleRules.cs:8-106`; client guards `DecisionResolutionPanel.tsx:19`, `DecisionRefinementPanel.tsx:22`
- **Hidden Semantic Concept:** The full lifecycle state machine — legal transitions plus constraints ("Accepted must go to Resolved", "Deferred must go to UnderReview", "Promoted candidates can only Expire") — governs the whole feature.
- **Why It Matters:** Users initiate operations against this model but can only discover constraints by triggering a conflict; the UI hard-codes duplicated guesses of "what's allowed next," and divergence from the backend rules is invisible.
- **Current Visibility:** Current-state badge + client-side guards only. No surface states "from state X, allowed next states are …".
- **Missing Visibility:** The set of legal next transitions for the current artifact and the outcome constraints enforced.
- **Recommended Exposure Mechanism:** Expose the allowed-transition set (and outcome constraints) for the current state so the UI renders eligibility rather than re-deriving it.

#### GOV-7 — Assimilation eligibility constraint is enforced silently
- **Severity:** Low
- **Location:** `src/CommandCenter.Decisions/Services/DecisionOperationalContextAssimilationService.cs:38-46,83-86`; UI `DecisionResolutionPanel.tsx:275-282`
- **Hidden Semantic Concept:** A constraint — assimilation can only be proposed for `Resolved` decisions with resolution detail — plus an authority-boundary statement that the package never mutates operational context.
- **Why It Matters:** The UI enables "Create Package" on `Resolved && Accepted`, but the backend gate is "Resolved with resolution" regardless of outcome, so eligibility differs between UI guess and backend rule; the precondition surfaces only as a 409.
- **Current Visibility:** Advisory-boundary text shown; the eligibility precondition is not.
- **Missing Visibility:** The eligibility precondition (resolved + has-resolution) as a stated constraint before the action.
- **Recommended Exposure Mechanism:** Surface assimilation eligibility as an explicit precondition state on the decision.

#### GOV-8 — Governance health verdict's causal link to blocking findings is only inferable
- **Severity:** Low
- **Location:** `src/CommandCenter.Decisions/Services/DecisionGovernanceService.cs:75-88`; UI `DecisionGovernancePanel.tsx:57-62,164`
- **Hidden Semantic Concept:** `health = Blocked` is derived *because* one or more findings carry `BlocksExecutionProjection`; advisory findings cannot block.
- **Why It Matters:** The health verdict is the top-line governance conclusion, but the panel shows Health and a separate blocking count without stating the blocking findings are the *cause*, so the user can't tell which findings are load-bearing.
- **Current Visibility:** Health, finding count, blocking count side by side; per-finding "Blocks execution projection" vs "Advisory" tag. The aggregation rule is implicit.
- **Missing Visibility:** Explicit health-derivation rule / which findings determine the verdict.
- **Recommended Exposure Mechanism:** Render the health rationale ("Blocked by N execution-blocking findings; M advisory findings do not affect health").

---

### Decision Quality / Signals / Reports / Burden / Projections

#### QUA-1 — Quality score value is hidden in UI; scoring formula invisible everywhere
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/DecisionQualityAssessmentService.cs:22,102-118`; model `DecisionQualityAssessment.cs:11`; UI omits it at `DecisionQualityPanel.tsx:156-159`
- **Hidden Semantic Concept:** Every assessment derives a 0–100 score from a 50 baseline plus per-signal contributions (Critical ±35, High ±25, Medium ±15, Low ±8, Info ±3, signed by direction) — the quantitative conclusion driving the rating.
- **Why It Matters:** The user sees only a categorical rating; two decisions with the same rating can have very different scores (64 "Mixed" vs 66 "Good" differ by one Low signal).
- **Current Visibility:** UI shows `rating` only; markdown shows "Score" without how it was computed; API returns `Score`.
- **Missing Visibility:** The score number in the UI; the baseline-plus-contribution decomposition; a statement that the score is heuristic.
- **Recommended Exposure Mechanism:** Show the score beside the rating; add a per-signal point-contribution breakdown from `ScoreContribution`.

#### QUA-2 — Critical-negative-signal override of rating is hidden
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/DecisionQualityAssessmentService.cs:120-134`
- **Hidden Semantic Concept:** A veto rule: any negative Critical signal forces the rating to Poor before the score-band switch, independent of score.
- **Why It Matters:** A user seeing "Poor" alongside a score that would map to "Mixed" has no way to know the rating was vetoed by one specific signal.
- **Current Visibility:** None — neither UI, markdown, nor diagnostics state the override fired or which signal triggered it.
- **Missing Visibility:** That a veto rule exists; whether it fired; which signal triggered it.
- **Recommended Exposure Mechanism:** Emit a diagnostic when the override fires ("Rating forced to Poor by critical signal {id}") and surface it.

#### QUA-3 — Rating threshold bands are undisclosed constraints
- **Severity:** Medium
- **Location:** `DecisionQualityAssessmentService.cs:127-133`; `DecisionQualityReportService.cs:247-257`
- **Hidden Semantic Concept:** The boundaries (≥85/≥65/≥40) converting a number into a categorical judgment, plus a distinct `Unknown`-at-zero rule for report/trend averages.
- **Why It Matters:** Users cannot interpret how close a decision is to the next band, nor why an empty repository reports "Unknown" vs "Poor"; the same label is produced by two different threshold tables.
- **Current Visibility:** None of the thresholds are shown.
- **Missing Visibility:** The numeric band boundaries; the average/Unknown-at-zero special case.
- **Recommended Exposure Mechanism:** Document bands inline (tooltip/legend) and emit the active threshold in diagnostics.

#### QUA-4 — "Effective burden" is a hidden max-severity selection
- **Severity:** High
- **Location:** `DecisionQualityReportService.cs:220-240,136-140`; duplicated `HumanAuthoringBurdenService.cs:87-97,131-142`; UI buckets `DecisionQualityPanel.tsx:96-99`
- **Hidden Semantic Concept:** When a decision has several burden signals, the report collapses them to one "effective" burden = highest-weighted (GenerationBypassed 5 > FullRewrite 4 > MajorRefinement 3 > MinorEdit 2 > ReviewOnly 1 > Unknown 0). The report's burden bucket counts depend entirely on this collapse.
- **Why It Matters:** A user reading "1 full rewrite" cannot tell the decision also had review-only signals that were overridden, nor that "worst wins" is the rule.
- **Current Visibility:** Bucket counts shown; selection rule and weights not. Per-decision burden signals appear only in markdown.
- **Missing Visibility:** That each decision is reduced by a max-weight rule; the weight ordering; overridden lower-burden signals.
- **Recommended Exposure Mechanism:** State the "worst-signal-wins" rule near the burden summary; show both the effective burden and the contributing signals.

#### QUA-5 — Recommendation-stability signal depends on hidden cross-decision history and a "≥2 divergences" threshold
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/DecisionQualitySignalService.cs:114-156`
- **Hidden Semantic Concept:** The signal's presence/direction is gated by aggregate history (≥2 accepted recommendation-bearing decisions) and a divergence threshold (High-negative only when this decision diverged AND divergenceCount ≥2; Low-positive only when divergenceCount == 0). Not derivable from the single decision under view.
- **Why It Matters:** A user inspecting one decision sees the signal (or its absence) without knowing it reflects a fleet-wide pattern and count gates; the same decision yields different signals as siblings change. The "diverged once" middle case silently produces no signal.
- **Current Visibility:** The resulting summary/detail if present; thresholds and the silent-empty middle case invisible.
- **Missing Visibility:** The ≥2 thresholds; that signal absence is itself meaningful; the cross-decision dependency.
- **Recommended Exposure Mechanism:** Include the threshold logic in the signal detail; emit a neutral/info signal for the in-between case.

#### QUA-6 — Projection-kind classification is a hidden keyword heuristic that decides constraint vs. directive routing
- **Severity:** High
- **Location:** `src/CommandCenter.Decisions/Services/DecisionProjectionService.cs:388-439,41-89,371-386`; UI `ExecutionDecisionInfluencePanel.tsx:109-110`
- **Hidden Semantic Concept:** Whether an accepted decision projects as architectural constraint, technology choice, repository convention, workflow policy, or implementation directive — and thus whether it is enforced as a constraint vs. a softer directive — is decided by case-insensitive substring matching on title/context/statement/evidence (with substring checks like "priority"/"before "/"first " promoting to priority).
- **Why It Matters:** This determines how a decision influences execution. The user can't see why "use Tauri" became a TechnologyChoice constraint while another became an ImplementationDirective, nor that the word "first" promoted a statement to priority.
- **Current Visibility:** Resulting `projectionKind`/statement type shown, never *why* or which keyword matched.
- **Missing Visibility:** The matched term / classification fallback; that classification is heuristic substring matching.
- **Recommended Exposure Mechanism:** Record the matched term (or "classification fallback") on each projected statement and surface it as the basis.

#### QUA-7 — Excluded/superseded decisions and their reasons are not surfaced in the influence UI
- **Severity:** High
- **Location:** `DecisionProjectionService.cs:130-191,485-515`; UI `ExecutionDecisionInfluencePanel.tsx:43-84`
- **Hidden Semantic Concept:** The set of decisions deliberately excluded from execution influence, with structured reasons ("Resolution outcome is Rejected.", "Decision is superseded by …", "Blocking governance finding prevents execution projection.").
- **Why It Matters:** "What did NOT influence execution, and why" is exactly what the user needs; the panel shows only what was included, implying nothing was withheld. A governance-blocked decision silently vanishes.
- **Current Visibility:** Diagnostics strings persisted and `trace.diagnostics` rendered, but the structured excluded/superseded lists with state/outcome are not shown as such.
- **Missing Visibility:** An "Excluded decisions" section with reason/state/outcome; the superseded-but-not-marked case.
- **Recommended Exposure Mechanism:** Render `excludedDecisions`/`supersededDecisions` (with reasons) as their own section, mirroring the included list.

#### QUA-8 — Conflict detection between projected decisions is computed but not surfaced in the influence panel
- **Severity:** Medium
- **Location:** `DecisionProjectionService.cs:654-767,19-39,593-600`; UI renders no conflicts section
- **Hidden Semantic Concept:** The system derives that two accepted decisions contradict each other (or conflict with the execution request) via prefix/subject parsing.
- **Why It Matters:** Contradictions among governing decisions are a high-value conclusion; showing fingerprint and included statements but omitting conflicts hides a material judgment affecting trust and action.
- **Current Visibility:** Conflicts in the API projection and persisted markdown, but the decisions/influence UI does not display them.
- **Missing Visibility:** The conflict list (which decisions, what subject, conflicting excerpt); that detection is heuristic prefix/subject matching.
- **Recommended Exposure Mechanism:** Add a Conflicts section listing each `ExecutionDecisionConflict` with both sides and sources; note the heuristic nature.

#### QUA-9 — Quality trend direction hides that it is a raw average comparison with no significance
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/DecisionQualityReportService.cs:50-92,176-204`; UI `DecisionQualityPanel.tsx:116-121`
- **Hidden Semantic Concept:** "Trend" is the sign of (current avg − previous avg), where current = latest assessment per decision and previous = second-latest; decisions with only one assessment inflate current but have no previous counterpart, biasing the comparison. Any non-zero difference flips direction.
- **Why It Matters:** A user reads "Trend: Positive" as a meaningful improvement, but any 0.01 difference flips it and the two averages are computed over different-sized, non-paired populations — uncertainty presented as a definite conclusion.
- **Current Visibility:** Direction, current/previous ratings, assessment count; the set-construction asymmetry and lack of a significance threshold are invisible.
- **Missing Visibility:** That direction has no magnitude threshold; that previous/current sets differ in size/membership; the count of decisions lacking a prior assessment.
- **Recommended Exposure Mechanism:** Surface both averages and both set sizes; add a diagnostic for paired-vs-unpaired decisions.

#### QUA-10 — "ReviewOnly" burden inferred from absence is presented as a positive signal without disclosing it is an inference
- **Severity:** Medium
- **Location:** `src/CommandCenter.Decisions/Services/HumanAuthoringBurdenService.cs:62-72,20-31`; direction mapping `DecisionQualitySignalService.cs:93-99`
- **Hidden Semantic Concept:** A favorable low-burden judgment (ReviewOnly) is derived from the *absence* of recorded refinement artifacts — an inference that the human merely reviewed, which could instead mean effort happened but was never recorded.
- **Why It Matters:** The favorable classification (which adds positive points) rests on missing data, not observed low effort; the user is not told the conclusion is absence-based with inherent uncertainty.
- **Current Visibility:** Burden label/summary in markdown; bucket count in UI. The "without persisted … evidence" caveat appears only in markdown and isn't tied to the positive scoring impact.
- **Missing Visibility:** That ReviewOnly here is inferred from absence; the uncertainty; its link to a positive score contribution.
- **Recommended Exposure Mechanism:** Mark absence-inferred burdens distinctly ("ReviewOnly (inferred from no recorded refinements)") and reflect the uncertainty in the signal card.

---

### Decision Sessions

#### SES-1 — Entire decision-session lifecycle is computed and projected but never surfaced in any UI
- **Severity:** Critical
- **Location:** `src/CommandCenter.Middle/Projections/RepositoryProjectionService.cs:122-167` (populates `RepositoryDecisionSessionSummary`) vs. no consumer anywhere in `src/CommandCenter.UI/src` (UI consumes only `continuitySummary`/`reasoningSummary` from the same dashboard projection, e.g. `Sidebar.tsx:106`)
- **Hidden Semantic Concept:** Whether a transfer is recommended (`LifecycleDecision`), whether it's permitted (`TransferEligibilityStatus`), the coherence/transfer-pressure driving it, the active session identity/state, and per-dimension health.
- **Why It Matters:** This is the headline outcome of the entire slice. The backend computes it and maps it onto the dashboard projection, yet the user has no window into what the governance system believes about their session.
- **Current Visibility:** Reachable only by manually hitting REST (`/lifecycle/projection`, `/workflow/summary`). Zero UI. (The `DecisionLifecycleTab`/`DecisionGovernancePanel` are about decision *generation*, not sessions.)
- **Missing Visibility:** A UI surface rendering `RepositoryDecisionSessionSummary` (decision, eligibility, coherence, transfer pressure, health, lineage).
- **Recommended Exposure Mechanism:** A decision-session governance panel/inspector bound to the already-projected fields.

#### SES-2 — Transfer execution exists but has no trigger and its result is never observable
- **Severity:** Critical
- **Location:** `src/CommandCenter.DecisionSessions/Services/DecisionSessionTransferService.cs:18-142,173`; endpoints expose only GET `/transfers`,`/transfers/history`,`/transfers/diagnostics` (`DecisionSessionEndpoints.cs:138-151`) — no `MapPost`; `ExecuteAsync` has zero external callers
- **Hidden Semantic Concept:** The transfer state machine (Active → TransferPending → continuity capture → replacement activation → Transferred), its per-step events, blocked-result reasoning (`CreateBlockedResult`), and failure diagnostics.
- **Why It Matters:** A meaningful state-mutating operation with multi-step transitions and a rich result is computed but never presented; the user can neither initiate nor watch a transfer, nor learn why one was blocked or failed.
- **Current Visibility:** Only historical transfer records via GET, after the fact, via raw API.
- **Missing Visibility:** The live transfer decision/transition, why blocked (eligibility findings), and failure cause.
- **Recommended Exposure Mechanism:** A POST endpoint returning `DecisionSessionTransferResult`, plus UI rendering its events/diagnostics and blocked-reason.

#### SES-3 — Lifecycle decision exposes the verdict but the score breakdown is split into a separate diagnostics call
- **Severity:** High
- **Location:** `DecisionSessionLifecyclePolicy.cs:66-83,159-175`; endpoint `/lifecycle/policy` returns only `.Evaluation` (`DecisionSessionEndpoints.cs:83-91`), assessments live only in `.Diagnostics` at a different endpoint
- **Hidden Semantic Concept:** Which inputs (coherence, transfer pressure, fragmentation, growth, cache-miss risk, economic values) drove the transfer-vs-reuse decision and by how much.
- **Why It Matters:** The user sees "Transfer score 0.62 exceeds reuse score 0.58" but not why — the evidence is in a separate diagnostics object no UI consumes.
- **Current Visibility:** Reason string + two aggregate scores via API; factor breakdown only via `/lifecycle/policy/diagnostics`.
- **Missing Visibility:** Co-located, human-readable factor attribution alongside the decision.
- **Recommended Exposure Mechanism:** Surface `ContributingFactors`/score assessments with the decision in one view.

#### SES-4 — "Eligible" conflates *permitted* with *recommended* and silently defaults to Transfer when policy is unavailable
- **Severity:** High
- **Location:** `DecisionSessionTransferEligibilityService.cs:191-207,216-225,162-166`; same pattern `DecisionSessionRecoveryService.cs:553-562`
- **Hidden Semantic Concept:** When policy evaluation cannot run, the system synthesizes a Transfer decision (provisional/degraded), and `Eligible` means execution-may-proceed, not preferable.
- **Why It Matters:** A user seeing eligibility derived from an unavailable policy has no signal that the underlying decision is a fabricated default rather than a real Transfer recommendation — hidden uncertainty/degraded state.
- **Current Visibility:** The disclaimer exists only inside `Diagnostics.Assumptions` text and a buried reason string; the synthesized Transfer surfaces as a normal decision.
- **Missing Visibility:** Explicit "policy-unavailable / provisional" flag distinct from a genuine Transfer verdict.
- **Recommended Exposure Mechanism:** A distinct provisional/degraded eligibility state, surfaced rather than buried in assumptions prose.

#### SES-5 — Recovery path selection is computed but unreachable as a persisted decision
- **Severity:** High
- **Location:** `DecisionSessionRecoveryService.cs:348-468,471-493,150-207,24`; persisted recovery invoked ONLY by `DecisionSessionRecoveryHostedService.cs:19`; the `/recovery` endpoint calls `GetRecoveryAsync` (persist:false)
- **Hidden Semantic Concept:** Recovery classifies each transfer's interruption (`InterruptedBeforeArtifact`, `InterruptedWithArtifact`, `CompletedWithMissingActiveReplacement`, `Failed`, pending statuses) and reports rebuilt/skipped/failed outcomes with evidence.
- **Why It Matters:** Recovery makes meaningful per-transfer decisions with distinct selected paths; the reasoning is computed but the authoritative persisted recovery only happens out-of-band, and nothing renders the classification.
- **Current Visibility:** Findings/statuses via raw GET; no UI; user-initiated persisted recovery impossible via API.
- **Missing Visibility:** Recovery path + trigger + resulting state; a user-initiated persisted-recovery action.
- **Recommended Exposure Mechanism:** POST recovery endpoint + UI listing each transfer's recovery classification with evidence.

#### SES-6 — Certification pass/fail and overall health verdict are not surfaced for decision sessions
- **Severity:** High
- **Location:** `DecisionSessionCertificationService.cs:40-114,662-675`; `DecisionSessionObservabilityService.cs:400-419,475-644,634-639`; endpoints `/certification`,`/lifecycle/health` (`DecisionSessionEndpoints.cs:118-201`) with no UI consumer
- **Hidden Semantic Concept:** Whether the decision-session governance trunk certifies as sound, which invariant failed, per-dimension Healthy/Warning/Unhealthy, and contradiction detection ("Healthy while evidence contains contradictory findings").
- **Why It Matters:** A certification failure is a major verdict about whether the system's own state is trustworthy; the user sees neither pass/fail nor the failed finding.
- **Current Visibility:** Raw API only; no UI binding (the UI's `useDecisionCertification` is decision-*generation* certification).
- **Missing Visibility:** Certification result, failed findings, overall health verdict in the UI.
- **Recommended Exposure Mechanism:** UI panel binding `DecisionSessionCertificationReport` + `DecisionSessionHealthReport`.

#### SES-7 — Influence trace (per-signal evidence chain behind the decision) is built but unconsumed
- **Severity:** Medium
- **Location:** `DecisionSessionObservabilityService.cs:255-398`; endpoint `/lifecycle/influence` (`DecisionSessionEndpoints.cs:113-116`) with no UI consumer
- **Hidden Semantic Concept:** The end-to-end "why" — how each measured signal (size, cache, economics, coherence, transfer pressure, policy, eligibility, artifact, transfers, recovery) contributed to the lifecycle decision and eligibility.
- **Why It Matters:** This is precisely the evidence/alternatives/constraints chain explaining the outcome, fully assembled and discarded at the UI boundary.
- **Current Visibility:** Raw API only.
- **Missing Visibility:** An explainability/influence view.
- **Recommended Exposure Mechanism:** Render `DecisionSessionInfluenceTrace` signals as an evidence breakdown.

#### SES-8 — Snapshot-rebuild ("was invalid and rebuilt") events are demoted to warning strings
- **Severity:** Medium
- **Location:** Repeated: `DecisionSessionTransferEligibilityService.cs:26-33`, `DecisionSessionLifecyclePolicy.cs:27-34`, `DecisionSessionEconomicsService.cs:25-32`, `DecisionSessionMetricsService.cs:29-36`, `DecisionSessionCoherenceService.cs:31-35`
- **Hidden Semantic Concept:** A prior persisted belief was discarded and a fresh one substituted (corrupt → rebuilt) — a state transition the user may want to know about.
- **Why It Matters:** A rebuilt snapshot means earlier recorded conclusions were untrustworthy; surfacing it only as one warning among many obscures that a recovery-of-state event occurred.
- **Current Visibility:** A warning string buried in each snapshot's diagnostics (themselves unrendered per SES-1).
- **Missing Visibility:** A distinct "snapshot rebuilt" event/flag with which snapshot and why.
- **Recommended Exposure Mechanism:** Promote to a typed lifecycle/history event distinguishable from analysis warnings.

---

### Workflow Engine

#### WFL-1 — Authoritative workflow projection never reaches the UI
- **Severity:** Critical
- **Location:** `src/CommandCenter.UI/src/lib/executionWorkflow.ts:3-111` (only rail data source) vs. `src/CommandCenter.Workflow/Services/WorkflowProjectionService.cs:125-232`; endpoint `WorkflowEndpoints.cs:40-57`
- **Hidden Semantic Concept:** The backend derives `CurrentStage`, `ProgressState` (8 states incl. `Blocked`/`Failed`/`Recovering`/`AwaitingGate`/`WaitingForHuman`), `BlockingGate`, `RequiredHumanAction`, and per-decision `Reasoning`. The UI recomputes 5 fixed steps client-side from `repositoryState` + two booleans, never calling `/workflow` (verified: zero `/workflow` references in non-test UI source).
- **Why It Matters:** The user sees a coarse hardcoded label that is a parallel, simpler model than what the engine actually believes; the 7-stage canonical graph collapses to 5 UI steps with no Decision or OperationalContext representation, so the user cannot see the workflow is blocked on a decision or operational-context gate.
- **Current Visibility:** A CSS state class + generic `detail` string derived from repository execution state only.
- **Missing Visibility:** Actual `CurrentStage`, `ProgressState`, `RequiredHumanAction`, and stage-selection `Reasoning`.
- **Recommended Exposure Mechanism:** Add a UI API client for `GET /workflow`; drive the rail from the real projection, rendering the stage-selection reasoning as detail/tooltip.

#### WFL-2 — Blocked-gate reasoning and evidence are never surfaced
- **Severity:** Critical
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowGateCatalogService.cs:84-127,236-247`; exposed at `WorkflowEndpoints.cs:147-164` (`/workflow/gates`); no UI consumer
- **Hidden Semantic Concept:** Each open gate carries `Type`, `RequiredAction`, `SatisfyingCommands` (the exact command that unblocks it, e.g. `accept_execution_handoff`), a human-readable `Reason`, and a `WorkflowGateEvidence` record (source domain/artifact, timestamp, fingerprint).
- **Why It Matters:** A blocked gate is the single most important outcome-affecting state — it tells the user the system is waiting on *them* and exactly what to do. None of the reason text, satisfying command, or evidence is shown; the UI shows only a generic "Awaiting review."
- **Current Visibility:** Generic per-step `detail` strings hardcoded in `executionWorkflow.ts`.
- **Missing Visibility:** `gate.Reason`, `gate.RequiredAction`, `gate.SatisfyingCommands`, and gate `Evidence`.
- **Recommended Exposure Mechanism:** A gate panel consuming `/workflow/gates` showing each open gate's reason, satisfying command, and source evidence.

#### WFL-3 — Multi-dimensional workflow health is entirely unsurfaced
- **Severity:** High
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowHealthService.cs:72-113,259-487,289,304-312,387`; endpoint `WorkflowEndpoints.cs:442-459`; no UI consumer
- **Hidden Semantic Concept:** Health rolls up 5 dimensions (Projection, Recovery, Gates, Continuation, Preparation), each with Status (Healthy/Degraded/Blocked), Reason, Evidence, Diagnostics; overall = worst dimension. Degraded states encode real uncertainty (corrupted timeline recoverable from domain evidence; duplicate mechanical-progression risk; repository-ownership mismatch → Blocked).
- **Why It Matters:** This is the system's self-assessment of whether its workflow state can be trusted; the user has no way to learn the workflow is degraded or why.
- **Current Visibility:** None in the UI.
- **Missing Visibility:** `OverallStatus`, per-dimension Status/Reason/Diagnostics.
- **Recommended Exposure Mechanism:** A health badge + expandable dimension list consuming `/workflow/health`.

#### WFL-4 — Recovery path selection (trust vs. discard the persisted timeline) is decided silently
- **Severity:** High
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowRecoveryService.cs:23-85,66`; endpoints `/workflow/recovery`, `POST /workflow/recover` (`WorkflowEndpoints.cs:185-221`); `/workflow/history` runs recovery as a GET side effect (`WorkflowEndpoints.cs:68`); no UI consumer
- **Hidden Semantic Concept:** Recovery compares persisted vs. domain-derived timeline fingerprints and chooses match (recovered) / conflict (persisted discarded, domain rebuilt) / load-error (rebuilt), recording `DiscardedArtifacts`, `RecoveredArtifacts`, and a diagnostics narrative.
- **Why It Matters:** This can silently discard persisted workflow state (the user's recorded position). The trigger and outcome are outcome-affecting and never surfaced — and a plain history GET can trigger the discard invisibly.
- **Current Visibility:** None in the UI.
- **Missing Visibility:** `RecoveryRequired`/`Recovered`, `DiscardedArtifacts`, recovery diagnostics.
- **Recommended Exposure Mechanism:** Surface recovery diagnostics whenever `RecoveryRequired=true` or discarded artifacts are non-empty.

#### WFL-5 — Continuation decision (Advance vs. Stop) and its stop-reason are invisible
- **Severity:** High
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowContinuationService.cs:34-50,174-228,214,224`; endpoint `/workflow/continuation/evaluation` (`WorkflowEndpoints.cs:328-345`); no UI consumer; background `WorkflowContinuationHostedService`
- **Hidden Semantic Concept:** Continuation decides whether to auto-advance one stage by ANDing six conditions; on stop, `BuildStopReason` produces a precise cause ("Domain evidence has not yet reached {stage}…", "Multiple mechanical transitions are available… continuation requires exactly one.").
- **Why It Matters:** Whether the system will progress on its own or is waiting — and the precise blocker — is core to "what is the system doing / why isn't it moving." Background continuation can advance without user action, so the held-position reasons matter.
- **Current Visibility:** None in the UI.
- **Missing Visibility:** `Outcome` (Advance/Stop), `StopReason`, `IsWaitingForHuman`, the chosen transition.
- **Recommended Exposure Mechanism:** Show continuation `Outcome` + `StopReason` near the active stage.

#### WFL-6 — Certification failures (governance/authority-boundary violations) are not surfaced
- **Severity:** High
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowCertificationService.cs:94-176,150-153`; endpoints `/workflow/certification` GET/POST (`WorkflowEndpoints.cs:537-573`); no UI consumer
- **Hidden Semantic Concept:** Certification asserts authority-boundary invariants (preparation must not satisfy gates, continuation must not cross gates, domain evidence wins on recovery, history is reconstructable, …) producing `Certified`, `FailedFindingCount`, and per-finding Summary/Detail/Diagnostics.
- **Why It Matters:** A failed certification means the engine detected it may have violated its own governance guarantees — directly outcome-affecting for a governance tool. Decisions and reasoning *do* have certification panels; workflow does not.
- **Current Visibility:** None in the UI.
- **Missing Visibility:** `Certified`, `Failures`, failed-finding summaries/diagnostics.
- **Recommended Exposure Mechanism:** A workflow-certification panel mirroring the existing decision/reasoning panels.

#### WFL-7 — Preparation refusal/duplicate/skip outcomes are computed with reasons but not surfaced
- **Severity:** Medium
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowPreparationService.cs:44-58,375-393,395-450,289-373,429`; endpoint `/workflow/preparation/evaluation` (`WorkflowEndpoints.cs:385-402`); no UI consumer
- **Hidden Semantic Concept:** Preparation decides whether to auto-create reviewable artifacts (decision proposals, operational-context proposals, commit prep), emitting `Outcome` (Allowed/Refused/Skipped/Duplicate), a specific `Reason`, and the `duplicateEvidence` list.
- **Why It Matters:** When the system declines to prepare an artifact the user is waiting for, the cause (open gate, duplicate evidence, non-runnable state) determines what the user should do.
- **Current Visibility:** None in the UI.
- **Missing Visibility:** `Outcome`, `Reason`, `DuplicateEvidence`.
- **Recommended Exposure Mechanism:** Surface the preparation evaluation `Outcome`+`Reason` in the active-stage context.

#### WFL-8 — Decision-stage status is overridden by a hidden governance-block flag
- **Severity:** Medium
- **Location:** `src/CommandCenter.Workflow/Services/WorkflowDecisionService.cs:39-40,77,254-270`
- **Hidden Semantic Concept:** When decision governance health is Blocked or a finding blocks execution, the projected `Status` is forced to `AwaitingResolution` regardless of true lifecycle state, with `IsGovernanceBlocked` set; the distinct blocking findings sit in `governanceSignals`/diagnostics.
- **Why It Matters:** The surfaced status conflates "needs resolution" with "blocked by governance," hiding whether the holdup requires a different action.
- **Current Visibility:** None in the UI; even via API the override is only distinguishable through the `IsGovernanceBlocked` flag + diagnostics.
- **Missing Visibility:** `IsGovernanceBlocked` and the governance blocking signals when status is overridden.
- **Recommended Exposure Mechanism:** When `IsGovernanceBlocked`, label the stage governance-blocked and list the blocking findings rather than a bare "AwaitingResolution."

---

### Reasoning Subsystem

#### REA-1 — Materialization recommendation uses hidden hardcoded thresholds, then is relabeled euphemistically
- **Severity:** High
- **Location:** `src/CommandCenter.Reasoning/Services/ReasoningMaterializationReviewService.cs:141-159,112-114,161-173,85-88`; UI mapping `ReasoningMaterializationReviewPanel.tsx:114-129`
- **Hidden Semantic Concept:** The accept/recommend decision is a pure threshold function (`failedScenarioCount >= 2` → AddReadModelReport; `repeatedWorkflowCount >= 3` → AddDerivedCache; else RemainDerived). The UI then translates the real enum into vague phrases ("Materialization pressure observed", "Further review recommended"), so the user never sees the actual verdict (RemainDerived/AddDerivedCache/AddReadModelReport/PromoteToFirstClassEntity/RejectConcept).
- **Why It Matters:** This is the system's architectural judgment on promoting a reasoning concept to a persisted entity — a major governance decision. The user can't tell why a concept stayed derived, what count crossed which threshold, or even which outcome was chosen.
- **Current Visibility:** Relabeled phrase + summary; the counts appear only when outcome ≠ RemainDerived, so the common case shows nothing.
- **Missing Visibility:** The literal outcome enum; the scenario counts; the thresholds; the special elevated "Direction" risk that never changes the outcome.
- **Recommended Exposure Mechanism:** Show the literal outcome plus a "decision basis" line ("2 of N scenarios failed reconstruction; read-model-review threshold is 2") and stop masking the enum.

#### REA-2 — Reconstruction confidence (High/Medium/Low) is a bare label with no basis
- **Severity:** High
- **Location:** `src/CommandCenter.Reasoning/Services/ReasoningReconstructionService.cs:245-258`; displayed `ReasoningReconstructionPanel.tsx:42`, `ReasoningQueryPanel.tsx:192`
- **Hidden Semantic Concept:** Confidence is a 3-branch rule (High = event AND relationship evidence AND zero trace diagnostics; Medium = event OR relationship; else Low).
- **Why It Matters:** Confidence is the system's stated uncertainty about a reconstructed narrative — exactly what the audit targets. "Medium" could mean "rich events but a broken relationship" or "one orphan relationship and nothing else," rendered identically.
- **Current Visibility:** Just the label, alongside raw counts the user must reverse-engineer into the rule.
- **Missing Visibility:** Which branch was taken and why it wasn't higher.
- **Recommended Exposure Mechanism:** Emit a `confidenceRationale` from `CalculateConfidence` and render it next to the chip.

#### REA-3 — Reconstruction traversal direction and "reconstructed" nature aren't distinguished from authored history
- **Severity:** Medium
- **Location:** `ReasoningReconstructionService.cs:19-21,34-42`; UI `ReasoningReconstructionPanel.tsx:40-62`
- **Hidden Semantic Concept:** The reconstruction silently chooses Backward (default) vs Forward traversal from `query.Direction`, fundamentally shaping which evidence is reachable, then presents a flat narrative.
- **Why It Matters:** A user can't tell whether absent evidence means "nothing exists" or "the chosen direction couldn't reach it." Direction is a load-bearing assumption behind the conclusion.
- **Current Visibility:** Diagnostics surface `HistoricalAt` cutoff and "No cited evidence" (good), but not traversal direction.
- **Missing Visibility:** The direction used + a note that the narrative is reconstructed from reachable evidence in that direction only.
- **Recommended Exposure Mechanism:** Add a diagnostic ("Backward reconstruction: traced from target toward originating events").

#### REA-4 — Inferred capture decisions (skip/dedupe) and inferred-vs-authored provenance aren't surfaced
- **Severity:** Medium
- **Location:** `src/CommandCenter.Backend/Services/DecisionReasoningCaptureService.cs` (silent idempotency returns; swallowed `ReasoningConflictException`); provenance tagged "InferredDecisionSupersession"/"inferred-capture"; UI `ReasoningEventFeed.tsx:48-50`
- **Hidden Semantic Concept:** Capture policy defines Manual/Assisted/Inferred modes; inferred capture records events the system concluded from observed transitions, making per-transition "create or skip" decisions and swallowing conflict races. The UI renders provenance as a flat `{sourceKind} by {capturedBy}` with no Manual/Inferred distinction.
- **Why It Matters:** A user can't tell which reasoning events are human-stated intent vs. system inferences from a state transition — a core trust distinction — nor when a capture was deduplicated/skipped.
- **Current Visibility:** `sourceKind`/tags exist in text but aren't classified/badged; skip decisions are invisible.
- **Missing Visibility:** A Manual/Assisted/Inferred badge per event; the triggering transition for inferred events.
- **Recommended Exposure Mechanism:** Derive a capture-mode badge from the `sourceKind` prefix and show an "inferred from" line.

#### REA-5 — Authority/ownership boundaries are documented but surfaced only as static banners, not as the reason a capture was blocked
- **Severity:** Medium
- **Location:** docs `reasoning-authority-boundary.md`, `reasoning-ownership-boundaries.md`; runtime blocks in `ReasoningManualCaptureService.cs` (`ValidateManualProvenance`), `ReasoningRelationshipService.cs` (validation→conflict reclassification); generic `ReasoningEndpoints.cs` `HandleAsync`
- **Hidden Semantic Concept:** Hard boundaries ("reasoning may not approve/reject/supersede decisions, promote operational context, create execution directives") are enforced at runtime only as low-level validation exceptions surfaced via generic BadRequest/Conflict.
- **Why It Matters:** The boundary system defines what reasoning is *allowed to assert*. Panels show reassuring static labels ("Advisory only") but never connect a specific blocked action to the specific boundary rule it violated.
- **Current Visibility:** Static per-panel banners; raw exception text on rejection.
- **Missing Visibility:** When blocked, the boundary rule and the authoritative domain owning the contested state.
- **Recommended Exposure Mechanism:** Attach a boundary/rule identifier to `ReasoningValidationException` and render a structured "blocked by reasoning ownership boundary: X" notice.

#### REA-6 — Taxonomy "lifecycle risk" verdict hides its threshold and terminal-type trigger
- **Severity:** Low
- **Location:** `ReasoningMaterializationReviewService.cs:175-201,190`; UI `ReasoningMaterializationReviewPanel.tsx:60-68`
- **Hidden Semantic Concept:** `lifecycleRisk` is true when a family has ≥4 distinct event types AND ≥1 terminal-type (name ends Invalidated/Retired/…), flagging drift toward an unapproved lifecycle state machine. The UI prints the count but not the threshold or the terminal-type trigger.
- **Why It Matters:** The user sees "4 event types" but not that 4 is the trip line or why a 4-type family without a terminal type wouldn't be flagged.
- **Current Visibility:** Family name, summary, event-type count.
- **Missing Visibility:** The ≥4 threshold, the terminal-type condition, the event-type list (computed but not shown).
- **Recommended Exposure Mechanism:** Render the event-type list and a "risk basis" note when `lifecycleRisk` is true.

---

### Continuity / Operational Context

#### CON-1 — Decision taxonomy classification gatekeeps assimilation but its result is never shown
- **Severity:** High
- **Location:** `src/CommandCenter.Continuity/Services/DecisionAnalysisService.cs:76-99`; consumed `src/CommandCenter.Middle/Continuity/OperationalContextGenerationService.cs:146,203-207`
- **Hidden Semantic Concept:** Every decision bullet is classified Architectural/Strategic/Tactical/Historical; only Architectural and Strategic non-retired signals are assimilated into operational understanding — Tactical/Historical are silently excluded (keyword-driven and lossy).
- **Why It Matters:** The taxonomy is the gatekeeping decision for what understanding survives. A user can't tell why a decision they wrote was dropped (classified Tactical because it contained "build"/"test"/"commit") vs. kept.
- **Current Visibility:** None. No endpoint serializes `DecisionSignal`/`DecisionTaxonomy`; only resulting text lines appear in the proposal markdown with no label of why.
- **Missing Visibility:** Per-decision taxonomy label and the list of classified-out signals with the exclusion reason.
- **Recommended Exposure Mechanism:** Include `DecisionAnalysisResult.Signals` (taxonomy + statement + assimilated flag) on the proposal and render a "decisions considered / assimilated" breakdown.

#### CON-2 — Assimilation truncation (`Take(8)`/`Take(3)`) silently drops qualifying decisions/constraints/questions
- **Severity:** High
- **Location:** `src/CommandCenter.Middle/Continuity/OperationalContextGenerationService.cs:146,159,164`
- **Hidden Semantic Concept:** Even among decisions passing the taxonomy filter, only the first 8 are folded in, with 3 constraints / 3 open questions each; the remainder are discarded with no record.
- **Why It Matters:** A "what to keep/drop" decision affecting persisted understanding. If 12 architectural decisions qualify, 4 vanish; the user believes the proposal reflects all durable decisions.
- **Current Visibility:** None — no count of "qualified but truncated," no warning (unlike compression, which warns).
- **Missing Visibility:** A signal that N qualifying items exceeded the cap and were not assimilated.
- **Recommended Exposure Mechanism:** Emit a warning ("8 of 12 durable decisions assimilated; 4 omitted") and surface the omitted statements.

#### CON-3 — `DecisionSignal.Consequences` is extracted but never used or surfaced
- **Severity:** Medium
- **Location:** computed `src/CommandCenter.Continuity/Services/DecisionAnalysisService.cs:60,154-164`; model `DecisionSignal.cs`; unused in `OperationalContextGenerationService.cs:150-167`
- **Hidden Semantic Concept:** The analyzer detects "therefore/as a result/consequence:" clauses and records the downstream consequence of a decision, which is then never folded in nor exposed.
- **Why It Matters:** Consequences are core evidence behind a decision's importance; deriving and dropping them means the system "understood" an implication the user never sees.
- **Current Visibility:** None — computed and discarded.
- **Missing Visibility:** The extracted consequence text, tied to its decision.
- **Recommended Exposure Mechanism:** Render consequences alongside assimilated decisions (or surface in the decision breakdown).

#### CON-4 — Contradiction detection emits only the first conflict and loses the structured pair
- **Severity:** Medium
- **Location:** `src/CommandCenter.Continuity/Services/DecisionAnalysisService.cs:176-200`
- **Hidden Semantic Concept:** The system detects mutually-negating active durable decisions ("must X" vs "must not X") but `yield break`s after the first; remaining contradictions are never reported, and the one found is flattened into a free-text warning losing the left/right pair.
- **Why It Matters:** Conflicting context is a high-value uncertainty signal; reporting only one means a user resolving it has no idea others remain.
- **Current Visibility:** Partial — a single contradiction surfaces as a generic "Decision analysis warning:" string in compression warnings.
- **Missing Visibility:** All detected contradictions; structured identification of the conflicting statements as a distinct category.
- **Recommended Exposure Mechanism:** Remove the `yield break`, model contradictions as a typed list, and render a dedicated "Conflicts detected" section.

#### CON-5 — Question/risk "resolved vs lost" classification relies on hidden evidence-matching heuristics with no traceability
- **Severity:** Medium
- **Location:** `src/CommandCenter.Continuity/Services/ContinuityDiagnosticsService.cs:159-182`; compression equivalent `UnderstandingCompressionService.cs:215-236`
- **Hidden Semantic Concept:** When an open question/active risk disappears between revisions, the system decides resolved (matched a "resolved/retired" recent-change entry by substring / ≥3-token overlap) vs. lost (no evidence).
- **Why It Matters:** "Lost" is a serious knowledge-loss conclusion; "Resolved" is benign. The user sees only resolved/lost *counts*, never which item or what evidence matched; a false "resolved" (coincidental overlap) hides genuine loss.
- **Current Visibility:** Outcome-only — `resolvedCount`/`lostCount` per trend, no item-level list, no matched-evidence string.
- **Missing Visibility:** The specific items classified lost vs resolved, and the resolution-evidence text for resolved items.
- **Recommended Exposure Mechanism:** Extend `ContinuityTrend` with lost-item and resolved-item-with-evidence lists; render under the lifecycle section.

#### CON-6 — Compression "modified" detection is disabled — `ModifiedItemCount` is hardcoded to 0
- **Severity:** Medium
- **Location:** `src/CommandCenter.Continuity/Services/UnderstandingCompressionService.cs:80`
- **Hidden Semantic Concept:** The compression summary presents Preserved/Added/Modified/Removed accounting, but "Modified" is never computed — a reworded item counts as one Removed + one Added.
- **Why It Matters:** The compression summary is the system's claim about *what changed* in understanding; a permanent `Modified: 0` while reporting Removed/Added misrepresents in-place edits as deletions+insertions, hiding that an item was reworded rather than dropped.
- **Current Visibility:** Misleading — the panel renders `Modified: {modifiedItemCount}`, structurally always 0.
- **Missing Visibility:** Genuine modified-item detection (or removal of the always-zero field).
- **Recommended Exposure Mechanism:** Implement modification detection (fuzzy/decision-key matching) or drop the field so the accounting isn't silently incomplete.

#### CON-7 — Semantic diff detects "changed" only for decision rationale; other in-place edits appear as remove+add
- **Severity:** Medium
- **Location:** `src/CommandCenter.Continuity/Services/UnderstandingDiffService.cs:99-102,105-134,184-194`
- **Hidden Semantic Concept:** The diff recognizes a *changed* (vs added/removed) item only for decision rationale with an exact prefix; every other section reports a reworded item as Removed + Added.
- **Why It Matters:** "What changed between versions" is the diff's explicit purpose; an edited architecture/constraint statement appears as one removal + one addition, obscuring that it's the same concept revised.
- **Current Visibility:** The change list renders `type: description`, but underlying types carry `*Changed` only for rationale; all other edits are unlinked add/remove pairs.
- **Missing Visibility:** Change (vs add/remove) detection for non-rationale sections, or an explicit note of the limitation.
- **Recommended Exposure Mechanism:** Broaden change-detection (identity matching by stable id) or label the limitation ("Edits outside Decision Rationale appear as remove + add").

#### CON-8 — Compression "transient noise" keep/drop rule (keyword + half-limit position) is invisible
- **Severity:** Low
- **Location:** `src/CommandCenter.Continuity/Services/UnderstandingCompressionService.cs:119-123,144-150,115,121,136`
- **Hidden Semantic Concept:** Whether a recent-change item is droppable "transient execution noise" depends on keyword matching AND a positional rule (only compressed after half the limit is filled); the same item could be kept or dropped depending on ordering. The code distinguishes three reason categories (duplicate / transient-keyword / aged-out) but flattens them to prose.
- **Why It Matters:** A keep/drop decision on understanding; the user sees *what* was compressed but not the *rule*, so identical-looking items treated differently appears arbitrary.
- **Current Visibility:** Outcome-only — `noiseRemovedIndicators` lists removed text but not the qualifying criterion or threshold behavior.
- **Missing Visibility:** The reason category per removed indicator and the half-limit threshold.
- **Recommended Exposure Mechanism:** Tag each noise-removed indicator with its category (duplicate / transient-execution / aged-out).

---

### Execution Subsystem

#### EXE-1 — Assembled prompt composition is never exposed to the user
- **Severity:** High
- **Location:** `src/CommandCenter.Execution/Services/ExecutionPromptBuilder.cs:45-60`; persisted `ExecutionSessionService.cs:227`; never rendered (verified: zero `promptMetadata`/`includedArtifactPaths`/`totalContextBytes` references in `src/CommandCenter.UI/src`)
- **Hidden Semantic Concept:** What actually went into the prompt sent to the provider — the exact `IncludedArtifactPaths`, total bytes/chars assembled, and `DirtyRepository` flag — captured at launch on the running session.
- **Why It Matters:** This is the single most outcome-determining input to the run. The Context Diagnostics panel shows the *pre-launch preview* for the selected milestone, which can differ from what the live session was launched with; the user can't confirm which decisions/handoff/operational-context were fed to the agent that produced the result.
- **Current Visibility:** `ExecutionPromptMetadata` is stored and serialized by `GET /api/execution-sessions/{id}` but no UI reads it; the session panel shows PID/executable/timestamps, not prompt contents.
- **Missing Visibility:** A per-session "prompt manifest" — included artifact paths (with roles), assembled totals, dirty-repo flag, injected governed-decision counts.
- **Recommended Exposure Mechanism:** A "Prompt Composition" section on `ExecutionSessionPanel` bound to `session.promptMetadata`.

#### EXE-2 — Recovery decision (reattach vs. orphan-fail) and its trigger are only generic event text
- **Severity:** High
- **Location:** `src/CommandCenter.Execution/Services/ExecutionSessionService.cs:26-86,73,78`; `ExecutionSessionRecoveryHostedService.cs:9-12` (runs silently at startup)
- **Hidden Semantic Concept:** On backend restart, per executing session the system chooses reattach the live process (`TryReattachAsync` succeeds → stays Executing) vs. mark Failed with `OrphanedProviderFailureReason`. The trigger (restart) and whether reattach was even attempted (gated on `SupportsReattach`) aren't distinguished.
- **Why It Matters:** A user returning to a Failed session sees only a generic event line, with no signal this was automatic startup recovery vs. an in-run failure, and no indication reattach was unsupported vs. attempted-and-failed.
- **Current Visibility:** `Recovery` events render as raw enum + message; no distinct recovered/degraded state; `status.ts` has no Recovery presentation.
- **Missing Visibility:** Whether recovery ran, which path and why, and a distinct visual state for recovered-vs-orphaned.
- **Recommended Exposure Mechanism:** A recovery banner sourced from the `Recovery` event, distinguishing reattach success from orphan-fail and noting `SupportsReattach`.

#### EXE-3 — Push failure leaves a hidden retry state with no surfaced attempt history
- **Severity:** High
- **Location:** `src/CommandCenter.Execution/Services/ExecutionSessionService.cs:446-452,755-796`; endpoint re-throws `GitEndpoints.cs:79-97`; type `types/execution.ts:38`; mapped `App.tsx:403`
- **Hidden Semantic Concept:** A failed push records `PushAttemptedAt` + `FailureReason` while keeping `RepositoryState = AwaitingPush` (a retryable degraded state). The endpoint re-throws, so the updated summary (with reason and attempt time) never returns — only a generic 409.
- **Why It Matters:** The user is left in `AwaitingPush` after a failed push but the reason (rejected remote, auth) and the fact an attempt was made aren't communicated; the Push button simply re-enables — hidden uncertainty/partial progress.
- **Current Visibility:** `pushAttemptedAt` exists in the type and is mapped, but no component renders it; the push review shows commit/branch/ahead, never `pushAttemptedAt` or a prior failure.
- **Missing Visibility:** Last push attempt timestamp + failure reason inline, marking the state "Push failed — retry."
- **Recommended Exposure Mechanism:** Return the failed summary (don't only throw) and render `pushAttemptedAt` + `failureReason` as a retry warning.

#### EXE-4 — Git-action eligibility is enforced as disabled buttons with no reason
- **Severity:** Medium
- **Location:** `src/CommandCenter.UI/src/App.tsx:437-446`; consumed `GitWorkflowPanel.tsx:127,168`; server preconditions `ExecutionSessionService.cs:320-323,350-353,429-432`
- **Hidden Semantic Concept:** Composite eligibility for Commit (preparation current + ≥1 selected path + non-empty message) and Push (session present + `AwaitingPush` + `commitSha`) is computed but collapses to a single disabled/enabled boolean.
- **Why It Matters:** When disabled, the user can't tell which precondition is unmet (no message? no paths? stale preparation?). The decision inputs exist but "why blocked" is dropped.
- **Current Visibility:** Buttons disable; generic "preparation not loaded"/loading states, not the specific failing precondition.
- **Missing Visibility:** Per-precondition checklist/hint explaining why commit/push is blocked.
- **Recommended Exposure Mechanism:** Render unmet conditions next to the disabled action ("Enter a commit message", "Select at least one path", "Preparation is stale — refresh").

#### EXE-5 — Handoff-completion outcome branches collapse into one generic failure reason
- **Severity:** Medium
- **Location:** `src/CommandCenter.Execution/Services/HandoffService.cs:45-92,14-18,118-122`
- **Hidden Semantic Concept:** On provider completion the service makes distinct decisions: missing current handoff → Failed; previous handoff differs → archive it (archive throws → Failed); success → AwaitingAcceptance + HandoffValidated. The archive-vs-no-archive decision and the archived sequence number are invisible.
- **Why It Matters:** A session can land in Failed because handoff post-processing (not the agent run) failed; the user sees only a generic failure string with no evidence of whether a handoff was produced, a prior existed, or archival succeeded.
- **Current Visibility:** Only `failureReason` text and a `HandoffValidated` event on success; no archival evidence.
- **Missing Visibility:** Handoff post-processing summary (produced yes/no, archived to which `handoff.NNNN.md`, or the archival error).
- **Recommended Exposure Mechanism:** Emit/render dedicated events (handoff-missing, handoff-archived-to-N, archive-failed) in the handoff-review header.

#### EXE-6 — Decision-projection conflicts that block launch are demoted to anonymous validation strings
- **Severity:** Medium
- **Location:** `src/CommandCenter.Execution/Services/ExecutionContextService.cs:80-84`; launch block `ExecutionSessionService.cs:153-163`; UI `ExecutionContextValidationList.tsx:14-16`
- **Hidden Semantic Concept:** A governed-decision conflict (`ExecutionDecisionConflict` with `DecisionId` + `ConflictingExcerpt`) is categorically different from a missing artifact, but is flattened into the same `ValidationErrors` string list driving `LaunchBlocked`.
- **Why It Matters:** The user can't distinguish "you violate governed decision X" from "file Y is missing"; the structured conflict (which decision, which excerpt) is the evidence behind the block but arrives only as a sentence.
- **Current Visibility:** Plain `<li>` strings; structured conflicts render only into the (non-user-facing) prompt.
- **Missing Visibility:** Conflicts as a distinct category with decision-ID linkage and the conflicting excerpt.
- **Recommended Exposure Mechanism:** Split diagnostics to carry typed conflicts; render a "Governed Decision Conflicts" group distinct from validation errors.

#### EXE-7 — "Pre-existing changes" in the commit scope is shown as a one-word flag without consequence
- **Severity:** Low
- **Location:** `src/CommandCenter.Execution/Services/GitService.cs:55-73,322-329`; UI `GitWorkflowEvidence.tsx:22`, `GitWorkflowPanel.tsx:147`
- **Hidden Semantic Concept:** The system classifies each changed path `ExecutionGenerated` vs `PreExisting` (diffed against the session-start snapshot) — which changes the agent made vs. pre-existing dirt that might be committed unintentionally.
- **Why It Matters:** The distinction is meaningful (you may not want to commit unrelated pre-existing changes), but it's reduced to "Pre-existing: Present/None" with per-item origin in small text and no warning when pre-existing files are selected.
- **Current Visibility:** Per-item `origin` label + an aggregate flag.
- **Missing Visibility:** A count/warning when pre-existing paths are in the selected scope; ability to bulk-deselect them.
- **Recommended Exposure Mechanism:** Surface "N pre-existing paths selected" warning + a "select only execution-generated" action.

#### EXE-8 — Monitoring/health state is explicitly stubbed as "Not projected"
- **Severity:** Low
- **Location:** UI `src/CommandCenter.UI/src/features/execution/ExecutionTab.tsx:183`; source `ExecutionMonitoringService.cs:176-234`
- **Hidden Semantic Concept:** The monitoring service derives health-affecting transitions (non-zero provider exit → Failed, successful exit → Completed, event-retention trimming), but the UI's monitoring summary is a hardcoded placeholder.
- **Why It Matters:** The "Execution Diagnostics" panel implies a monitoring health readout that doesn't exist; derived failure/exit conclusions are only inferable from the raw event stream.
- **Current Visibility:** Raw events + session state/failureReason; the dedicated "Monitoring warnings" line is a literal stub.
- **Missing Visibility:** Real monitored signals (provider exit code, retention-trimmed indicator, last-activity staleness as a health flag).
- **Recommended Exposure Mechanism:** Replace the placeholder with a derived health summary (last exit code, stale-activity flag from `lastActivityAt`, retention-trimmed indicator).

---

## Appendix — Verified Non-Findings (already adequately exposed)

To document scope and avoid false positives, the audit confirmed these semantic concepts *are* surfaced and are therefore not findings: decision revision retired options/assumptions, changed-fields, lineage from→state transitions, influence-trace statements/diagnostics, source-fingerprint-matches-current, package regeneration comparison flags (decisions UI); operational-context proposal lifecycle status + review state, stale/promotion-failure reasons, compression tier counts + warnings + retention warnings, semantic-change list, repeated-signal indicators, evolution trend table (continuity UI); git path origin labels, commit/branch/ahead evidence, validation/missing-optional artifact lists (execution UI). Backend endpoints generally serialize full domain objects, so the gaps above are genuine compute-side omissions or UI render-layer drops, not serialization losses.
