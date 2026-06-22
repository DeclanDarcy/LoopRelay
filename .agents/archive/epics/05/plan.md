# Command Center Decision Lifecycle Implementation Plan

## Objective

Implement a first-class decision lifecycle in Command Center while preserving the existing architecture:

- Repository files under `.agents` remain authoritative.
- Execution sessions remain disposable.
- Operational context remains the home of settled project understanding.
- Human approval remains authoritative for decision resolution.
- React remains presentation state only.
- Tauri remains an HTTP bridge and sidecar host.
- Runtime memory remains a cache only and must be reconstructable from repository artifacts.

The completed lifecycle is:

```text
Repository State
Decision Context Resolution
Decision Discovery
Decision Proposal Generation
Decision Review
Decision Refinement
Decision Resolution
Decision Governance
Execution Consumption
Lifecycle Certification
Operational Adoption Evidence
```

The implementation is complete when Command Center can discover decision candidates, generate structured proposals, support human review and refinement, record authoritative outcomes, govern resolved decisions, project governed decisions into execution context, report decision health, certify recovery from repository artifacts, and collect adoption evidence from real project use.

This plan intentionally implements decision lifecycle only. It does not implement reasoning trajectory preservation, continuity fidelity, or continuity strategy. Hypotheses, rejected alternatives, architectural exploration, reasoning evolution, and long-horizon reasoning reconstruction are preserved only where they are part of a candidate, proposal, refinement, resolution, or governance finding. A later implementation plan should address reasoning trajectory preservation as a separate capability.

## Current Codebase Baseline

Command Center currently has:

- .NET backend sidecar in `src/CommandCenter.Backend`.
- React/TypeScript UI in `src/CommandCenter.UI`.
- Rust/Tauri shell in `src/CommandCenter.Shell`.
- Backend tests in `tests/CommandCenter.Backend.Tests`.
- Repository registration, artifact discovery, planning readiness, workspace projections, execution sessions, Git workflow, operational-context continuity, and artifact rotation.
- Shared repository and artifact infrastructure in `src/CommandCenter.Core`.
- Operational-context parsing, generation, review, promotion, diagnostics, and report services in `src/CommandCenter.Continuity` and `src/CommandCenter.Middle`.
- Execution context, provider launch, event monitoring, handoff workflow, commit, and push services in `src/CommandCenter.Execution`.

Existing decision support is artifact-centric:

- `ArtifactService` discovers `.agents/decisions/decisions.md` and `.agents/decisions/decisions.NNNN.md`.
- `ArtifactRotationService` can rotate current decisions markdown.
- `ExecutionContextService` currently loads `.agents/decisions/decisions.md` as an optional raw artifact named `CurrentDecisions`.
- `DecisionAnalysisService` in `src/CommandCenter.Continuity` extracts markdown bullet signals for operational-context decision retention. It does not own decision identity, state, proposals, resolution, lineage, or authority.
- The repository workspace reports `HasCurrentDecisions`, current/historical decision artifacts, and operational-context stable decisions, but has no dedicated decision lifecycle workspace.
- The UI has primary tabs for Workspace, Execution, Operational Context, and Continuity. There is no Decisions tab.

Primary gaps:

- No first-class `Decision` aggregate.
- No stable decision identity such as `DEC-0001`.
- No lifecycle state model.
- No outcome or resolution model.
- No relationship or lineage model.
- No repository-backed structured decision store.
- No decision context projection for reasoning.
- No candidate discovery, proposal generation, review, refinement, or resolution workflow.
- No resolved-decision projection into execution context.
- No candidate lifecycle for dismissed, expired, or duplicate candidates.
- No proposal lifecycle separate from review state.
- No explicit boundary for when resolved decisions should be proposed for operational-context assimilation.
- No governance coverage analysis for repeated ambiguity, repeated blockers, repeated forks, or repeated unresolved questions.
- No decision governance, health report, certification, or operational adoption reporting.

## Architecture Rules

