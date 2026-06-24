# MVP Integration and Semantic Transparency Implementation Plan

## Goal

Deliver a cohesive MVP by connecting, surfacing, and consolidating capabilities that already exist in the current Command Center codebase.

The implementation should finish the product surface, not redesign working domain systems. The backend remains authoritative for domain state and reasoning. The Tauri shell bridges commands to the backend. The React UI renders authoritative projections, invokes approved lifecycle operations, and never recreates backend rules.

The MVP is complete when a user can operate the repository workflow, governance lifecycle, decision pipeline, execution lifecycle, reasoning subsystem, and operational-context lifecycle from the application while understanding what happened, why it happened, what evidence supports it, what constraints apply, what uncertainty remains, and what action is available next.

## Core Invariants

- Preserve existing project boundaries and service ownership.
- Prefer existing services, projections, endpoints, and models before adding new ones.
- Add narrow read models when semantic facts exist but are not projected.
- Add narrow command endpoints only when a service already owns the mutation but the route is missing.
- Every semantic concept should have one authority, one projection, and many consumers. Do not allow many authorities or many competing projections for the same concept.
- Workflow is the operational backbone for product state. Later workspaces should consume workflow projection where it provides operational status, gates, required actions, health, recovery, or certification context.
- Workflow projection is the canonical operational timeline for the application. Other workspaces may contextualize it, but they must not create competing lifecycle timelines.
- Do not move lifecycle authority into React, Tauri, workflow, repository summaries, health views, certification, or explainability components.
- Do not compute workflow progression, decision eligibility, recommendation scoring, quality, burden, reasoning confidence, taxonomy, compression, continuity evolution, execution prompt composition, git eligibility, or health in the UI.
- Derived state must remain disposable and rebuildable from authoritative repository or service evidence.
- Health must be rendered through dimensions, findings, diagnostics, and evidence. Summary labels may exist, but they must not replace decomposed health details.
- Certification is observational. It may report and persist findings, but it must not repair or mutate authoritative state.
- Explanations are presentation and composition only. They do not become a domain authority.
- Retire duplicate state, duplicate projections, duplicate UI models, and obsolete endpoints once authoritative replacements are integrated.

## Current Codebase Fit

The solution is organized around these projects:

- `src/CommandCenter.Core`: repository identity, artifact paths, artifact store, repository service, planning readiness, common projections.
- `src/CommandCenter.Execution`: execution context, prompt building, provider launch, monitoring, recovery, handoff processing, git commit and push workflow.
- `src/CommandCenter.Workflow`: workflow projection, gates, state-machine diagnostics, continuation, preparation, recovery, health, reports, certification.
- `src/CommandCenter.DecisionSessions`: governance session registry, lifecycle policy, transfer eligibility, transfer execution, recovery, observability, health, certification.
- `src/CommandCenter.Decisions`: decision discovery, candidates, proposals, generation, review, refinement, resolution, supersession, archive, governance, quality, execution influence, certification.
- `src/CommandCenter.Reasoning`: reasoning events, threads, relationships, graph, traces, query, reconstruction, materialization review, certification.
- `src/CommandCenter.Continuity`: operational-context parsing, lifecycle, compression, semantic diff, diagnostics, continuity reports.
- `src/CommandCenter.Middle`: repository dashboard and workspace projections that compose backend summaries for the UI.
- `src/CommandCenter.Backend`: minimal API endpoint mapping and service composition.
- `src/CommandCenter.Shell`: Tauri command bridge and backend sidecar lifecycle.
- `src/CommandCenter.UI`: React application, typed clients, hooks, components, and characterization tests.
- `tests/CommandCenter.Backend.Tests`: xUnit service, persistence, endpoint, projection, and certification tests.

Important current integration facts:

- `src/CommandCenter.Backend/Endpoints/WorkflowEndpoints.cs` already exposes workflow projection, diagnostics, timeline, history, transitions, gates, gate history, recovery, execution, handoff, decisions, operational context, git, continuation, preparation, health, reports, and certification.
- `src/CommandCenter.UI/src/api/workflow.ts` does not exist, and `src/CommandCenter.Shell/src/main.rs` has no workflow command bridge.
- `src/CommandCenter.UI/src/lib/executionWorkflow.ts` derives workflow steps from `RepositoryExecutionState`; this must be retired once authoritative workflow projection is consumed.
- `src/CommandCenter.Middle/Projections/RepositoryDashboardProjection.cs` and `RepositoryWorkspaceProjection.cs` include `RepositoryDecisionSessionSummary`, but `src/CommandCenter.UI/src/types/repositories.ts` does not expose `decisionSessionSummary`.
- `src/CommandCenter.Backend/Endpoints/DecisionSessionEndpoints.cs` exposes decision-session read routes but does not map `IDecisionSessionTransferService.ExecuteAsync` or `IDecisionSessionRecoveryService.RecoverAsync`.
- `src/CommandCenter.Shell/src/main.rs` has no decision-session command bridge.
- `src/CommandCenter.Backend/Endpoints/DecisionEndpoints.cs` already exposes many decision lifecycle endpoints, including discovery, candidate transitions, proposal generation, proposal review transitions, refinement, resolution, supersede, archive, governance, quality, influence, and certification.
- `src/CommandCenter.UI/src/api/decisions.ts` does not expose several lifecycle verbs already mapped by the backend, including discovery, candidate promotion/dismissal/expiration/duplicate, proposal generation, proposal review transitions, proposal discard/expire, supersede, and archive.
- `src/CommandCenter.Decisions/Services/DecisionLifecycleRules.cs` owns transition validation, but there is no read-only lifecycle eligibility projection for UI action availability.
- `src/CommandCenter.Execution/Models/ExecutionSession.cs` stores `PromptMetadata`, repository snapshots, commit preparation, push attempt metadata, and failure reason, but `ExecutionSessionSummary` omits prompt metadata and the UI does not show a launched-session prompt manifest.
- `ExecutionSessionService.PushAsync` persists retryable push failure state and then throws. The endpoint and client must return or refresh the updated retry state so users can retry with context.
- `ExecutionPromptBuilder` appends governed decisions and conflicts to launched prompts, while `ExecutionContextService` flattens governed conflicts into validation strings. The UI needs structured conflict visibility.
- `ReasoningReconstruction` exposes confidence as a string but not the full confidence rationale, missing evidence, and reconstruction scope required for semantic transparency.
- `UnderstandingDiffService` compares most operational-context items by normalized text and can represent modifications as remove/add pairs. It needs identity-aware modification detection.
- There is no shared explainability presentation layer. Existing decision, execution, reasoning, continuity, workflow, and governance panels render explanations differently.

