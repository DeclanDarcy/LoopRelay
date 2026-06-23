# Automated Decision Generation Implementation Plan

## Objective

Implement automated decision generation as a first-class Command Center capability that increases decision throughput while preserving human governance.

The completed workflow is:

```text
Repository State
        |
Decision Context
        |
Decision Candidate Discovery
        |
Option Generation
        |
Tradeoff Analysis
        |
Recommendation Generation
        |
Decision Package
        |
Human Review and Refinement
        |
Human Resolution
        |
Decision Quality Evaluation
        |
Resolved Decision Projection
        |
Execution Context
        |
Execution
```

The system generates candidates, options, tradeoffs, recommendations, packages, diagnostics, quality assessments, and execution-facing decision guidance. Humans review, refine, accept, reject, defer, supersede, and archive. Execution consumes only accepted resolved decisions that pass governance checks.

The implementation is complete when a repository can progress from current project evidence to generated decision packages without requiring a human to author the decision content, and every resolved decision that influences execution is traceable to a human resolution.

## Delivery Strategy

Optimize first for the smallest end-to-end validation that proves the workflow replacement hypothesis.

Tier 0 is the core validation slice:

```text
Candidate
        |
Generated Options
        |
Generated Tradeoffs
        |
Generated Recommendation
        |
Human Resolution
        |
Execution Consumption
```

Tier 0 proves whether a real repository can move from evidence to execution guidance with no human-authored decision content. It uses the existing proposal, resolution, and execution-projection infrastructure wherever that infrastructure is already adequate.

Tier 0 implementation order:

1. Preserve the current lifecycle and authority tests.
2. Add typed generation context only to the depth required for generation.
3. Upgrade candidate discovery to typed candidates and better suppression.
4. Replace shallow proposal generation with real option generation.
5. Add structured tradeoff analysis.
6. Add derived recommendation generation.
7. Resolve through the existing human resolution flow.
8. Project the accepted resolved decision through the existing execution consumption path.
9. Record the human authoring burden for the generated decision.

Tier 1 hardens the capability after Tier 0 has run on real repository work:

- immutable package-version semantics
- directive-driven refinement and scoped regeneration
- quality dashboards and trend reports
- enriched execution influence traces
- final generation certification

Tier 1 work is valuable, but it must not delay the first real validation of automated decision production. The first decision-generation slice is successful only if a human reviews and resolves generated content instead of writing the decision manually.

## Current Codebase Baseline

Command Center currently has:

- .NET backend sidecar in `src/CommandCenter.Backend`.
- React/TypeScript UI in `src/CommandCenter.UI`.
- Rust/Tauri shell in `src/CommandCenter.Shell`.
- Shared repository, artifact, configuration, planning, and projection infrastructure in `src/CommandCenter.Core`.
- Operational-context parsing, generation, review, lifecycle, diagnostics, and reporting in `src/CommandCenter.Continuity` and `src/CommandCenter.Middle`.
- Execution context, provider launch, event monitoring, handoff, commit, push, and prompt-building services in `src/CommandCenter.Execution`.
- Structured decision lifecycle in `src/CommandCenter.Decisions`.
- Reasoning trajectory preservation in `src/CommandCenter.Reasoning`.

Decision lifecycle support already includes:

- `DecisionContextService` building deterministic repository-backed context items and snapshots.
- `DecisionDiscoveryService` discovering evidence-backed candidates from decision context signals.
- Candidate states: `Discovered`, `Promoted`, `Dismissed`, `Expired`, `Duplicate`.
- `DecisionGenerationService` creating proposals from promoted candidates.
- Proposal states: `Draft`, `Generated`, `Viewed`, `NeedsRefinement`, `ReadyForResolution`, `Refined`, `Resolved`, `Expired`, `Discarded`.
- Review status, review notes, proposal revisions, proposal lineage, option comparison, evidence inspection, and source attribution.
- `DecisionResolutionService` creating authoritative decision records from human resolution commands.
- Resolution snapshots that preserve proposal content at the point of decision authority.
- Supersession, archival, governance, lifecycle certification, operational-context assimilation recommendations, and execution projection.
- `ExecutionContextService` including `ExecutionDecisionProjection` when a projection service is available.
- `ExecutionPromptBuilder` rendering governed decision constraints and directives into execution prompts.
- Decision UI panels for candidates, proposal review, refinement, resolution, revisions, option comparison, evidence, governance, and certification.
- Backend and UI characterization tests across decision context, discovery, generation, review, refinement, resolution, governance, projection, certification, and execution integration.

Primary gaps to close:

- Proposal generation is still shallow and heuristic. It builds one default option, optionally a second conflict option, and recommends `options[0]`. That is not sufficient automated decision generation.
- Candidate discovery uses generic signal strings and classification, but lacks a typed candidate-kind model with explicit architectural fork, strategic direction, tactical choice, operational blocker, constraint conflict, contradiction, supersession, and workflow-continuation semantics.
- Decision context is item-based and deterministic, but not yet exposed as a typed decision-generation context with project, milestone, operational, handoff, decision-history, repository, constraint, risk, and question sections.
- `DecisionOption` lacks option type, assumptions, dependencies, validation diagnostics, and relationships.
- `DecisionTradeoff` only stores benefit and cost strings. It needs structured benefits, costs, risks, dependencies, consequences, impact, severity, and cross-option comparison.
- Recommendation generation lacks a separate evaluation service, evidence scoring, explicit assumptions, concern generation, no-recommendation mode, and explainability for why alternatives lost.
- The current proposal acts as the package, but immutable package/version semantics are not explicit enough for governance-ready generated decision packages.
- Refinement supports edited proposal content and revision history, but it does not yet analyze structured human directives into scoped regeneration.
- Decision quality evaluation is absent. There are no quality signals, quality assessments, reports, or trend views derived from governance outcomes.
- Human authoring burden is not yet measured as a first-class product metric across generated proposals, refinements, resolutions, and certification.
- Execution projection exists, but it should be enriched into an execution-facing decision context with directives, constraints, priorities, architecture rules, influence traceability, persisted diagnostics, and adherence observations.
- Certification currently validates lifecycle integrity. It must also certify generation capability, governance compatibility, throughput improvement, decision quality, execution influence, and workflow replacement.