1. Repository authority stays in repository artifacts.
   Decision state must be recoverable from files under `.agents/decisions`.

2. Human authority controls resolution.
   The system may identify, analyze, recommend, compare, and explain. It must not approve, resolve, override, or supersede decisions without an explicit user action.

3. Decision lifecycle is separate from execution.
   Execution implements work. The decision lifecycle manages decision evolution and authority. Execution may consume governed resolved decisions but may not mutate them.

4. Decision lifecycle is separate from operational context.
   Operational context carries settled understanding. Decision records carry lifecycle state, options, tradeoffs, rationale, resolution, and lineage. Operational context may distill resolved decisions later, but it is not the decision database.

5. Decision assimilation into operational context is review-mediated.
   Resolving a decision does not automatically mutate `.agents/operational_context.md` or `StableDecisions`. A resolved decision may create an assimilation recommendation. That recommendation becomes operational context only through the existing operational-context proposal, review, acceptance, and promotion workflow.

6. Assimilation recommendation creation is not assimilation policy.
   The decision lifecycle may identify that a resolved decision could matter to operational context and package that as a recommendation. Continuity owns whether that recommendation becomes an operational-context proposal, how it is merged with existing understanding, and how the review/promotion workflow applies it.

7. Decision lifecycle is not reasoning trajectory preservation.
   Decision artifacts preserve reasoning that is necessary to review, refine, resolve, govern, and consume decisions. They do not attempt to preserve every hypothesis, exploration path, rejected argument, or continuity-fidelity signal.

8. Proposal is not authority.
   Candidates and proposals are reasoning artifacts. Only a resolved decision outcome becomes authoritative project direction.

9. Review before mutation.
   Discovery and generation are read-only. Refinement changes proposals only. Resolution changes authoritative decisions only after explicit human action.

10. Governance is advisory but precedes execution consumption.
   Governance detects contradictions, broken lineage, missing metadata, coverage gaps, and execution-alignment issues. It does not mutate decisions or execution. Execution consumes governed resolved decisions, not merely resolved decisions.

11. Execution receives governed resolved decisions only.
    Open decisions, under-review decisions, draft proposals, review notes, unresolved revisions, and decisions with blocking governance findings must not be projected as execution constraints or directives.

12. React owns presentation state only.
    React may track selected decision, selected proposal, active tab, filters, expanded panels, and local text drafts. It must not infer lifecycle authority or state transitions.

13. Tauri remains a bridge.
    Rust commands should translate UI calls to backend HTTP endpoints and return typed results. Decision rules stay in the .NET backend.

## Target Solution Structure

Add a dedicated backend project:

```text
src/
  CommandCenter.Decisions/
    Abstractions/
    Extensions/
    Models/
    Persistence/
    Primitives/
    Projections/
    Services/
```

Project references:

- `CommandCenter.Decisions` references `CommandCenter.Core`.
- `CommandCenter.Decisions` may reference `CommandCenter.Continuity` for operational-context parsing and projection inputs.
- `CommandCenter.Execution` references `CommandCenter.Decisions` only for resolved-decision execution projection.
- `CommandCenter.Middle` references `CommandCenter.Decisions` for dashboard and workspace projection summaries, and for the bridge from resolved decisions into operational-context assimilation recommendation creation.
- `CommandCenter.Backend` references `CommandCenter.Decisions` and maps decision endpoints.

Add `src/CommandCenter.Decisions/CommandCenter.Decisions.csproj` to `CommandCenter.slnx`.

Set `<UseExecutionContextAlias>false</UseExecutionContextAlias>` in the new project unless it directly references `CommandCenter.Execution`.

Keep the existing `DecisionAnalysisService` in `CommandCenter.Continuity` until the lifecycle model is certified. Structured resolved decisions should later feed operational-context retention through explicit assimilation recommendations created in `CommandCenter.Middle`. Continuity still owns operational-context generation, merge policy, review, acceptance, and promotion; decision services must not mutate decision authority from continuity signals or mutate operational context directly.

## Target Repository Layout