## Delivery Rules

- Work milestone by milestone in order unless an implementation dependency requires a small preparatory slice from a later milestone.
- Each milestone must leave the application buildable and tests runnable.
- When adding a UI operation, wire backend endpoint, Tauri command, TypeScript client, hook/state refresh, UI control, and characterization tests together.
- When adding an explanation, render authoritative fields. If the field is missing, add it to the owning backend projection rather than inferring it in the UI.
- When adding or changing models, update backend serialization tests and frontend TypeScript types in the same milestone.
- When replacing duplicate surfaces, keep old UI reachable only until the authoritative replacement is tested, then remove the old code.
- Persist milestone evidence as implementation artifacts under `.agents/milestones/` or `.agents/certification/` as needed. These evidence files must be generated from the implemented code and must not be required to understand this plan.

## Milestone 0: Capability Verification and Consolidation

### Objective

Create a single implementation baseline before exposing more product surface. This milestone should be short, engineering-oriented, and limited to the information needed to sequence implementation safely.

### Implementation

1. Build a capability inventory covering Core, Workflow, Decision Sessions, Decisions, Reasoning, Continuity, Execution, Middle, Backend, Shell, and UI.
2. For each capability record the minimum useful facts: name, owning project, authority, entry points, consumers, current reachability, and current completion.
3. Assign exactly one disposition: `Core MVP`, `Deferred`, `Infrastructure`, `Extension Point`, `Experimental`, or `Retire`.
4. Perform an authority review for workflow lifecycle, execution lifecycle, decision lifecycle, decision-session lifecycle, reasoning, operational context, certification, recovery, observability, repository summaries, health, and diagnostics.
5. Record only actionable plan adjustments: sequencing corrections, duplicate concepts to consolidate, routes or clients to add, and capabilities to defer or retire.
6. Freeze the MVP boundary so later milestones focus on integration and surfacing, not rediscovery.

### Deliverables

- Capability inventory.
- Capability disposition register.
- Authority review.
- MVP adjustment log.

### Exit Criteria

- Every implemented capability is inventoried.
- Every capability has one disposition.
- Every semantic concept has exactly one authority.
- Every duplicate or ambiguity discovered during the review has a concrete adjustment, deferral, or retirement decision.
- Every Core MVP capability has an implementation path.
- Milestone 0 produces enough engineering direction to start Milestone 1 without turning into a documentation project.

## Milestone 1: Workflow Engine Integration

### Objective

Replace client-side workflow derivation with the authoritative workflow projection and make workflow state, gates, continuation, recovery, health, and certification visible. The workflow projection becomes the operational backbone and canonical operational timeline that later workspaces consume when they need current operational status, required action, gate, recovery, health, or certification context.

### Backend and Shell

1. Keep `CommandCenter.Workflow` as the workflow authority for projection and workflow-derived diagnostics.
2. Reuse existing routes in `WorkflowEndpoints.cs`:
   - `GET /api/repositories/{repositoryId}/workflow`
   - `GET /api/repositories/{repositoryId}/workflow/diagnostics`
   - `GET /api/repositories/{repositoryId}/workflow/timeline`
   - `GET /api/repositories/{repositoryId}/workflow/history`
   - `GET /api/repositories/{repositoryId}/workflow/transitions`
   - `GET /api/repositories/{repositoryId}/workflow/gates`
   - `GET /api/repositories/{repositoryId}/workflow/gates/history`
   - `GET /api/repositories/{repositoryId}/workflow/recovery`
   - `POST /api/repositories/{repositoryId}/workflow/recover`
   - `GET /api/repositories/{repositoryId}/workflow/execution`
   - `GET /api/repositories/{repositoryId}/workflow/handoff`
   - `GET /api/repositories/{repositoryId}/workflow/decisions`
   - `GET /api/repositories/{repositoryId}/workflow/operational-context`
   - `GET /api/repositories/{repositoryId}/workflow/git`
   - `GET /api/repositories/{repositoryId}/workflow/continuation/evaluation`
   - `POST /api/repositories/{repositoryId}/workflow/continuation/run`
   - `GET /api/repositories/{repositoryId}/workflow/continuation/history`
   - `GET /api/repositories/{repositoryId}/workflow/preparation/evaluation`
   - `POST /api/repositories/{repositoryId}/workflow/preparation/run`
   - `GET /api/repositories/{repositoryId}/workflow/preparation/history`
   - `GET /api/repositories/{repositoryId}/workflow/health`
   - `GET /api/repositories/{repositoryId}/workflow/reports/repository`
   - `GET /api/repositories/{repositoryId}/workflow/reports/progression`
   - `GET /api/repositories/{repositoryId}/workflow/reports/human-governance`
   - `GET /api/repositories/{repositoryId}/workflow/reports/readiness`
   - `GET /api/repositories/{repositoryId}/workflow/certification`
   - `POST /api/repositories/{repositoryId}/workflow/certification`
3. Add Tauri commands in `src/CommandCenter.Shell/src/main.rs` for every Core MVP workflow read/action route. Prefer small `backend_get_value` and `backend_post_value` helpers to avoid duplicating request/error handling.
4. Preserve backend error semantics. Return backend conflict, not found, and bad request messages unchanged through the shell.

### UI

