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

(See ./milestones/m0-baseline-certification.md)

## Milestone 1: Decision Context Foundation Upgrade

(See ./milestones/m1-decision-context.md)

## Milestone 2: Decision Candidate Discovery Upgrade

(See ./milestones/m2-candidate-discovery.md)

## Milestone 3: Option Generation

(See ./milestones/m3-option-generation.md)

## Milestone 4: Tradeoff Analysis

(See ./milestones/m4-tradeoff-analysis.md)

## Milestone 5: Recommendation Generation

(See ./milestones/m5-recommendation-generation.md)

## Milestone 6: Decision Package Generation

(See ./milestones/m6-decision-packages.md)

## Milestone 7: Interactive Decision Refinement

(See ./milestones/m7-decision-refinement.md)

## Milestone 8: Decision Quality Evaluation

(See ./milestones/m8-decision-quality.md)

## Milestone 9: Decision Consumption Integration

(See ./milestones/m9-decision-consumption.md)

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

(See ./milestones/m10-generation-certification.md)

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