## Architecture Rules

1. Repository files remain authoritative.
   Structured decision artifacts under `.agents/decisions` are the source of truth. Markdown remains a deterministic human-readable projection.

2. Human resolution remains authoritative.
   The system can generate, recommend, compare, diagnose, and certify. It must not accept, reject, defer, supersede, archive, or approve decisions without explicit human action.

3. Generation and resolution stay separate.
   Generated candidates, options, tradeoffs, recommendations, packages, refinements, assessments, and diagnostics are not decision authority.

4. Execution consumes only accepted resolved decisions.
   Draft proposals, unresolved recommendations, rejected decisions, deferred decisions, archived decisions, superseded decisions, and decisions with blocking governance findings must not direct execution.

5. Operational context remains separate.
   A resolved decision may produce a reviewable assimilation recommendation. Decision services must not directly mutate `.agents/operational_context.md` or bypass operational-context review and promotion.

6. Reasoning remains explanatory.
   Decision workflows may emit reasoning events after authoritative transitions, but reasoning must not own decision authority, operational-context authority, governance enforcement, or execution directives.

7. UI remains presentation state.
   React can select, filter, draft input, and display state. Backend services own lifecycle rules and mutation authority.

8. Tauri remains a bridge.
   Rust commands call backend endpoints and translate errors. Decision logic stays in .NET services.

9. Generation must produce alternatives, not a single answer.
   A proposal should target three viable options, allow two as the minimum, allow five as the maximum, and allow one option only when the package explicitly explains why only one technically valid path exists.

10. Recommendations must be derived.
    Recommendation generation must use context, candidate evidence, options, tradeoffs, constraints, risks, prior decisions, and current goals. It must not depend on hardcoded preference, option ordering, or static templates.

11. Quality evaluation observes; it does not govern.
    Quality services may assess acceptance, modification, rejection, supersession, recommendation stability, and human effort. They must not block, mutate, or override decisions.

12. Certification measures the mission.
    Passing certification requires evidence that the system generates useful decision packages, humans primarily review/refine/resolve, and execution is guided by resolved decisions.

13. Earliest useful validation comes before hardening.
    The first delivery target is an end-to-end generated decision that reaches execution consumption through human resolution. Package versioning, quality trends, directive-driven refinement, and final certification should harden the workflow after the core generation loop has been tested on real work.

14. Human authoring burden is a primary metric.
    Every generated decision should make clear whether the human reviewed only, made minor edits, requested major regeneration, rewrote the decision, or bypassed generation. The system is not succeeding if humans remain the primary authors.

## Target Repository Layout

Keep the existing structured lifecycle layout and extend it only where needed:

```text
.agents/
  decisions/
    decisions.md
    records/
      DEC-0001/
        decision.json
        decision.md
        history.json
    candidates/
      CAND-0001/
        candidate.json
        candidate.md
        history.json
    proposals/
      PROP-0001/
        proposal.json
        proposal.md
        review.json
        notes.json
        history.json
        diagnostics.json
        versions/
          PKG-0001.json
          PKG-0001.md
        revisions/
          REV-0001.json
          REV-0001.md
    contexts/
      context.<timestamp>.json
    assimilation/
      DEC-0001/
        recommendation.json
        recommendation.md
    governance/
      governance.<timestamp>.json
      governance.<timestamp>.md
    quality/
      assessments/
        assessment.<timestamp>.json
        assessment.<timestamp>.md
      reports/
        quality.<timestamp>.json
        quality.<timestamp>.md
      trends/
        trend.<timestamp>.json
        trend.<timestamp>.md
    projections/
      execution.<timestamp>.json
      execution.<timestamp>.md
    influence/
      execution-<session-id>.json
      execution-<session-id>.md
    certification/
      certification.<timestamp>.json
      certification.<timestamp>.md
```

Rules:

- JSON records are authoritative.
- Markdown projections are deterministic and rebuildable.
- Package versions are immutable snapshots.
- Current proposal state may point to the latest package version, but prior package versions must remain reviewable.
- Reports are persisted only when a user runs/report-generates them.
- All records carry repository ownership, schema version, fingerprints, source references, and diagnostics where applicable.
- IDs remain repository-scoped, human-readable, and sequence allocated by scanning existing artifacts.

## Target Domain Additions

Add or extend decision models under `src/CommandCenter.Decisions/Models` and `Primitives`:

```text
DecisionCandidateType
DecisionOptionType
DecisionOptionRelationship
DecisionOptionRelationshipType
DecisionGenerationContext
DecisionGenerationDiagnostics
DecisionOptionValidationResult
AnalyzedDecisionOption
DecisionBenefit
DecisionBenefitCategory
DecisionCost
DecisionCostCategory
DecisionRisk
DecisionRiskCategory
DecisionDependency
DecisionDependencyType
DecisionConsequence
DecisionConsequenceDirection
TradeoffImpact
TradeoffSeverity
OptionEvaluation
RecommendationEvidence
RecommendationEvidenceType
RecommendationMode
DecisionPackage
DecisionPackageMetadata
DecisionPackageValidationResult
DecisionPackageVersion
DecisionPackageComparison
RefinementDirective
RefinementDirectiveType
RefinementPlan
DecisionQualityAssessment
DecisionQualityRating
DecisionQualitySignal
QualitySignalDirection
QualitySignalSeverity
DecisionQualityReport
DecisionQualityTrend
ExecutionDecisionContext
ExecutionDecisionPriority
ExecutionArchitectureRule
DecisionInfluenceTrace
DecisionProjectionDiagnostics
DecisionGenerationCertificationResult
DecisionGenerationCertificationReport
HumanAuthoringBurden
HumanAuthoringBurdenSignal
HumanAuthoringBurdenReport
DecisionThroughputReport
```