1. Add `src/CommandCenter.UI/src/types/workflow.ts` with TypeScript models matching `CommandCenter.Workflow.Models`.
2. Export workflow types from `src/CommandCenter.UI/src/types/index.ts`.
3. Add `src/CommandCenter.UI/src/api/workflow.ts` and export it from `src/CommandCenter.UI/src/api/index.ts`.
4. Add workflow hooks such as `useWorkflowProjection`, `useWorkflowHistory`, `useWorkflowGates`, `useWorkflowContinuation`, `useWorkflowRecovery`, `useWorkflowHealth`, and `useWorkflowCertification`.
5. Replace `getExecutionWorkflowSteps` usage with workflow projection data.
6. Retire `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after all consumers use authoritative workflow data.
7. Replace or adapt `WorkspaceRail` and `ExecutionWorkflowRail` to render:
   - current stage
   - progress state
   - stage reasoning
   - blocking gate
   - required human action
   - current transition
   - satisfying commands
   - continuation state
   - recovery state
   - health dimensions
   - certification findings
8. Add workflow panels under `src/CommandCenter.UI/src/features/workflow/` or move existing rail components there:
   - `WorkflowOverviewPanel`
   - `WorkflowHistoryPanel`
   - `WorkflowGatePanel`
   - `WorkflowContinuationPanel`
   - `WorkflowRecoveryPanel`
   - `WorkflowHealthPanel`
   - `WorkflowCertificationPanel`
9. Integrate workflow into repository workspace, execution workspace, and dashboard summary without duplicating the domain model.
10. Establish a shared workflow consumption pattern for later milestones:
   - repository workspace shows workflow as primary operational status
   - decision-session workspace links governance state back to workflow gates and required actions
   - execution workspace shows execution as a workflow stage, not a separate workflow model
   - operational-context workspace shows review and promotion state through workflow gates where applicable

### Tests

- Add backend endpoint tests for any route not already covered.
- Add shell command tests where feasible.
- Add UI characterization tests proving workflow panels render projection stage, gate reason, satisfying command, recovery diagnostics, health dimensions, and certification findings.
- Add a regression test that no UI workflow state is derived from `RepositoryExecutionState`.

### Exit Criteria

- Workflow projection is the sole UI workflow source.
- Users can see current stage, progress, reasoning, gates, required human actions, continuation, recovery, health, and certification.
- Workflow history is reconstructable from projected evidence.
- Workflow gates explain why progress is blocked, who owns the unblock action, and which command satisfies it.
- No other workspace creates a parallel lifecycle timeline for operational product state.
- Parallel client-side workflow derivation is removed.
- Later workspaces have a documented consumption pattern for workflow projection instead of bypassing the operational backbone.

## Milestone 2: Governance Workspace Integration

### Objective

Make the governance lifecycle visible and actionable while preserving `CommandCenter.DecisionSessions` as the lifecycle authority. User-facing product language should be `Governance`; backend, API, and model names should keep `DecisionSession` where that is the established implementation boundary.

### Backend and Shell

1. Reuse existing decision-session read routes in `DecisionSessionEndpoints.cs`.
2. Add a narrow transfer execution endpoint:
   - `POST /api/repositories/{repositoryId}/decision-sessions/transfers`
   - Calls `IDecisionSessionTransferService.ExecuteAsync(repositoryId)`.
   - Returns `DecisionSessionTransferResult`.
   - Does not execute transfer unless policy and eligibility services allow it.
3. Add a narrow persisted recovery endpoint:
   - `POST /api/repositories/{repositoryId}/decision-sessions/recovery`
   - Calls `IDecisionSessionRecoveryService.RecoverAsync(repositoryId)`.
   - Returns `DecisionSessionRecoveryResult`.
4. Keep `GET /decision-sessions/recovery` as assessment-only and `POST /decision-sessions/recovery` as the persisted recovery trigger.
5. Add Tauri commands for:
   - session list and active session
   - diagnostics
   - metrics, statistics, economics, coherence
   - lifecycle policy and policy diagnostics
   - transfer eligibility and eligibility diagnostics
   - lifecycle projection, history, influence, health
   - continuity artifacts and artifact lookup
   - transfers, transfer history, transfer diagnostics, transfer execution
   - recovery, recovery history, recovery diagnostics, persisted recovery
   - workflow summary, workflow health, workflow influence
   - certification get/report/run

### UI

1. Add `src/CommandCenter.UI/src/types/decisionSessions.ts` matching `CommandCenter.DecisionSessions.Models` plus workflow governance projection models.
2. Update `src/CommandCenter.UI/src/types/repositories.ts` to include `decisionSessionSummary` on dashboard and workspace projections.
3. Add `src/CommandCenter.UI/src/api/decisionSessions.ts` and export it.
4. Add hooks for lifecycle projection, policy, eligibility, analysis, transfers, recovery, continuity artifacts, health, and certification.
5. Add repository-level governance summary rendering using `RepositoryDecisionSessionSummary`:
   - active session id
   - lifecycle state
   - lifecycle decision
   - transfer eligibility status
   - coherence score
   - transfer pressure
   - cache pressure or miss risk
   - health dimensions
6. Add a dedicated governance workspace under `src/CommandCenter.UI/src/features/governance/`:
   - `GovernanceWorkspace`
   - `DecisionSessionLifecyclePanel`
   - `DecisionSessionAnalysisPanel`
   - `DecisionSessionEligibilityPanel`
   - `DecisionSessionTransferPanel`
   - `DecisionSessionContinuityArtifactPanel`
   - `DecisionSessionRecoveryPanel`
   - `DecisionSessionHealthPanel`
   - `DecisionSessionCertificationPanel`
7. Lifecycle explanation must display authoritative reuse score, transfer score, reason, contributing factors, transfer pressure, cache risk, continuity benefit, coherence, fragmentation, and growth when present.
8. Transfer readiness must distinguish "transfer recommended" from "transfer currently executable".
9. Recovery display must distinguish recovered, diagnosed, requires intervention, duplicate active sessions, interrupted transfers, discarded snapshots, and rebuilt snapshots.
10. Workflow integration must consume only `IWorkflowDecisionSessionService` and `IDecisionSessionObservabilityService` outputs already exposed through workflow and decision-session endpoints.
11. Where governance affects product status, render the workflow gate or required human action next to the governance detail instead of inventing a separate governance workflow.
12. Navigation, page titles, and visible UI labels use Governance terminology while code-facing contracts keep DecisionSession naming where they mirror backend authority.

### Tests

- Backend endpoint tests for transfer execution and persisted recovery.
- Repository projection tests proving `decisionSessionSummary` serializes and TypeScript types include it.
- UI tests for repository governance summary, lifecycle explanation, transfer eligibility, recovery, health, and certification.

### Exit Criteria

- Decision-session functionality is available through one frontend client.
- Repository summaries surface governance without detailed duplication.
- A dedicated Governance Workspace presents lifecycle, analysis, transfer, continuity artifact, recovery, health, certification, and history.
- Transfer trigger and persisted recovery trigger are reachable through approved UI actions.
- Workflow reflects governance state without owning it.
- No duplicate governance state or authority path is introduced.

## Milestone 3: Decision Pipeline Completion

### Objective

Make the existing decision lifecycle operational end to end from product entrypoints:

```text
Discovery -> Candidate -> Proposal -> Review -> Refinement -> Resolution -> Supersession -> Archive
```

### Backend and Shell

1. Inventory all decision lifecycle routes already mapped in `DecisionEndpoints.cs`.
2. Add or expose any missing request/response models in TypeScript for:
   - `DecisionDiscoveryResult`
   - `DecisionProposalTransitionRequest`
   - `CreateDecisionProposalCommand` or existing generation request type
   - `SupersedeDecisionCommand`
   - `ArchiveDecisionCommand`
   - proposal generation diagnostics
3. Add a backend read-only lifecycle eligibility projection over `DecisionLifecycleRules`:
   - current state
   - allowed next states
   - allowed actions
   - blocked actions
   - blocking reasons
   - required request fields
   - action command name
4. Prefer a single route returning candidate, proposal, and decision eligibility for a repository:
   - `GET /api/repositories/{repositoryId}/decisions/lifecycle/eligibility`
5. If a single route becomes too large, split by entity while keeping the rule evaluation in one backend service.
6. Add shell commands for Core MVP lifecycle operations:
   - `discover_decisions`
   - `promote_decision_candidate`
   - `dismiss_decision_candidate`
   - `expire_decision_candidate`
   - `mark_decision_candidate_duplicate`
   - `generate_decision_proposal`
   - `expire_decision_proposal`
   - `discard_decision_proposal`
   - `mark_decision_proposal_viewed`
   - `mark_decision_proposal_needs_refinement`
   - `mark_decision_proposal_ready_for_resolution`
   - `supersede_decision`
   - `archive_decision`
   - `get_decision_lifecycle_eligibility`
7. Shell commands must call backend endpoints and return backend domain responses directly.

### UI

1. Expand `src/CommandCenter.UI/src/api/decisions.ts` with typed functions for all Core MVP lifecycle operations.
2. Expand `useDecisionDiscovery`, `useDecisionProposals`, `useDecisionProposalReview`, and related hooks with action methods and refresh behavior.
3. Update `DecisionCandidateBrowser` to show:
   - state
   - signals
   - evidence
   - duplicate status
   - allowed actions
   - unavailable action reasons
4. Add candidate actions:
   - discover
   - promote
   - dismiss
   - expire
   - mark duplicate
   - generate proposal
5. Proposal generation flow must refresh candidates, refresh proposals, navigate to the generated proposal where appropriate, and display generation diagnostics, generated proposal id, generation mode, accepted option count, rejected option count, deduplicated option count, and validation diagnostics.
6. Update proposal viewer/review panels to render review state, allowed transitions, unavailable reasons, last transition, and transition controls.
7. Add supersede and archive actions for resolved decisions, including target decision selection, rationale, resulting state, relationships, governance impact, and execution projection refresh.
8. Classify lower-priority lifecycle features as Core MVP, Deferred, Internal, or Remove:
   - proposal review notes
   - proposal revision list
   - revision comparison
   - context snapshot listing
9. Deferred features may remain reachable only if intentionally placed in an advanced or diagnostic view.

### Tests

- Backend tests for lifecycle eligibility projection.
- Endpoint tests for shell-reachable lifecycle routes.
- UI tests for candidate actions, proposal generation, proposal review transitions, supersede, archive, and refresh behavior.
- End-to-end test path:
  - discover candidate
  - promote candidate
  - generate proposal
  - mark viewed
  - mark needs refinement
  - refine proposal
  - mark ready for resolution
  - resolve decision
  - supersede decision
  - archive superseded decision

### Exit Criteria

- Every Core MVP decision lifecycle operation is reachable from the product.
- UI action availability comes from backend eligibility, not client guesses.
- Proposal generation feeds live review, refinement, and resolution panels.
- Supersede and archive update decision governance and execution influence projections.
- Deferred lifecycle endpoints have explicit dispositions.

## Milestone 4: Decision Transparency

### Objective

Expose why decisions, recommendations, options, quality ratings, governance findings, and execution influence outcomes exist without changing the decision algorithms. This milestone produces and surfaces authoritative decision explanation projections. Shared cross-domain rendering belongs to Milestone 8.

### Backend

1. Ensure `DecisionProposal` serialization includes all generated transparency data already produced by services:
   - generation diagnostics
   - option validation results
   - rejected options
   - deduplicated options
   - analyzed options
   - tradeoff comparisons
   - tradeoff analysis diagnostics
   - recommendation mode
   - recommendation evidence
   - option evaluations
   - supporting factors
   - concerns
   - assumptions
   - alternative explanations
2. If any of these are computed but not persisted or projected, add them to the owning model and repository serialization.
3. Add read-only projection fields where quality and burden currently expose labels without basis:
   - quality score contribution
   - threshold crossed
   - signal contribution
   - override reason
   - burden selection rule
   - burden winning signal
   - unknown vs inferred status
4. Extend governance and influence projections where needed to expose included, excluded, superseded, conflicting, ignored, and blocked decisions with reasons.
5. Keep these outputs as decision-owned projections. They are the semantic inputs that later shared explainability components will render.

### UI

1. Add decision-specific projection renderers under `src/CommandCenter.UI/src/features/decisions/`:
   - `DecisionRecommendationExplanation`
   - `DecisionOptionEvaluationTable`
   - `DecisionRejectedOptionList`
   - `DecisionQualityExplanation`
   - `DecisionBurdenExplanation`
   - `DecisionGovernanceExplanation`
   - `DecisionInfluenceExplorer`
2. Update `DecisionProposalViewer` to display recommendation mode, rationale, confidence when available, supporting factors, concerns, assumptions, alternative explanations, recommendation evidence, and option evaluations.
3. Update option views to display score, rank, score explanation, benefits, costs, risks, dependencies, constraints, disqualification, and required human action.
4. Display rejected, disqualified, deduplicated, invalid, insufficient-evidence, and duplicate options in a visible section rather than hiding them behind diagnostics.
5. Update `DecisionQualityPanel` to show score, rating, signal contribution, thresholds, overrides, warnings, unknowns, and burden reasoning.
6. Update governance panels to show resolution authority, stale authority, recommendation divergence, lifecycle state, allowed transitions, blocked transitions, transition reasons, governance findings, and authority violations.
7. Update execution influence panels to show why decisions were included, excluded, superseded, conflicted, ignored, or converted into constraints/directives/priorities/rules.
8. Keep all calculations in backend projections. UI components render fields and group them for comprehension only.
9. Avoid building generic explanation abstractions in this milestone. If a component would be useful across domains, keep it local and migrate it during Milestone 8.

### Tests

- Backend serialization and projection tests for transparency fields.
- UI characterization tests for recommendation explanation, option scoring, rejected options, quality contribution, burden reasoning, governance state, and influence exclusion/conflict reasons.
- Regression tests proving no UI-side scoring, ranking, quality, burden, or governance calculation helpers exist.

### Exit Criteria

- Every recommendation explains why it exists and which assumptions, concerns, evidence, and alternatives matter.
- Every option explains score, rank, constraints, disqualification, and evidence.
- Rejected and excluded alternatives remain visible.
- Quality, burden, governance, and influence are explainable from authoritative data.
- Duplicate decision reasoning and duplicate presentation summaries are removed or replaced.

## Milestone 5: Execution Transparency

### Objective

Make execution explainable: what was launched, what context was included, what recovery happened, what failed, what is retryable, what changed, what is safe to commit, and what is safe to push.

### Backend

1. Add an `ExecutionPromptManifest` model that captures requested context and delivered context for each launched session:
   - session id
   - generated at
   - full prompt text or persisted prompt artifact reference
   - requested artifact paths
   - requested artifact roles
   - requested context bytes
   - requested context characters
   - delivered artifact paths
   - delivered artifact roles
   - delivered context bytes
   - delivered context characters
   - dirty repository flag at request time
   - dirty repository flag at delivery time when known
   - governed decision count requested
   - governed decision count delivered
   - operational context source requested and delivered
   - handoff source requested and delivered
   - milestone source requested and delivered
   - provider delivery status
   - provider adjustments, including truncation, refusal, provider-added wrapper, or provider cache reference when present
   - divergence reason when delivered context differs from requested context
   - diagnostics
2. Persist the launched prompt manifest with the execution session. The manifest is app execution metadata, not repository authority.
3. If the provider abstraction cannot yet report delivered-context divergence or adjustments, record delivered context as equal to requested context, provider adjustments as empty, and include an explicit `NoProviderDivergenceSignal` diagnostic. Keep the model ready for future provider limits, refusals, wrappers, cache references, or delivery failures.
4. Add `GET /api/execution-sessions/{sessionId}/prompt` to return the launched manifest.
5. Extend `ExecutionSessionSummary` or add a `ExecutionSessionTransparency` endpoint for:
   - prompt metadata
   - recovery ran
   - recovery trigger
   - reattach attempted
   - reattach succeeded or failed
   - orphaned provider state
   - session marked failed by recovery
   - recovery event timestamp
   - provider process state
   - exit code
   - last activity
   - stale activity
   - event retention trimming
   - monitoring warnings
6. Adjust push behavior so a failed push returns the updated retry state to the UI:
   - Keep `ExecutionSessionService.PushAsync` as the execution authority.
   - In `GitEndpoints.MapPush`, when push fails after state persistence, load the latest session summary and include it in a structured conflict body, or return a typed `PushAttemptResult` that includes failure and session.
   - Update shell and UI clients to refresh or consume that session.
7. Add a backend git action eligibility read model:
   - commit preparation loaded
   - preparation current
   - selected path count
   - commit message present
   - repository state allows commit
   - session exists
   - awaiting push
   - commit SHA exists
   - remote branch state
   - previous push failure
   - disabled reasons
8. Add structured governed decision conflict details to execution context diagnostics instead of only flattened validation strings.
9. Add handoff processing transparency fields:
   - handoff produced
   - handoff missing
   - handoff archived
   - archive path or sequence
   - archive failed
   - handoff validated
   - validation failure
   - resulting session state
10. Add semantic categories and consequence text to execution events:
   - launch
   - provider
   - monitoring
   - recovery
   - handoff
   - git
   - failure

### UI

1. Extend execution TypeScript types for prompt manifest, recovery diagnostics, git eligibility, handoff processing, monitoring health, structured governed conflicts, and semantic event grouping.
2. Add `getExecutionPromptManifest`, `getExecutionTransparency`, and git eligibility client functions.
3. Update `ExecutionSessionPanel` and `ExecutionTab` to show the session-level launched prompt manifest. Clearly distinguish preview context, requested launched context, delivered launched context, and provider adjustments.
4. Add recovery banner and recovery event details to distinguish normal run failure from startup recovery failure.
5. Update `GitWorkflowPanel` to show commit and push precondition checklists, blocked reasons, retry warnings, previous push failure, and push attempt history.
6. Update `GitPathBucket` and `GitWorkflowEvidence` to distinguish execution-generated changes from pre-existing repository changes and provide bulk actions to select only execution-generated changes or deselect pre-existing changes.
7. Update handoff panels to show post-processing state, archive diagnostics, validation diagnostics, and whether provider failure differs from handoff processing failure.
8. Replace placeholder monitoring text with real monitoring health fields.
9. Update execution validation list to render governed decision conflicts as governance blockers with decision id, conflicting excerpt, conflict reason, affected prompt/context, and resolution path.
10. Group execution events by semantic category and display event consequence plus related state change.

### Tests

- Backend tests proving prompt manifest is persisted, distinguishes requested and delivered context, records provider adjustments, and differs from preview when appropriate.
- Backend tests for push conflict response containing updated retry state.
- Backend tests for git eligibility branches and structured governed conflicts.
- UI tests for prompt manifest, provider adjustments, recovery banner, push retry, disabled commit/push reasons, handoff processing diagnostics, pre-existing change warnings, and event grouping.

### Exit Criteria

- Users can inspect requested context and delivered context for the provider launch.
- Provider adjustments are represented explicitly, even when no adjustment signal is available.
- Recovery behavior is distinct from normal execution failure.
- Push failures leave an understandable retryable state.
- Commit and push controls explain blocked preconditions.
- Handoff post-processing is visible and distinguishable from provider failure.
- Monitoring diagnostics are real.
- Governed decision conflicts are distinct from generic validation errors.
- Pre-existing changes are separated from execution-generated changes.

## Milestone 6: Reasoning Transparency

### Objective

Make reasoning conclusions explain their provenance, confidence, thresholds, reconstruction scope, capture mode, authority boundaries, lifecycle risk, and diagnostics.

### Backend

1. Build a reasoning transparency inventory for:
   - materialization recommendations
   - reconstruction confidence
   - reconstruction direction
   - capture provenance
   - inferred reasoning
   - skipped or deduplicated captures
   - authority-boundary blocks
   - taxonomy lifecycle risk
   - diagnostics
2. Extend materialization review models to include:
   - literal recommendation enum
   - failed scenario count
   - repeated workflow count
   - thresholds
   - elevated risk signals
   - branch reason
   - diagnostics
3. Extend reconstruction models to include:
   - confidence rationale
   - event evidence present
   - relationship evidence present
   - trace diagnostics present
   - missing evidence
   - why confidence was not higher
   - forward or backward direction
   - target and source reference
   - historical cutoff
   - reachable and unreachable evidence where known
4. Extend reasoning event or projection models to distinguish capture modes:
   - Manual
   - Assisted
   - Inferred
5. For inferred capture, expose source transition, source artifact, capture reason, captured by, and source timestamp.
6. For skipped or deduplicated capture, expose skip reason, existing event id, and duplicate signal where relevant.
7. Replace plain boundary errors with structured boundary error responses:
   - boundary rule
   - owning domain
   - rejected assertion
   - allowed alternative
   - diagnostic detail
8. Extend taxonomy lifecycle risk findings with:
   - family
   - event type count
   - event type threshold
   - terminal event type present
   - terminal event types
   - reason risk was or was not flagged
9. Normalize reasoning diagnostics by category:
   - evidence
   - confidence
   - materialization
   - reconstruction
   - capture
   - authority boundary
   - lifecycle risk
   - validation

### UI

1. Update reasoning TypeScript types and API responses.
2. Update `ReasoningMaterializationReviewPanel` to render literal recommendations and threshold basis.
3. Update `ReasoningReconstructionPanel`, `ReasoningQueryPanel`, and trace panels to show confidence rationale, evidence branches, missing evidence, direction, scope, and historical cutoff.
4. Update `ReasoningEventFeed` with capture provenance badges and inferred capture details.
5. Add authority boundary notices that identify the owning domain and allowed alternative.
6. Update taxonomy and materialization review rendering to show lifecycle-risk rules and thresholds.
7. Add a grouped reasoning diagnostics component.

### Tests

- Backend tests for materialization threshold branches.
- Backend tests for confidence rationale branches.
- Backend tests for forward/backward reconstruction scope.
- Backend tests for manual, assisted, and inferred capture.
- Backend tests for boundary violation explanations.
- Backend tests for lifecycle risk thresholds.
- UI tests for rendering each explanation branch.

### Exit Criteria

- Users can understand why materialization was recommended or not.
- Confidence labels explain their evidence and missing evidence.
- Reconstruction scope and direction are explicit.
- Authored, assisted, and inferred reasoning are distinguishable.
- Boundary violations explain the owning rule and allowed alternative.
- Lifecycle risk findings show their rule basis.
- Reasoning diagnostics are semantically grouped and actionable.

## Milestone 7: Continuity and Operational Context Transparency

### Objective

Make operational context explain why information was retained, removed, compressed, assimilated, rejected, modified, contradicted, resolved, or lost.

### Backend

1. Extend decision assimilation analysis to expose, for every analyzed decision:
   - taxonomy
   - assimilated or excluded
   - exclusion reason
   - durability
   - resulting operational statement
   - evidence
2. Expose taxonomy classification basis:
   - matched evidence
   - matched rules
   - heuristic/fallback status
   - diagnostics
3. Expose assimilation limits:
   - total qualifying items
   - assimilated items
   - omitted items
   - limit
   - reason
4. Expose consequences with originating decision, reasoning, and operational impact.
5. Surface every detected contradiction with decision A, decision B, conflict type, evidence, severity, and resolution guidance.
6. Extend operational evolution reporting:
   - added
   - modified
   - removed
   - preserved
   - lost
   - resolved
   - previous state
   - current state
   - reason
   - evidence
7. Extend compression output:
   - retained
   - compressed
   - removed
   - merged
   - noise removed
   - duplicate removed
   - transient removed
   - rule
   - evidence
   - threshold
8. Improve `UnderstandingDiffService` to detect modifications rather than remove/add pairs when item identity, source reference, section, or stable lineage indicates continuity.
9. Add or update semantic change types for modified architecture, modified constraint, modified workflow, modified decision, modified understanding, lost understanding, resolved understanding, duplicate removed, and transient removed.
10. Normalize continuity diagnostics by category:
    - assimilation
    - compression
    - evolution
    - diff
    - recovery
    - classification
    - contradictions
    - lost understanding
    - resolved understanding

### UI

1. Extend operational-context and continuity TypeScript types.
2. Add panels:
   - `OperationalContextAssimilationPanel`
   - `OperationalContextTaxonomyPanel`
   - `OperationalContextAssimilationLimitPanel`
   - `OperationalContextConsequencePanel`
   - `OperationalContextContradictionPanel`
   - `OperationalContextEvolutionTimeline`
   - `OperationalContextCompressionExplanation`
   - `ContinuityDiagnosticsGroupedPanel`
3. Update `OperationalContextProposalComparison` and `OperationalContextSemanticChangeList` to display modifications as modifications, not separate remove/add entries.
4. Show omitted assimilation items and silent truncation as visible facts.
5. Show compression warnings with specific item reasons and evidence.

### Tests

- Backend tests for assimilation inclusion/exclusion reasons.
- Backend tests for taxonomy basis and heuristic fallback.
- Backend tests for assimilation limits and omitted items.
- Backend tests for all contradiction detection.
- Backend tests for identity-aware semantic diff modifications.
- Backend tests for compression reason categories.
- UI tests for the new panels and modification rendering.

### Exit Criteria

- Every analyzed decision explains why it was assimilated or rejected.
- Taxonomy classifications expose their basis.
- Assimilation limits and omitted items are visible.
- Consequences stay linked to originating decisions.
- All contradictions are explorable.
- Operational evolution distinguishes added, modified, removed, preserved, lost, and resolved understanding.
- Compression explains item-level outcomes.
- Semantic diff preserves identity and lineage for modifications.

## Milestone 8: Unified Explainability Layer

### Objective

Create one shared rendering language for explanations across workflow, decision sessions, decisions, execution, reasoning, continuity, health, diagnostics, and certification. This milestone consumes authoritative explanation projections produced by earlier milestones and replaces domain-specific rendering duplication. It does not create new domain explanations.

### Model

Add `src/CommandCenter.UI/src/types/explainability.ts` with presentation-only concepts:

- `Explanation`
- `ExplanationEvidence`
- `ExplanationConstraint`
- `ExplanationAlternative`
- `ExplanationAssumption`
- `ExplanationDiagnostic`
- `ExplanationUncertainty`
- `ExplanationRecommendation`
- `ExplanationAction`
- `ExplanationHealthDimension`

These types organize already-authoritative domain projection data. They do not normalize backend domains into one generic authority.

Milestone 4 and related transparency milestones own domain explanation projections. Milestone 8 owns shared presentation components and adapters only.

### Components

Add shared components under `src/CommandCenter.UI/src/components/explainability/`:

- `EvidenceList`
- `DecisionBasis`
- `ConstraintViewer`
- `AlternativeExplorer`
- `UncertaintyView`
- `HealthView`
- `DiagnosticList`
- `ActionEligibilityView`
- `CertificationFindingsView`

Each component must render:

- what happened
- why
- evidence
- alternatives
- constraints
- uncertainty
- next action

### Integration

1. Add adapter functions under `src/CommandCenter.UI/src/lib/explainability/` for each domain. Adapters map authoritative domain fields into presentation concepts without computing domain outcomes.
2. Adapters may reorganize authoritative information, but they must not omit semantically relevant evidence, constraints, uncertainty, diagnostics, findings, or eligible actions.
3. Replace domain-specific explanation widgets in:
   - decisions
   - workflow
   - decision sessions
   - execution
   - reasoning
   - operational context
   - health
   - diagnostics
   - certification
4. Keep domain-specific detail panels where needed, but render evidence, constraints, alternatives, uncertainty, health, diagnostics, and certification findings through shared components.
5. Keep visual language consistent across status badges, diagnostics, warnings, findings, evidence references, and action eligibility.

### Tests

- Component tests for all shared explainability components.
- Adapter tests proving adapters do not compute domain scores, decisions, lifecycle state, or eligibility.
- Adapter tests proving semantically relevant evidence and diagnostics are preserved when mapping into presentation concepts.
- UI characterization tests proving major domains use shared explainability components.

### Exit Criteria

- Evidence, constraints, alternatives, uncertainty, health, diagnostics, action eligibility, and certification findings render consistently across the app.
- Explanations are composed from authoritative projections.
- No second domain authority is introduced.
- Users encounter the same explanation model across every major workspace.

## Milestone 9: Product Cohesion

### Objective

Make the application feel unified, not merely smaller. Remove fragmentation so every semantic concept has one authority, one projection, one primary navigation path, and one primary presentation.

### Implementation

1. Audit navigation for workflow, decision sessions, decisions, execution, reasoning, operational context, repository, health, diagnostics, and certification.
2. Define one primary home and allowed contextual links for each capability.
3. Consolidate duplicate workflow displays, governance summaries, execution monitoring views, reasoning confidence displays, continuity evolution summaries, health widgets, and certification summaries.
4. Review backend endpoints and classify each as `Keep`, `Redirect`, `Internal`, or `Remove`.
5. Review projections and classify each as authoritative, derived consumer, compatibility, or retire.
6. Review frontend state and classify each state value as authoritative view state, derived display state, disposable UI state, or duplicate domain state.
7. Normalize interaction patterns for review, accept, reject, transfer, recover, generate, refine, commit, push, promote, archive, and supersede:
   - action
   - eligibility
   - evidence
   - result
   - diagnostics
8. Build or update a unified operational dashboard that summarizes:
   - workflow
   - governance
   - execution
   - operational context
   - reasoning
   - repository
   - health
   - certification
   - diagnostics
9. Delete obsolete UI components, old workflow derivation, duplicate panels, temporary views, deprecated widgets, obsolete summaries, and unused client functions after replacements are tested.
10. Align terminology across statuses, health, diagnostics, recovery, certification, governance, execution, and explainability.

### Likely Cleanup Targets

- `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after workflow projection integration.
- Any rail or status component that still consumes `RepositoryExecutionState` as a workflow source.
- Duplicate decision recommendation, quality, governance, and influence summaries replaced by explainability components.
- Duplicate health renderers replaced by shared `HealthView`.
- Duplicate diagnostics renderers replaced by shared `DiagnosticList`.