Preserve the existing current and historical decision markdown files:

```text
.agents/
  decisions/
    decisions.md
    decisions.0001.md
    decisions.0002.md
```

Add structured lifecycle artifacts under the same repository-owned tree:

```text
.agents/
  decisions/
    records/
      DEC-0001/
        decision.json
        decision.md
        history.json
      DEC-0002/
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
        revisions/
          REV-0001.json
          REV-0001.md
    assimilation/
      DEC-0001/
        recommendation.json
        recommendation.md
    contexts/
      context.<timestamp>.json
    governance/
      governance.<timestamp>.json
    certification/
      certification.<timestamp>.json
    adoption/
      adoption.<timestamp>.json
```

Rules:

- `decision.json` is the structured authoritative record for a decision.
- `decision.md`, `candidate.md`, `proposal.md`, and `decisions.md` are human-readable projections.
- `history.json` records state transitions, resolution history, supersession, archival, and relationship changes.
- Candidate `history.json` records discovery, dismissal, expiration, duplicate marking, and promotion history.
- Proposal `history.json` records draft, generation, review, refinement, readiness, expiration, and resolution history.
- Assimilation recommendations are inputs to the operational-context workflow; they are not operational-context authority and they do not define continuity merge policy.
- IDs are human-inspectable and allocated by scanning existing artifact IDs and choosing the next sequence.
- Runtime caches may exist, but every lifecycle state must be rebuildable from the repository tree.
- `decisions.md` remains the current decision index for existing artifact browser compatibility.
- `decisions.NNNN.md` remains the rotated historical markdown projection.

## Core Domain Model

Implement these primitives and models in `CommandCenter.Decisions`:

```text
DecisionId
Decision
DecisionState
DecisionClassification
DecisionOutcome
DecisionResolution
DecisionRelationship
DecisionRelationshipType
DecisionMetadata
DecisionHistoryEntry
DecisionSourceReference
DecisionEvidence
DecisionContext
DecisionContextSnapshot
DecisionContextDiagnostics
DecisionContextValidationResult
DecisionCandidate
DecisionCandidateState
DecisionCandidatePriority
DecisionSignal
DecisionProposal
DecisionProposalState
DecisionOption
DecisionTradeoff
DecisionRecommendation
DecisionAssumption
DecisionAssimilationRecommendation
DecisionReviewWorkspace
DecisionReviewState
DecisionReviewNote
DecisionProposalRevision
DecisionRefinementRequest
DecisionConstraint
DecisionAssumptionRevision
DecisionOptionRevision
DecisionTradeoffRevision
ResolveDecisionCommand
DecisionResolutionRationale
DecisionResolutionHistory
ExecutionDecisionProjection
ExecutionConstraint
ExecutionDirective
ExecutionDecisionConflict
DecisionGovernanceFinding
DecisionGovernanceCategory
DecisionGovernanceReport
DecisionHealthAssessment
DecisionLifecycleCertificationResult
DecisionCertificationReport
DecisionOperationalAdoptionReport
```

Initial states:

```text
Open
UnderReview
Resolved
Superseded
Archived
```

Initial outcomes:

```text
Accepted
Rejected
Deferred
```

Initial classifications:

```text
Architectural
Strategic
Tactical
Operational
```

Initial relationship types:

```text
DependsOn
Supersedes
ConflictsWith
Supports
Constrains
DerivedFrom
RelatedTo
```

Minimum lifecycle transitions:

| From | Event | To |
| --- | --- | --- |
| Open | Mark under review | UnderReview |
| Open | Accept | Resolved |
| Open | Reject | Archived |
| Open | Defer | UnderReview |
| UnderReview | Accept | Resolved |
| UnderReview | Reject | Archived |
| UnderReview | Defer | UnderReview |
| Resolved | Supersede | Superseded |
| Superseded | Archive | Archived |

Invalid transitions must return a conflict from services and endpoints.

Initial candidate states:

```text
Discovered
Promoted
Dismissed
Expired
Duplicate
```