Recommended `HumanAuthoringBurden` values:

```text
ReviewOnly
MinorEdit
MajorRefinement
FullRewrite
GenerationBypassed
Unknown
```

The metric should be attached to proposal review, refinement, resolution, quality assessment, and generation certification records where enough evidence exists.

Schema strategy:

- Prefer additive fields and new records where possible.
- When positional records must change, update JSON persistence tests, UI types, Tauri DTOs, and markdown projections in the same slice.
- Preserve compatibility for existing structured artifacts by allowing missing new optional fields to default to empty collections or `Unknown` diagnostics.
- Fail visibly on unsupported schema versions.

## Target Service Additions

Add service contracts under `src/CommandCenter.Decisions/Abstractions`:

```text
IDecisionCandidateAnalyzer
IDecisionCandidateDiscoveryPipeline
IDecisionContextProjectionService
IOptionGenerationService
IOptionValidationService
ITradeoffAnalysisService
IOptionComparisonService
IRecommendationService
IDecisionPackageService
IRefinementAnalysisService
IDecisionQualitySignalService
IDecisionQualityAssessmentService
IDecisionQualityReportService
IDecisionInfluenceService
IDecisionGenerationCertificationService
IHumanAuthoringBurdenService
```

Refactor `DecisionGenerationService` into an orchestrator:

```text
Promoted Candidate
        |
Decision Context Projection
        |
Option Generation
        |
Option Validation and Deduplication
        |
Tradeoff Analysis
        |
Option Comparison
        |
Recommendation Generation
        |
Package Assembly
        |
Persistence and Projection
```

Keep existing service names where callers already depend on them. Introduce narrower services behind current facade methods first, then expose new endpoints only after tests prove the new pipeline.

## Backend API Additions

Keep current decision endpoints and add these repository-scoped endpoints:

```text
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/generation-diagnostics
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/packages
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/packages/{packageId}
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/packages/{leftPackageId}/compare/{rightPackageId}
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/refinements/analyze
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/refinements/regenerate
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/quality/assess
GET  /api/repositories/{repositoryId}/decisions/quality/assessments
GET  /api/repositories/{repositoryId}/decisions/quality/reports/current
POST /api/repositories/{repositoryId}/decisions/quality/reports
GET  /api/repositories/{repositoryId}/decisions/quality/trends/current
POST /api/repositories/{repositoryId}/decisions/projections/execution
GET  /api/repositories/{repositoryId}/decisions/projections/execution/latest
GET  /api/repositories/{repositoryId}/decisions/influence/executions/{executionId}
GET  /api/repositories/{repositoryId}/decisions/influence/decisions/{decisionId}
GET  /api/repositories/{repositoryId}/decisions/generation-certification/current
POST /api/repositories/{repositoryId}/decisions/generation-certification
GET  /api/repositories/{repositoryId}/decisions/generation-certification/reports
```

Endpoint behavior:

- `400 BadRequest` for invalid payloads, unsafe paths, invalid IDs, missing required fields, and validation failures that are not lifecycle conflicts.
- `404 NotFound` for missing repository, candidate, proposal, package, decision, report, or influence trace.
- `409 Conflict` for invalid lifecycle transitions, stale fingerprints, duplicate active proposals, blocked projection, conflicting directives, unsupported schema versions, and stale refinement bases.
- `200 OK` for successful reads and mutations that return updated projections.
- Error bodies follow the existing `{ error = "..." }` pattern.

## Tauri Bridge Additions

Add Rust commands in `src/CommandCenter.Shell/src/main.rs` after backend endpoints exist:

```text
get_decision_generation_diagnostics
list_decision_packages
get_decision_package
compare_decision_packages
analyze_decision_refinement
regenerate_decision_refinement
assess_decision_quality
list_decision_quality_assessments
get_decision_quality_report
generate_decision_quality_report
get_decision_quality_trend
generate_execution_decision_projection
get_latest_execution_decision_projection
get_execution_decision_influence
get_decision_influence
get_decision_generation_certification
run_decision_generation_certification
list_decision_generation_certification_reports
```

Each command should mirror existing command style: call backend HTTP, deserialize typed responses, and use the shared response-error path for non-success responses.

## UI Additions

Extend `src/CommandCenter.UI/src/types/decisions.ts`, `api/decisions.ts`, and decision hooks for:

- typed candidate kind
- option type and relationships
- structured benefits, costs, risks, dependencies, consequences
- option evaluations and comparisons
- recommendation evidence, assumptions, concerns, and no-recommendation mode
- package versions and package comparison
- refinement directives and refinement plans
- quality assessments, reports, and trends
- execution decision context, priorities, architecture rules, and influence traces
- generation certification result/report
- human authoring burden values, burden signals, and throughput reports

Extend the Decisions workspace with focused panels:

```text
DecisionContextInspectionPanel
DecisionGenerationDiagnosticsPanel
DecisionPackageHistoryPanel
DecisionPackageComparisonPanel
DecisionStructuredTradeoffPanel
DecisionRecommendationEvidencePanel
DecisionRefinementDirectivePanel
DecisionQualityDashboard
DecisionQualityTrendPanel
DecisionExecutionInfluencePanel
DecisionGenerationCertificationPanel
```

UI rules:

- Keep recommendations visually distinct from resolutions.
- Keep generated packages visually distinct from current authoritative decisions.
- Keep refinement feedback/directives distinct from direct artifact editing.
- Show whether each generated decision required review only, minor edits, major refinement, full rewrite, or generation bypass.
- Show source evidence beside the option, tradeoff, recommendation, assumption, quality signal, or execution projection it supports.
- Disable controls based on backend state and expose backend conflict messages.
- Do not infer decision authority from client state.
- Do not add visible instructional text that explains the application rather than supporting the workflow.

## Milestone 0: Baseline Certification and Contract Hardening

Goal: establish the implementation baseline and lock the authority boundaries before replacing the shallow generation path.

Work:

- Run the current backend and UI test suites to capture a starting point.
- Document the current decision lifecycle contract in `docs/` if any public behavior is not already documented by tests.
- Add characterization tests proving existing behavior that must not regress:
  - repository-backed structured artifacts reload after service restart
  - candidates and proposals do not mutate operational context
  - only human resolution creates authoritative decisions
  - unresolved proposals do not project to execution
  - accepted resolved decisions with blocking governance findings do not project
  - proposal markdown is projection only
- Add schema migration tests for reading existing candidate, proposal, decision, governance, certification, and projection artifacts after new optional fields are introduced.
- Add explicit tests that `DecisionGenerationService` no longer recommends by option order once the new pipeline is introduced.
- Add the first burden-classification tests:
  - generated recommendation accepted unchanged is `ReviewOnly`
  - generated recommendation accepted after small structured refinement is `MinorEdit`
  - generated recommendation replaced through extensive refinement is `MajorRefinement`
  - human-authored replacement content is `FullRewrite`
  - manual decision creation outside generation is `GenerationBypassed`

Exit criteria:

- Current behavior is covered by regression tests.
- All authority boundaries are protected by tests.
- New schema additions have a compatibility strategy.
- The repository can record human authoring burden without changing lifecycle authority.
- The implementation can proceed without rebuilding already working lifecycle infrastructure.

## Milestone 1: Decision Context Foundation Upgrade

Goal: turn the deterministic item-based context into a typed generation context while preserving the existing `DecisionContext` snapshot behavior.

Work:

- Add `DecisionGenerationContext` with sections:
  - `Project`
  - `Milestone`
  - `OperationalContext`
  - `Handoff`
  - `DecisionHistory`
  - `Repository`
  - `Constraints`
  - `Risks`
  - `Questions`
- Add `IDecisionContextProjectionService` that maps the current `DecisionContext.Items` model into the typed generation context.
- Preserve the existing `DecisionContextService.BuildContextAsync` and snapshot APIs.
- Add source diagnostics for each typed context section:
  - sources used
  - sources missing
  - warning count
  - context size
  - source fingerprints
- Add repository-state context using existing `IGitService` or repository snapshot capabilities where available:
  - branch
  - dirty state
  - modified paths
  - recent commit summaries when available
- Extract constraints, risks, and questions from operational context, decisions, handoffs, and milestones using evidence-backed source references.
- Validate required inputs:
  - plan
  - active milestone
  - repository metadata
- Treat operational context, handoff, and decision history as optional but diagnostic.

Tests:

- Context projection produces all typed sections from existing repository artifacts.
- Missing required inputs fail validation.
- Missing optional inputs produce warnings, not failure.
- Constraints, risks, and questions carry source references.
- Context fingerprint changes when a contributing source changes.
- Context snapshot reloads after restart.

Exit criteria:

- Decision generation can consume one typed context object.
- The UI can inspect objectives, constraints, risks, questions, decision history, and diagnostics.
- Existing context endpoints and tests continue to pass.

## Milestone 2: Decision Candidate Discovery Upgrade

Goal: discover typed, evidence-backed decision candidates and suppress noise.

Work:

- Add `DecisionCandidateType`:
  - `ArchitecturalFork`
  - `StrategicDirection`
  - `TacticalChoice`
  - `OperationalBlocker`
  - `ConstraintConflict`
  - `Contradiction`
  - `Supersession`
  - `WorkflowContinuation`
- Add analyzer interface:

```csharp
public interface IDecisionCandidateAnalyzer
{
    Task<IReadOnlyList<DecisionCandidateSignal>> AnalyzeAsync(
        DecisionGenerationContext context,
        CancellationToken cancellationToken);
}
```

- Implement built-in analyzers:
  - handoff analyzer
  - milestone analyzer
  - operational-context analyzer
  - decision-history analyzer
  - repository-state analyzer
- Refactor current line-scanning signal extraction into analyzers returning raw signals.
- Add a discovery pipeline:

```text
Typed Context
        |
Source Analyzers
        |
Raw Candidate Signals
        |
Deduplication
        |
Priority Scoring
        |
Resolved-Decision Suppression
        |
Candidate Persistence
```

- Deduplicate by type, normalized title, source fingerprint, affected artifacts, and related decisions.
- Suppress candidates when an accepted resolved decision already governs the issue.
- Do not suppress when new evidence contradicts or invalidates an accepted resolved decision.
- Persist ignored/deferred-equivalent states using existing candidate lifecycle where possible; do not create a background expiration process.
- Add `AffectedArtifacts` and `RelatedDecisionIds` fields if existing source references are not sufficient for UI and suppression.

Tests:

- Handoff blocker creates a blocking `ArchitecturalFork` or `OperationalBlocker`.
- Existing accepted resolved decision suppresses duplicate candidates.
- Contradictory operational context and handoff evidence creates a `Contradiction`.
- Ambiguous milestone direction creates a `TacticalChoice`.
- Repeated repository failure pattern creates an `OperationalBlocker` when evidence exists.
- Duplicate signals merge into one candidate.
- Every candidate has at least one evidence item and at least one source reference.

Exit criteria:

- The system can answer what decisions appear necessary, why they are necessary, how urgent they are, and whether they were already resolved.
- Candidate discovery is analyzer-driven and typed.
- Noise suppression is test-covered.

## Milestone 3: Option Generation

Goal: generate multiple viable alternatives for every promoted candidate.

Work:

- Add `DecisionOptionType`:
  - `Adopt`
  - `Preserve`
  - `Refactor`
  - `Replace`
  - `Delay`
  - `Remove`
  - `Expand`
  - `Constrain`
  - `Investigate`
- Extend `DecisionOption` with:
  - option type
  - assumptions
  - dependencies
  - diagnostics
  - source evidence
- Add `IOptionGenerationService` and keep `DecisionGenerationService.GenerateProposalAsync` as the public orchestrator.
- Generate candidate-type-specific options:
  - architectural fork: preserve, incrementally evolve, replace, hybrid
  - strategic direction: accelerate, maintain, reduce scope, pivot
  - tactical choice: implement now, implement later, implement differently, avoid
  - operational blocker: fix, work around, defer, escalate
  - contradiction: resolve toward source A, resolve toward source B, merge, investigate
  - constraint conflict: honor constraint A, honor constraint B, narrow scope, escalate
  - supersession: keep active decision, supersede, archive, gather evidence
  - workflow continuation: continue, pause, re-sequence, reduce scope
- Target three options by default.
- Require at least two options unless an explicit single-option justification is persisted.
- Add option validation:
  - reject duplicate options
  - reject non-actionable options
  - reject options unrelated to candidate evidence
  - reject empty assumptions/dependencies when they are required by option type
- Add option deduplication by normalized title, type, and semantic evidence overlap.
- Add `DecisionOptionRelationship` for conflicts and dependencies between options.
- Persist generation diagnostics with generated, rejected, and deduplicated options.

Tests:

- Every candidate type can generate options.
- Default candidates generate at least two options.
- Architectural forks generate materially distinct architecture options.
- Operational blockers generate fix/workaround/defer/escalate choices when evidence supports them.
- Duplicate options collapse.
- Invalid options are rejected with diagnostics.
- Single-option output requires explicit persisted justification.
- Recommendation is not generated in this milestone except as an absent/placeholder field when the existing API shape requires it.

Exit criteria:

- Humans receive real alternatives instead of inventing options.
- The current `options[0]` recommendation path is no longer part of option generation.

## Milestone 4: Tradeoff Analysis

Goal: analyze benefits, costs, risks, dependencies, consequences, and cross-option comparison before recommendation.

Work:

- Add structured analysis models:
  - `AnalyzedDecisionOption`
  - `DecisionBenefit`
  - `DecisionCost`
  - `DecisionRisk`
  - `DecisionDependency`
  - `DecisionConsequence`
  - `TradeoffImpact`
  - `TradeoffSeverity`
- Preserve existing `DecisionTradeoff` fields during migration, but make the generated package use structured analysis.
- Add `ITradeoffAnalysisService`.
- Add `IOptionComparisonService`.
- Analyze each option against:
  - candidate type
  - typed context goals
  - constraints
  - risks
  - prior decisions
  - repository state
  - dependencies
- Require every option to have at least:
  - one benefit
  - one cost
  - one risk
- Represent unknown risk explicitly instead of omitting risk.
- Add cross-option comparison:
  - relative strengths
  - relative weaknesses
  - unique advantages
  - unique risks
  - disqualifying constraints
- Persist analysis diagnostics:
  - input option
  - context fingerprint
  - generated analysis
  - unknowns
  - validation warnings

Tests:

- Benefits, costs, risks, dependencies, and consequences are generated for every option.
- Unknowns are explicit.
- Analysis is candidate-specific, not generic filler.
- Cross-option comparison identifies differences.
- Constraint-violating options are surfaced as risks or disqualifiers, not silently recommended.
- Diagnostics explain generated analysis.

Exit criteria:

- Humans can compare consequences without producing the analysis manually.
- Recommendation generation has structured evidence to consume.

## Milestone 5: Recommendation Generation

Goal: derive a recommendation from context, options, tradeoffs, constraints, risks, and evidence.

Work:

- Add `IRecommendationService`.
- Add `OptionEvaluation` containing:
  - strengths
  - weaknesses
  - risks
  - constraints
  - summary
  - score/ranking metadata only if it remains explainable
- Add `DecisionRecommendation` fields if needed:
  - summary
  - rationale
  - supporting factors
  - concerns
  - assumptions
  - alternative explanation
  - mode
- Add `RecommendationEvidence` and evidence types:
  - benefit
  - cost
  - risk
  - dependency
  - consequence
  - constraint
  - prior decision
  - repository state
- Support recommendation modes:
  - preferred option
  - preferred plus alternative
  - no recommendation
- Allow no recommendation when evidence is insufficient, uncertainty is excessive, or contradiction remains unresolved.
- Explain why the recommended option won and why each alternative lost.
- Generate concerns and assumptions for every recommendation.
- Refuse to recommend an option that violates hard constraints unless the recommendation mode is no recommendation or escalation.
- Remove hardcoded `options[0]` recommendation behavior.

Tests:

- Recommendations are derived from structured option evaluations.
- Reordering options does not change the recommendation when evidence is unchanged.
- Recommended option has supporting evidence.
- Alternatives have explicit losing rationale.
- Concerns and assumptions are present.
- Excessive uncertainty produces no recommendation.
- Constraint violation prevents recommendation or produces escalation/no-recommendation.

Exit criteria:

- The system can answer what it recommends, why, what evidence supports it, what assumptions matter, and why alternatives are weaker.
- At this point the generated proposal contains enough system-authored content for a human to resolve without writing the decision manually.

## Milestone 6: Decision Package Generation

Goal: harden generated proposals into governance-ready immutable decision packages after the core generation loop has been validated.