### Tests

- Navigation characterization tests.
- UI tests proving primary surfaces remain reachable.
- Static or unit tests for removed duplicate helpers where practical.
- Backend endpoint disposition tests for retained routes.

### Exit Criteria

- Every major capability has one obvious primary navigation path.
- Every semantic concept has one authoritative projection and one primary presentation.
- Duplicate endpoints, projections, views, and components are removed or intentionally retained with documented purpose.
- Interaction patterns are consistent across the product.
- The dashboard gives a coherent overview without replacing detailed workspaces.

## Milestone 10: MVP Closure and Release Readiness

### Objective

Prove the MVP is coherent, authoritative, explainable, operationally complete, and releasable. This milestone adds no new product capability.

### Audits

1. Capability closure audit:
   - Workflow
   - Decision Sessions
   - Decisions
   - Execution
   - Reasoning
   - Operational Context
   - Explainability
   - Repository
   - Dashboard
   - Certification
2. Authority audit:
   - UI does not own domain state.
   - Workflow does not mutate decision-session lifecycle.
   - Certification does not repair state.
   - Explainability does not compute domain outcomes.
   - Repository summaries do not redefine lifecycle.
3. Semantic transparency audit:
   - what happened
   - why
   - evidence
   - alternatives
   - constraints
   - uncertainty
   - next action
