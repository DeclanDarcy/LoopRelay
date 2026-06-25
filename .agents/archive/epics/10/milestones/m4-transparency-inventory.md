# Milestone 4 Transparency Inventory

## Scope

This inventory covers the first Milestone 4 step: identify which decision transparency facts already exist as decision-owned data, which are serialized to the UI type surface, which are rendered today, and which facts are computed only implicitly.

## Proposal Transparency

- `DecisionProposal` already persists and serializes:
  - `GenerationDiagnostics`
  - `AnalyzedOptions`
  - `TradeoffComparisons`
  - `TradeoffAnalysisDiagnostics`
  - `Recommendation`
- `DecisionRecommendation` already persists and serializes:
  - `Mode`
  - `Summary`
  - `Rationale`
  - `Evidence`
  - `RecommendationEvidence`
  - `OptionEvaluations`
  - `SupportingFactors`
  - `Concerns`
  - `Assumptions`
  - `AlternativeExplanations`
- `OptionEvaluation` already carries option score, rank, score explanation, strengths, weaknesses, risks, constraints, summary, and evidence.
- `DecisionGenerationDiagnostics` already carries option validation results and counts for generated, accepted, rejected, deduplicated, and fallback options.

Current gap: generated diagnostics expose counts and validation results, but do not preserve concrete rejected option payloads or deduplicated option payloads. The UI can show validation failures and counts now, but cannot render rejected or deduplicated alternatives as first-class options until the generator-owned model persists those items.

## TypeScript Surface

- `src/CommandCenter.UI/src/types/decisions.ts` already includes proposal fields for analyzed options, tradeoff comparisons, tradeoff analysis diagnostics, generation diagnostics, recommendation evidence, and option evaluations.
- `DecisionProposalViewer` currently renders only basic proposal evidence, options, tradeoffs, recommendation rationale, basic generation counts, and diagnostics.

Current gap: most existing transparency fields are available to React but not rendered.

## Quality Transparency

- `DecisionQualityAssessmentService` computes score as `50 + Sum(ScoreContribution(signal))`, clamped to 0..100.
- `DecisionQualitySignalService` emits signal category, direction, severity, summary, detail, and sources.
- Rating thresholds are currently:
  - critical negative signal => `Poor`
  - score >= 85 => `Excellent`
  - score >= 65 => `Good`
  - score >= 40 => `Mixed`
  - otherwise `Poor`

Current gap: signal contribution, threshold crossed, and critical override reason are computed in private service logic and not projected as data. The UI should not recreate those rules.

## Burden Transparency

- `HumanAuthoringBurdenService` emits burden signals from proposal revisions, refinement artifacts, generated-proposal bypass, or review-only default.
- `DecisionQualityReportService` derives an effective burden by selecting the highest weighted burden.
- The weighting order is:
  - `GenerationBypassed`
  - `FullRewrite`
  - `MajorRefinement`
  - `MinorEdit`
  - `ReviewOnly`
  - `Unknown`

Current gap: the winning burden signal and selection rule are private service logic and not projected as data. Unknown versus inferred/default status is also not explicit.

## Governance Transparency

- `DecisionGovernanceReport` and `DecisionGovernanceFinding` already expose health, summary counts, finding category, severity, blocking status, title, detail, sources, and related decision/candidate/proposal ids.
- Governance findings already identify stale authority, authority boundary, lifecycle coverage, fingerprint integrity, projection integrity, and conflicting authority cases through finding title/detail.

Current gap: the model is finding-centric rather than entity-centric. It is authoritative enough for current UI explanation, but entity-specific governance views will need grouping by related decision, candidate, and proposal without deriving new governance rules.

## Execution Influence Transparency

- `DecisionProjectionService` already builds `DecisionProjectionDiagnostics` with included, excluded, superseded, projected statements, conflicts, diagnostics, and per-decision reasons.
- `ExecutionDecisionProjection` returned to clients includes constraints, directives, priorities, architecture rules, conflicts, diagnostics, context, and projection fingerprint.
- `DecisionInfluenceTrace` records projected statements included in a prompt and projection diagnostics.

Current gap: `DecisionProjectionDiagnostics` is persisted as an artifact but is not surfaced in the TypeScript API/type model. Influence traces show included prompt statements and diagnostics, but not the full included/excluded/superseded decision diagnostic set unless clients read the persisted projection diagnostic artifact through another path.

## Recommended Next Backend Slice

Add narrow decision-owned projection fields for quality and burden explanation before expanding UI rendering:

- expose quality signal score contribution
- expose quality rating threshold and critical override reason
- expose burden selection rule, effective/winning burden, winning signal id, and unknown/default inference status
- surface projection diagnostics through the decision API/type surface so execution influence can explain included, excluded, superseded, blocked, and conflicted decisions without artifact spelunking

Do not add UI-side score, rating, burden, or influence calculations.