Tier 0 may use the existing proposal record as the first reviewable package shape if it contains generated options, structured tradeoffs, recommendation evidence, assumptions, and concerns. This milestone formalizes package versioning and package comparison as Tier 1 governance hardening.

Work:

- Add `DecisionPackage` as an immutable snapshot that contains:
  - candidate
  - typed context summary
  - options
  - analyzed options
  - recommendation
  - recommendation evidence
  - assumptions
  - open concerns
  - metadata
  - generated timestamp
- Add `DecisionPackageMetadata`:
  - context fingerprint/version
  - generator version
  - candidate id
  - repository state fingerprint
  - milestone id/path
  - source proposal id
- Add `IDecisionPackageService`.
- Add package validation:
  - summary required
  - context required
  - options required
  - recommendation or no-recommendation explanation required
  - evidence required
  - at least two options unless justified
  - recommended option id must exist when recommendation mode selects an option
- Store package versions under each proposal.
- Render deterministic package markdown with:
  - decision summary
  - context
  - options
  - tradeoff analysis
  - recommendation
  - supporting evidence
  - open concerns
  - assumptions
  - diagnostics
- Add package comparison:
  - recommendation changes
  - option changes
  - evidence changes
  - risk changes
  - context fingerprint changes

Tests:

- Package generation persists JSON and markdown.
- Missing required sections fail validation.
- Package identity is stable and repository-scoped.
- Package versions are immutable after creation.
- Package comparison detects recommendation and option changes.
- Resolution snapshots reference the package/proposal fingerprint used for authority.

Exit criteria:

- Humans review complete packages, not raw runtime objects.
- Generated packages are durable, inspectable, comparable, and ready for governance.

## Milestone 7: Interactive Decision Refinement

Goal: let humans guide regeneration without becoming package authors.

Work:

- Add structured `RefinementDirective` and `RefinementDirectiveType`:
  - `AddConstraint`
  - `RemoveConstraint`
  - `IncreasePriority`
  - `DecreasePriority`
  - `ExploreAlternative`
  - `ReevaluateRisk`
  - `ReevaluateCost`
  - `ReevaluateRecommendation`
  - `ClarifyGoal`
- Add `IRefinementAnalysisService`.
- Add `RefinementPlan`:
  - regenerate options
  - reevaluate tradeoffs
  - reevaluate recommendation
  - full regeneration
  - applied constraints
  - diagnostics
- Keep current direct `DecisionRefinementRequest` support for compatibility, but prefer directive-driven refinement in UI.
- Add endpoints to analyze refinement before mutation and regenerate scoped package versions.
- Preserve every refinement as:
  - request
  - directives
  - plan
  - old package version
  - new package version
  - comparison
  - diagnostics
- Classify the refinement's human authoring burden:
  - small directive-only adjustment is `MinorEdit`
  - scoped regeneration is `MajorRefinement`
  - replacement of generated content with human-authored content is `FullRewrite`
- Ensure refinement never mutates prior package versions.
- Add UI controls for structured directives and show old/new recommendation diff.

Tests:

- Constraint directive affects recommendation.
- Priority directive changes option evaluation.
- Risk directive updates tradeoff analysis.
- Alternative exploration adds or changes options.
- Goal clarification can trigger full regeneration.
- Stale package fingerprint rejects refinement.
- Version history and comparison persist after restart.

Exit criteria:

- Humans can correct assumptions, constraints, priorities, risks, and goals, then receive a regenerated package without manually rewriting the decision.

## Milestone 8: Decision Quality Evaluation

Goal: measure whether generated decisions reduce human decision-production burden.

Work:

- Add quality models:
  - `DecisionQualityAssessment`
  - `DecisionQualityRating`
  - `DecisionQualitySignal`
  - `QualitySignalDirection`
  - `QualitySignalSeverity`
  - `DecisionQualityReport`
  - `DecisionQualityTrend`
  - `HumanAuthoringBurdenSignal`
  - `HumanAuthoringBurdenReport`
- Add `IDecisionQualitySignalService`.
- Add `IDecisionQualityAssessmentService`.
- Add `IDecisionQualityReportService`.
- Add `IHumanAuthoringBurdenService` if burden classification is not implemented inside the quality services.
- Extract signals from:
  - resolution outcome
  - selected option
  - recommendation divergence
  - refinement count
  - refinement scope
  - recommendation stability
  - alternative utilization
  - rejection/archive/supersession
  - human rewrite indicators
  - generation bypass indicators
- Evaluate categories:
  - recommendation quality
  - option quality
  - tradeoff quality
  - context quality
  - constraint quality
  - human effort
  - human authoring burden
- Generate repository reports:
  - generated package count
  - accepted count/rate
  - modified count/rate
  - rejected count/rate
  - superseded count/rate
  - recommendation divergence rate
  - alternative utilization rate
  - review-only count/rate
  - minor-edit count/rate
  - major-refinement count/rate
  - full-rewrite count/rate
  - generation-bypassed count/rate
- Generate trend reports over persisted assessments.
- Add UI quality dashboard and trend view.
- Keep quality advisory and non-mutating.

Tests:

- Accepted recommended option produces positive quality signals.
- Rejected decision produces negative quality signals.
- Alternative selection lowers recommendation quality but preserves option usefulness.
- Major refinement or rewrite increases human-effort penalty.
- Full rewrite and generation bypass are recorded separately from ordinary refinement.
- Repeated recommendation reversal reduces stability.
- Reports and trends are deterministic and persisted.
- Quality assessment does not mutate decisions, proposals, packages, or execution projection.

Exit criteria:

- Command Center can answer whether generated decisions are useful, how much human effort remains, whether recommendations are improving, and whether alternatives/tradeoffs are valuable.

## Milestone 9: Decision Consumption Integration