4. Reachability audit:
   - endpoints
   - Tauri commands
   - UI controls
   - hosted services
   - background recovery
   - workflow
   - decision sessions
   - execution
   - repository projections
5. Explainability validation:
   - decision
   - workflow
   - execution
   - reasoning
   - continuity
   - decision sessions
   - health
   - certification
   - diagnostics
6. Integration verification:
   - workflow consumes governance
   - execution consumes decisions
   - reasoning captures lifecycle events
   - operational context assimilates stable decisions
   - repository projections summarize authoritative domains
   - dashboard composes projections
   - explainability renders projection facts
   - certification reports visibility
7. Product cohesion review:
   - navigation
   - interaction
   - terminology
   - status
   - health
   - diagnostics
   - errors
   - recovery
   - certification
   - governance
   - execution
8. Release cleanup:
   - temporary diagnostics
   - compatibility code
   - deprecated endpoints
   - legacy components
   - temporary feature flags
   - unused documentation
   - obsolete UI helpers
9. Architectural drift review:
   - no duplicate authority introduced
   - no client-side heuristics introduced
   - no parallel lifecycle created
   - no projection became authoritative

### Verification Commands

Run the full verification set before declaring MVP complete:

```text
dotnet test CommandCenter.slnx
cd src/CommandCenter.UI && npm run lint
cd src/CommandCenter.UI && npm run test
cd src/CommandCenter.UI && npm run build
cd src/CommandCenter.UI && npm run test:e2e
```