Minimum candidate transitions:

| From | Event | To |
| --- | --- | --- |
| Discovered | Promote | Promoted |
| Discovered | Dismiss | Dismissed |
| Discovered | Expire | Expired |
| Discovered | Mark duplicate | Duplicate |
| Promoted | Expire stale source | Expired |

Candidate expiration policy:

- No background timer, watcher, or elapsed-time-only rule may expire candidates.
- Expiration occurs only during explicit user-triggered discovery, governance, or candidate-management operations.
- A candidate may expire when its source fingerprint no longer exists in current decision context, its source milestone is no longer active or relevant, its source blocker has been resolved, its related decision has been resolved or superseded, a duplicate candidate has been promoted, or a governed resolved decision makes the candidate obsolete.
- Expiration must record reason, timestamp, triggering operation, source evidence, and prior candidate state in candidate history.
- Dismissal remains distinct from expiration: dismissal is a human judgment that the candidate is not worth pursuing, while expiration means the candidate no longer matches current repository state.
- Rediscovery after expiration must either create a new candidate with fresh evidence or append a new discovery history entry that reactivates only through an explicit backend transition.

Initial proposal states:

```text
Draft
Generated
Viewed
NeedsRefinement
ReadyForResolution
Refined
Resolved
Expired
Discarded
```

Proposal state is distinct from review notes and distinct from decision state. Review state records what a reviewer has done; proposal state records where the proposal is in the lifecycle.

Minimum proposal transitions:

| From | Event | To |
| --- | --- | --- |
| Draft | Generate | Generated |
| Generated | View | Viewed |
| Viewed | Mark needs refinement | NeedsRefinement |
| NeedsRefinement | Refine | Refined |
| Generated | Mark ready | ReadyForResolution |
| Viewed | Mark ready | ReadyForResolution |
| Refined | Mark ready | ReadyForResolution |
| ReadyForResolution | Resolve | Resolved |
| Generated | Expire | Expired |
| Viewed | Expire | Expired |
| NeedsRefinement | Expire | Expired |
| Refined | Expire | Expired |

## Backend Service Contracts

Add service contracts under `CommandCenter.Decisions.Abstractions`:

```text
IDecisionRepository
IDecisionArtifactProjectionService
IDecisionContextService
IDecisionDiscoveryService
IDecisionGenerationService
IDecisionReviewService
IDecisionRefinementService
IDecisionResolutionService
IDecisionGovernanceService
IDecisionProjectionService
IDecisionOperationalContextAssimilationService
IDecisionCertificationService
IDecisionOperationalAdoptionService
```

Persistence services:

- `FileSystemDecisionRepository` loads and saves structured lifecycle artifacts.
- It uses `IArtifactStore` for IO and `ArtifactPath.ResolveRepositoryPath` for path safety.
- It must reject absolute paths, path traversal, and repository escapes.
- It must use deterministic JSON serialization with string enums.
- It must preserve unknown JSON fields only if a forward-compatibility strategy is intentionally implemented. Otherwise fail visibly on unsupported schema versions.

Projection services:

- `DecisionArtifactProjectionService` renders markdown projections from structured records.
- It updates `decisions.md` after authoritative decision mutations.
- It does not derive authority from markdown projections.
- `DecisionOperationalContextAssimilationService` creates reviewable recommendation packages for the existing operational-context proposal workflow. It owns recommendation creation only. Continuity owns assimilation policy, proposal generation, review, acceptance, and promotion. The service must not write `.agents/operational_context.md` directly.

## Backend API Surface

Add `DecisionEndpoints.cs` under `src/CommandCenter.Backend/Endpoints` and map it from `Program.cs`.

Repository-scoped endpoints:

```text
GET  /api/repositories/{repositoryId}/decisions
GET  /api/repositories/{repositoryId}/decisions/{decisionId}
GET  /api/repositories/{repositoryId}/decisions/context
POST /api/repositories/{repositoryId}/decisions/context
GET  /api/repositories/{repositoryId}/decisions/candidates
POST /api/repositories/{repositoryId}/decisions/discover
POST /api/repositories/{repositoryId}/decisions/candidates/{candidateId}/promote
POST /api/repositories/{repositoryId}/decisions/candidates/{candidateId}/dismiss
POST /api/repositories/{repositoryId}/decisions/candidates/{candidateId}/expire
POST /api/repositories/{repositoryId}/decisions/candidates/{candidateId}/duplicate
GET  /api/repositories/{repositoryId}/decisions/proposals
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}
POST /api/repositories/{repositoryId}/decisions/candidates/{candidateId}/proposals
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/viewed
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/needs-refinement
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review/ready-for-resolution
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/notes
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/refinements
GET  /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/revisions
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/expire
POST /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/resolve
POST /api/repositories/{repositoryId}/decisions/{decisionId}/supersede
POST /api/repositories/{repositoryId}/decisions/{decisionId}/archive
GET  /api/repositories/{repositoryId}/decisions/{decisionId}/assimilation
POST /api/repositories/{repositoryId}/decisions/{decisionId}/assimilation/propose-operational-context
GET  /api/repositories/{repositoryId}/decisions/governance
POST /api/repositories/{repositoryId}/decisions/governance/reports
GET  /api/repositories/{repositoryId}/decisions/execution-projection
GET  /api/repositories/{repositoryId}/decisions/certification
POST /api/repositories/{repositoryId}/decisions/certification
POST /api/repositories/{repositoryId}/decisions/adoption/reports
GET  /api/repositories/{repositoryId}/decisions/adoption/reports
```

Endpoint behavior:

- Return `404` for missing repository, decision, candidate, proposal, or report.
- Return `400` for invalid requests and unsafe paths.
- Return `409` for invalid lifecycle transitions, stale proposals, conflicting lineage, artifact collisions, or unsafe resolution commands.
- Return response bodies as existing endpoints do: `{ error = "..." }` for failures.

## Tauri Shell Updates

Add Rust bridge commands in `src/CommandCenter.Shell/src/main.rs`:

```text
list_decisions
get_decision
get_decision_context
build_decision_context
list_decision_candidates
discover_decisions
promote_decision_candidate
dismiss_decision_candidate
expire_decision_candidate
mark_decision_candidate_duplicate
list_decision_proposals
get_decision_proposal
generate_decision_proposal
mark_decision_proposal_viewed
mark_decision_proposal_needs_refinement
mark_decision_proposal_ready_for_resolution
add_decision_review_note
refine_decision_proposal
list_decision_proposal_revisions
expire_decision_proposal
resolve_decision_proposal
supersede_decision
archive_decision
get_decision_assimilation_recommendation
propose_decision_operational_context_assimilation
get_decision_governance
generate_decision_governance_report
get_execution_decision_projection
run_decision_certification
generate_decision_adoption_report
list_decision_adoption_reports
```

The commands should mirror existing operational-context and continuity bridge style: call backend HTTP endpoints, deserialize typed responses where practical, and use `response_error` for non-success responses.

## UI Plan

Add a dedicated Decisions workspace tab.

Update:

```text
src/CommandCenter.UI/src/state/shellState.ts
src/CommandCenter.UI/src/components/shell/WorkspaceTabs.tsx
src/CommandCenter.UI/src/components/shell/CommandPalette.tsx
src/CommandCenter.UI/src/App.tsx
src/CommandCenter.UI/src/devTauriMock.ts
```

Add:

```text
src/CommandCenter.UI/src/types/decisions.ts
src/CommandCenter.UI/src/api/decisions.ts
src/CommandCenter.UI/src/hooks/useDecisionContext.ts
src/CommandCenter.UI/src/hooks/useDecisionDiscovery.ts
src/CommandCenter.UI/src/hooks/useDecisionProposals.ts
src/CommandCenter.UI/src/hooks/useDecisionGovernance.ts
src/CommandCenter.UI/src/hooks/useDecisionAssimilation.ts
src/CommandCenter.UI/src/features/decisions/DecisionLifecycleTab.tsx
src/CommandCenter.UI/src/features/decisions/DecisionBrowser.tsx
src/CommandCenter.UI/src/features/decisions/DecisionContextPanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionCandidatePanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionProposalViewer.tsx
src/CommandCenter.UI/src/features/decisions/DecisionOptionComparison.tsx
src/CommandCenter.UI/src/features/decisions/DecisionEvidenceList.tsx
src/CommandCenter.UI/src/features/decisions/DecisionReviewPanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionRevisionHistory.tsx
src/CommandCenter.UI/src/features/decisions/DecisionResolutionPanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionAssimilationPanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionGovernancePanel.tsx
src/CommandCenter.UI/src/features/decisions/DecisionCertificationPanel.tsx
src/CommandCenter.UI/src/test/characterization/decisionLifecycle*.test.tsx
```

UI rules:

- Show full proposal context, options, tradeoffs, recommendation, assumptions, evidence, and diagnostics.
- Keep evidence adjacent to the recommendation, tradeoff, assumption, or option it supports.
- Use backend state for lifecycle permissions.
- Disable or omit controls when backend says the action is not available.
- Show candidate state and proposal state separately from decision state.
- Make review notes visibly distinct from proposal revisions.
- Make recommendations visibly distinct from resolutions.
- Make operational-context assimilation recommendations visibly distinct from promoted operational context.
- Make governance findings advisory.
- Keep command palette actions navigation-only until backend workflow authority exists for mutation commands.

## Milestone 0: Decision Domain and Artifact Foundation

(See ./milestones/m0-domain-foundation.md)

## Milestone 1: Decision Context Resolution

(See ./milestones/m1-context-resolution.md)

## Milestone 2: Decision Discovery

(See ./milestones/m2-decision-discovery.md)

## Milestone 3: Decision Proposal Generation

(See ./milestones/m3-proposal-generation.md)

## Milestone 4: Decision Review Workspace

(See ./milestones/m4-review-workspace.md)

## Milestone 5: Decision Refinement Workflow

(See ./milestones/m5-refinement-workflow.md)

## Milestone 6: Decision Resolution

(See ./milestones/m6-decision-resolution.md)

## Milestone 7: Decision Governance

(See ./milestones/m7-decision-governance.md)

## Milestone 8: Execution Consumption

(See ./milestones/m8-execution-consumption.md)

## Milestone 9: Lifecycle Certification

(See ./milestones/m9-lifecycle-certification.md)

## Milestone 10: Operational Adoption and Long-Horizon Validation

(See ./milestones/m10-operational-adoption.md)

## Cross-Cutting Implementation Details

### Source Attribution

Every context item, candidate, option, tradeoff, recommendation, assumption, review finding, refinement, resolution rationale, assimilation recommendation, projected execution constraint, coverage finding, and governance finding must be traceable to source references where possible.

Minimum source reference fields:

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

### Operational-Context Assimilation Boundary

Decision services may create assimilation recommendation packages containing the resolved decision, selected outcome, rationale, projected stable-decision text, supporting evidence, and source references.

Decision services must not:

- decide whether the recommendation should become operational context
- merge recommendation content into an `OperationalContextDocument`
- write `.agents/operational_context.md`
- mark operational-context proposals accepted or promoted
- bypass existing operational-context review

Continuity services own operational-context interpretation, merge policy, proposal creation, semantic diff, review, acceptance, rejection, and promotion. Assimilation recommendations are inputs to that workflow only.

### Fingerprints and Stale Protection

Use SHA-256 fingerprints over normalized UTF-8 content for:

- decision context snapshots
- candidate source context
- proposal source candidate
- proposal content
- proposal state changes
- review content
- refinement base revision
- accepted resolution source proposal
- assimilation recommendation inputs
- governance report inputs
- projected governed execution decisions
- certification input state