Goal: make accepted resolved decisions direct execution with explicit influence traceability.

Work:

- Extend or wrap `ExecutionDecisionProjection` into `ExecutionDecisionContext` with:
  - directives
  - constraints
  - priorities
  - architecture rules
  - conflicts
  - diagnostics
- Keep compatibility with existing `ExecutionConstraint` and `ExecutionDirective` prompt rendering.
- Add `ExecutionDecisionPriority`.
- Add `ExecutionArchitectureRule`.
- Add projection rules:
  - include accepted resolved decisions
  - include active architectural direction
  - include active constraints and priorities
  - exclude open decisions, rejected decisions, deferred decisions, archived decisions, superseded decisions, unresolved proposals, and blocked decisions
  - expose only the replacement decision when supersession exists
- Strengthen conflict detection:
  - contradictory positive/negative directives
  - mutually exclusive architecture rules
  - superseded authority still projecting
  - execution request/milestone contradicting active decision
- Persist projection diagnostics:
  - included decisions
  - excluded decisions
  - superseded decisions
  - projected statements
  - conflicts
- Add influence traces per execution session:
  - decision id
  - projected directive/constraint/priority/rule
  - prompt section
  - execution session id
  - adherence observation when available
- Extend execution UI to show influencing decisions and directive source details.
- Update prompt builder to render priorities and architecture rules separately while preserving constraints/directives.

Tests:

- Accepted resolved decisions project.
- Unresolved proposals never project.
- Rejected, archived, deferred, and superseded decisions do not project.
- Supersession projects only the active replacement.
- Conflicting directives fail validation or block launch.
- Execution prompt includes constraints, directives, priorities, and architecture rules.
- Influence trace can answer which decisions affected an execution session.

Exit criteria:

- Every execution session can explain which decisions directed it and why.
- Execution receives no unresolved decision authority.
- A generated recommendation can be human-resolved, projected to execution, and measured for human authoring burden before Tier 1 hardening work begins.

## Tier 0 Validation Gate

Run this gate immediately after Milestones 1, 2, 3, 4, 5, and the minimal execution-consumption path in Milestone 9 are working.

Validation scenario:

```text
Repository evidence
        |
Typed decision context
        |
Discovered candidate
        |
Generated options
        |
Generated tradeoffs
        |
Generated recommendation
        |
Human resolution
        |
Execution projection
        |
Prompt guidance
        |
Human authoring burden record
```

Pass criteria:

- Candidate is discovered from repository evidence without human-authored candidate content.
- At least two viable options are generated without human-authored option content.
- Every option has generated tradeoff analysis.
- Recommendation is derived from evidence and not option order.
- Human resolves by selecting or rejecting generated content rather than writing the decision from scratch.
- Accepted resolved decision appears in execution context and prompt output.
- Burden classification is `ReviewOnly`, `MinorEdit`, or `MajorRefinement`; `FullRewrite` or `GenerationBypassed` means the core hypothesis is not yet validated.
- The validation uses a real repository workflow, not only synthetic unit-test fixtures.

If this gate fails, continue improving Milestones 1-5 and the minimal Milestone 9 path before adding package-version hardening, dashboards, trend reports, or final certification.

## Milestone 10: Automated Decision Generation Certification

Goal: certify that automated decision generation replaces human decision production with human governance.

Work:

- Add `DecisionGenerationCertificationResult`:
  - generation certified
  - governance certified
  - throughput certified
  - quality certified
  - consumption certified
  - workflow replacement certified
  - findings
  - failures
- Add `DecisionGenerationCertificationReport`.
- Add `IDecisionGenerationCertificationService`.
- Evaluate certification categories:
  - generation capability
  - governance compatibility
  - throughput improvement
  - human authoring burden reduction
  - decision quality
  - execution influence
  - workflow replacement alignment
- Certification requirements:
  - candidates discovered automatically
  - multiple options generated
  - tradeoffs generated
  - recommendations generated or responsibly withheld
  - packages generated and persisted
  - humans review/refine/resolve
  - humans do not author most final decision content
  - history preserved
  - quality assessments available
  - human authoring burden assessments available
  - execution consumes accepted resolved decisions
  - influence is traceable
- Certification failure conditions:
  - hardcoded recommendations
  - single-option generation without justification
  - missing evidence
  - resolution bypass
  - humans rewriting most generated packages
  - generated decisions frequently classified as `FullRewrite` or `GenerationBypassed`
  - recommendations ignored repeatedly
  - execution projection absent
  - influence not traceable
- Add certification scenarios:
  - architectural fork
  - workflow priority decision
  - contradiction with withheld recommendation
  - refinement after human changes assumptions
  - end-to-end repository lifecycle
- Add certification reports:
  - repository report
  - workflow report
  - human authoring burden report
  - executive report that directly answers whether human decision production has been replaced by system generation and human governance

Tests:

- Certification passes for fixtures that exercise discovery through execution influence.
- Certification fails when recommendation is hardcoded or order-based.
- Certification fails when options are missing.
- Certification fails when governance resolution is bypassed.
- Certification fails when no quality evidence exists after resolved generated decisions.
- Certification fails when most generated decisions require `FullRewrite`.
- Certification fails when manual decisions bypass generation more often than generated decisions reach resolution.
- Certification fails when execution influence cannot be traced.
- Certification report persists and reloads after restart.

Exit criteria:

- Certification can answer with evidence whether the system primarily generates decisions, humans primarily review/refine/resolve, humans are no longer the primary authors, execution is directed by resolved decisions, and workflow burden is reduced.

## Cross-Cutting Requirements

### Human Authoring Burden

Human authoring burden is a first-class workflow replacement metric. It measures how much decision-production work remained with the human after the system generated a candidate, options, tradeoffs, recommendation, and reviewable package.

Classification rules:

- `ReviewOnly`: the human reviewed generated content and resolved it without content changes.
- `MinorEdit`: the human supplied small structured guidance or a small rationale adjustment, and the generated recommendation or option set remained substantially intact.
- `MajorRefinement`: the human supplied substantial guidance that caused scoped regeneration, but the final decision still came from regenerated system output.
- `FullRewrite`: the human replaced generated decision content with human-authored option, tradeoff, recommendation, or rationale content.
- `GenerationBypassed`: a decision reached authority without passing through generated candidate/proposal content.
- `Unknown`: persisted evidence is insufficient to classify burden.

Recording rules:

- Proposal review should record whether the human only viewed, noted, requested refinement, or prepared for resolution.
- Refinement should record accepted changes, rejected changes, directive scope, regeneration scope, and whether replacement content was human-authored.
- Resolution should record whether the selected option was recommended, an alternative generated option, or a custom/human-authored option.
- Quality assessment should convert review/refinement/resolution evidence into a burden classification.
- Certification should fail when `FullRewrite` or `GenerationBypassed` dominates resolved decisions.

This metric must remain observational. It must not block resolution, mutate decisions, or punish a human for overriding generated content.

### Evidence and Source Attribution

Every generated object must carry source references where possible:

- context sections
- candidate signals
- candidates
- options
- option relationships
- benefits
- costs
- risks
- dependencies
- consequences
- option evaluations
- recommendation evidence
- assumptions
- concerns
- package versions
- refinements
- quality signals
- execution projections
- influence traces
- certification findings

Minimum source-reference fields remain:

```text
SourceKind
RelativePath
Section
ItemId
DecisionId
ProposalId
CandidateId
Excerpt
```

Add fingerprints to source references only if the existing `DecisionSourceReference` shape cannot support stale protection through surrounding metadata.

### Fingerprints and Stale Protection

Use SHA-256 over normalized UTF-8 content for:

- decision context snapshots
- typed generation context
- candidate source context
- option-generation inputs
- tradeoff-analysis inputs
- recommendation inputs
- package versions
- refinement base package/proposal
- resolution source package/proposal
- quality assessment inputs
- execution projection inputs
- influence traces
- certification input state

Reject stale mutation commands when relevant source fingerprints differ from the command payload.

### Markdown Projection Rules

Generated markdown should be deterministic and human-readable.

Preferred package projection order:

```text
Package ID
Proposal ID
Candidate ID
State
Generated At
Context Fingerprint
Decision Summary
Decision Context
Options
Option Relationships
Tradeoff Analysis
Recommendation
Recommendation Evidence
Open Concerns
Assumptions
Diagnostics
History
```

Markdown projection must not become lifecycle authority.

### Provider Boundary

Initial generation may use deterministic local heuristics if they satisfy quality and evidence requirements. If model-backed generation is added:

- define a provider abstraction behind generation services
- persist generator version and prompt/context fingerprint
- keep output validation mandatory
- reject invalid provider output
- never let provider output resolve decisions
- never let provider output mutate operational context
- make provider unavailability produce diagnostics, not corrupted lifecycle state

### Dashboard and Workspace Projection

Extend repository projections with decision-generation summary fields:

```text
DecisionGenerationSummary
  CandidateCount
  ActiveCandidateCount
  GeneratedPackageCount
  ReviewablePackageCount
  ResolvedGeneratedDecisionCount
  RecommendationDivergenceCount
  QualityAssessmentCount
  LatestQualityRating
  ExecutionInfluenceCount
  BlockingProjectionConflictCount
  LastGeneratedPackageAt
  LastQualityAssessmentAt
  LastExecutionProjectionAt
  LastGenerationCertificationAt
  GenerationCertificationResult
```

Keep projection read-only and backend-owned.

## Verification Commands

Backend build:

```text
dotnet build CommandCenter.slnx
```

Backend tests:

```text
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj
```

UI lint:

```text
npm run lint --prefix src/CommandCenter.UI
```

UI tests:

```text
npm run test --prefix src/CommandCenter.UI
```

UI build:

```text
npm run build --prefix src/CommandCenter.UI
```

Shell build:

```text
cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml
```

End-to-end UI certification:

```text
npm run test:e2e --prefix src/CommandCenter.UI
```

Run the relevant subset after each milestone and the full set before certification.

## Non-Goals

Do not implement:

- automatic decision approval
- automatic decision resolution
- automatic decision rejection
- automatic decision supersession
- automatic operational-context promotion
- unresolved decision projection into execution
- metrics-driven lifecycle mutation
- quality assessment blocking governance
- provider-owned decision authority
- Tauri-owned decision logic
- client-side lifecycle authority
- hidden private decision database
- background filesystem watchers for lifecycle mutation
- background polling that mutates lifecycle state
- raw conversation transcript storage
- productivity scoring
- a single opaque quality score without supporting signals
- replacement of existing decision lifecycle, reasoning, continuity, execution, or artifact infrastructure

## Final Exit State

Command Center can:

- build typed decision-generation context from repository evidence
- discover typed decision candidates automatically
- generate multiple viable options for candidates
- analyze tradeoffs for every option
- derive recommendations from evidence and withhold recommendations when appropriate
- assemble immutable governance-ready decision packages
- let humans refine intent through structured guidance
- preserve package versions, revision history, and package comparisons
- resolve decisions only through explicit human action
- evaluate generated decision quality from governance outcomes
- measure human authoring burden for generated and bypassed decisions
- project accepted resolved decisions into execution context
- trace which decisions influenced each execution session
- certify generation, governance, throughput, quality, consumption, and workflow replacement

The repository remains authoritative, humans remain decision authorities, execution remains governed by accepted resolved decisions, operational context remains settled understanding, and the system becomes the primary producer of decision packages rather than requiring humans to author them manually.