If a command is not runnable in the local environment, document the exact blocker and the nearest completed substitute verification.

### Deliverables

- Capability closure report.
- Final authority verification.
- Final semantic transparency verification.
- Final reachability verification.
- Final explainability verification.
- Final integration verification.
- Product cohesion review.
- Architectural drift review.
- Release verification report.
- Repository cleanup report.
- MVP certification report.
- Release readiness checklist.

### Exit Criteria

- Every Core MVP capability is implemented, integrated, visible, reachable, tested, and intentional.
- Authority boundaries remain intact.
- Architectural drift review confirms no duplicate authority, client-side heuristic, parallel lifecycle, or authoritative projection was introduced.
- No critical semantic opacity remains.
- No unintended orphaned Core MVP capability remains.
- Explanations are consistent across workflow, governance, execution, reasoning, continuity, health, diagnostics, and certification.
- All major subsystems participate in a unified operational experience.
- Full automated verification passes or has explicit documented blockers.
- Transitional code and obsolete release artifacts are removed or intentionally retained.
- The final certification report declares the MVP complete only when every Core MVP exit criterion is satisfied.

## Cross-Milestone Test Matrix

Use this matrix to keep test coverage proportional to risk:

- Backend services: test domain projection, command, recovery, eligibility, diagnostics, and certification logic directly.
- Backend endpoints: test every new route and every changed error/response shape.
- Shell commands: test command naming and backend error preservation where feasible.
- TypeScript clients: test request argument shape and error mapping where practical.
- Hooks: test refresh behavior, mutation flow, and stale state cleanup.
- UI components: characterization tests for visible semantic facts, blocked action reasons, diagnostics, and certification findings.
- E2E: cover one repository path through workflow, decision lifecycle, execution lifecycle, operational-context review, governance transfer readiness, and dashboard summary.

## Definition of Done

A milestone is done only when:

- It uses existing domain authority or adds a narrow authority-owned projection.
- All new product actions are wired from backend to shell to TypeScript client to UI.
- All new explanations render authoritative fields.
- Duplicate client-side derivation introduced by earlier implementation has been removed or has a documented retirement milestone.
- Tests cover the new route, projection, command, client, and visible UI behavior.
- The application remains buildable.
- Any deferred or internal capability is explicitly classified.