Reject stale candidate, proposal, refinement, resolution, assimilation, governance, and execution-projection commands when the proposal, candidate, source decision, governance input, or source artifact changed since the command payload was created.

### Markdown Projection Rules

Markdown projections must be deterministic and human-readable.

Generated projection order:

```text
Decision ID
State
Classification
Outcome
Context
Options
Tradeoffs
Recommendation
Assumptions
Evidence
Relationships
Resolution
History
```

Markdown is a projection, not the domain source of truth. Structured JSON records remain authoritative for lifecycle state.

### Error Handling

Use existing backend conventions:

- `400 BadRequest` for invalid payloads and unsafe paths.
- `404 NotFound` for missing repository or lifecycle object.
- `409 Conflict` for invalid state transitions, stale commands, duplicate IDs, projection collisions, and broken lineage.
- `200 OK` for successful reads and mutations that return projections.

### Projection Refresh

After candidate promotion, candidate dismissal, candidate expiration, duplicate marking, proposal generation, proposal expiration, review, refinement, resolution, supersession, archive, assimilation recommendation generation, governance report generation, certification, or adoption report generation:

- Refresh repository workspace projection.
- Refresh dashboard decision summaries.
- Rebuild generated markdown projections when authoritative structured state changed.
- Do not let the UI infer updated authority before the backend projection returns it.

### Workspace and Dashboard Projection Updates

Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection` with a decision lifecycle summary:

```text
DecisionLifecycleSummary
  OpenDecisionCount
  UnderReviewDecisionCount
  ResolvedDecisionCount
  SupersededDecisionCount
  ArchivedDecisionCount
  ActiveCandidateCount
  DismissedCandidateCount
  ExpiredCandidateCount
  DuplicateCandidateCount
  GeneratedProposalCount
  ViewedProposalCount
  NeedsRefinementCount
  RefinedProposalCount
  ReadyForResolutionCount
  ExpiredProposalCount
  AssimilationRecommendationCount
  GovernanceFindingCount
  DecisionCoverageFindingCount
  BlockingGovernanceFindingCount
  LastResolutionAt
  LastGovernanceReportAt
  LastCertificationAt
  LastAdoptionReportAt
```

Keep this projection read-only and backend-owned.

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

## Non-Goals

Do not implement:

- decision sessions
- reasoning sessions
- reasoning trajectory preservation
- full hypothesis lifecycle
- full architectural exploration lifecycle
- full rejected-alternative preservation outside decision proposals
- continuity fidelity strategy
- session reuse
- session routing
- hidden private decision database
- automatic decision approval
- automatic decision resolution
- automatic operational-context assimilation
- automatic supersession
- automatic governance enforcement
- unresolved decision projection into execution
- decisions with blocking governance findings projected into execution
- client-side lifecycle authority
- Tauri-owned decision logic
- provider-owned decision authority
- raw conversation transcript storage
- productivity scoring
- single numeric decision quality score
- metrics-driven mutation
- background filesystem watchers
- background polling for lifecycle mutation

## Final Exit State

Command Center has a dedicated decision lifecycle capability that can:

- build deterministic decision context from repository state
- discover decision candidates with evidence
- generate structured proposals with options and tradeoffs
- support review before mutation
- preserve proposal evolution through revisions
- resolve decisions only through explicit human action
- persist authoritative decisions under `.agents/decisions`
- govern resolved decisions before execution consumption
- project governed resolved decisions into execution context
- detect governance issues without mutating state
- certify lifecycle integrity and repository recovery
- report operational adoption evidence

The repository remains authoritative, human resolution remains authoritative, execution remains disposable, operational context remains settled understanding, and decisions become first-class project objects with explicit lifecycle, candidate/proposal state, lineage, governance, assimilation boundaries, and execution influence.

## Future Work

This plan intentionally governs decision lifecycle only. Preservation of hypotheses, rejected alternatives outside proposal scope, architectural exploration, reasoning evolution, continuity fidelity, and continuity strategy remains outside scope and should be addressed as a separate reasoning trajectory preservation capability.
