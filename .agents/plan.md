# Governed Workflow Orchestration Implementation Plan

## Objective

Implement Governed Workflow Orchestration as a first-class Command Center capability that coordinates repository work across existing domains while preserving human authority and domain ownership.

The target lifecycle is:

```text
Human work selection
        |
Execution
        |
Handoff review
        |
Decision discovery and resolution
        |
Operational context review and promotion
        |
Commit review
        |
Push review
        |
Completed repository work cycle
```

The system coordinates mechanical progression, recovery, state observation, diagnostics, history, influence tracing, health reporting, and certification. Humans continue to select work, review handoffs, resolve decisions, approve or reject context, approve commits, approve pushes, and choose the next work target.

The implementation is complete when Command Center can observe the complete repository workflow, explain the current stage and blocking gate, recover workflow evidence after restart, advance mechanically between non-authority stages, stop at every human authority boundary, and prove those behaviors with certification evidence.

## Current Codebase Baseline

Command Center currently has:

- .NET backend sidecar in `src/CommandCenter.Backend`.
- React/TypeScript UI in `src/CommandCenter.UI`.
- Rust/Tauri shell in `src/CommandCenter.Shell`.
- Backend tests in `tests/CommandCenter.Backend.Tests`.
- Shared repository, artifact, configuration, planning, and projection infrastructure in `src/CommandCenter.Core`.
- Execution context, provider launch, monitoring, handoff, commit, and push services in `src/CommandCenter.Execution`.
- Operational-context parsing, proposal, review, promotion, diagnostics, and reporting in `src/CommandCenter.Continuity` and `src/CommandCenter.Middle`.
- Structured decision lifecycle, generation, review, refinement, resolution, governance, quality, execution projection, and certification in `src/CommandCenter.Decisions`.
- Reasoning event, thread, relationship, graph, reconstruction, materialization review, and certification in `src/CommandCenter.Reasoning`.
- Repository workspace projections in `RepositoryProjectionService`.
- Backend route groups for repositories, artifacts, planning, operational context, continuity, execution, execution sessions, git, decisions, and reasoning.
- UI primary tabs for workspace, execution, operational context, decisions, reasoning, and continuity.

Primary gaps:

- No first-class workflow coordination project.
- No repository workflow projection spanning execution, handoff, decisions, continuity, commit, and push.
- No cross-domain workflow stage model.
- No explicit transition graph for repository workflow progression.
- No unified workflow gate catalog that maps human authority boundaries to existing commands.
- No workflow timeline, workflow history, gate history, continuation history, or recovery history.
- No workflow fingerprinting or recovery diagnostics.
- No workflow-facing projections for execution, handoff, decisions, operational context, or git.
- No mechanical continuation service that advances only between non-authority stages.
- No workflow health, quality, progression, governance participation, or readiness reports.
- No workflow certification that proves recovery, authority preservation, gate halting, and end-to-end progression.

## Authority Model

Workflow is a coordination layer. It is not a competing authority domain.

Existing authority remains unchanged:

- Execution owns execution state, provider sessions, execution events, handoff production, handoff acceptance state, commit preparation, commit execution, and push execution.
- Decisions own candidates, proposals, packages, refinements, review, resolution, governance analysis, quality, certification, and execution decision projection.
- Continuity owns operational-context proposals, review, edit, rejection, acceptance, promotion, and `.agents/operational_context.md`.
- Git support owns repository status, commit preparation, commit execution, and push execution.
- Reasoning owns explanatory event history and reconstruction.
- Repository files under `.agents` remain durable authority. Runtime memory is a cache.

Workflow may:

- observe domain state
- project workflow state
- persist derived workflow evidence
- recover workflow projections from domain artifacts
- evaluate transitions
- identify gates
- identify required human actions
- invoke existing non-authority preparation commands after explicit work has been selected
- stop at authority gates
- explain and certify behavior

Workflow may not:

- select repositories, milestones, tasks, goals, or priorities
- launch execution without explicit human work selection
- accept or reject handoffs
- resolve, archive, supersede, or approve decisions
- accept, edit, reject, or promote operational context
- approve or execute commits
- approve or execute pushes
- mutate domain state directly
- use provider APIs directly
- become the source of truth for domain lifecycle state

If workflow evidence conflicts with domain artifacts, the domain artifacts win and workflow rebuilds its view.

## Architecture Rules

1. Workflow coordinates; domains decide.
   Workflow may determine that a stage is complete, identify the next stage, and record why progression stopped or advanced. Domain services remain the only owners of domain lifecycle state.

2. Workflow evidence is derived, reconstructable, and disposable.
   Workflow timelines, histories, gates, recovery records, continuation events, preparation events, reports, and certification artifacts are useful audit evidence. They are never required for domain correctness. If deleted, stale, or corrupt, they must be rebuildable from execution, decision, continuity, and git evidence.

3. Domain artifacts win every conflict.
   When workflow evidence disagrees with authoritative domain artifacts, recovery discards or repairs the workflow view and records diagnostics.

4. Workflow may call existing domain commands.
   Workflow may invoke existing public domain service methods or backend commands only where this plan explicitly permits a non-authority preparation action.

5. Workflow may not create parallel domain commands.
   Workflow must not introduce alternate commands for accepting handoffs, resolving decisions, reviewing context, promoting context, committing, pushing, or selecting work. It may route users to existing commands and may call existing non-authority preparation commands when allowed.

6. Progression and preparation are separate.
   Progression changes workflow stage and records coordination evidence. Preparation asks an existing domain to create reviewable artifacts, such as candidates, proposals, context proposals, or commit preparations. Preparation may make later human review possible, but it is not stage progression and it never satisfies an authority gate.

## Target Solution Structure

Add a dedicated backend project:

```text
src/
  CommandCenter.Workflow/
    Abstractions/
    Extensions/
    Models/
    Persistence/
    Primitives/
    Projections/
    Services/
```

Project references:

- `CommandCenter.Workflow` references `CommandCenter.Core`, `CommandCenter.Execution`, `CommandCenter.Decisions`, and `CommandCenter.Continuity`.
- `CommandCenter.Workflow` should not reference `CommandCenter.Middle`.
- `CommandCenter.Backend` references `CommandCenter.Workflow` and maps workflow endpoints.
- `CommandCenter.Middle` may reference `CommandCenter.Workflow` for read-only dashboard and workspace workflow summaries.
- Existing domain projects should not reference `CommandCenter.Workflow`.

Add `src/CommandCenter.Workflow/CommandCenter.Workflow.csproj` to `CommandCenter.slnx`.

Register services through:

```text
src/CommandCenter.Workflow/Extensions/ServiceCollectionExtensions.cs
```

and call `builder.Services.AddWorkflow()` from `src/CommandCenter.Backend/Program.cs`.

Add:

```text
src/CommandCenter.Backend/Endpoints/WorkflowEndpoints.cs
```

and map it from `Program.cs`.

## Target Repository Layout

Persist workflow evidence under `.agents/workflow`:

```text
.agents/
  workflow/
    timelines/
      workflow.<timestamp>.json
      workflow.<timestamp>.md
    gates/
      gates.<timestamp>.json
      gates.<timestamp>.md
    recovery/
      recovery.<timestamp>.json
      recovery.<timestamp>.md
    continuation/
      continuation.<timestamp>.json
      continuation.<timestamp>.md
    preparation/
      preparation.<timestamp>.json
      preparation.<timestamp>.md
    reports/
      repository.<timestamp>.json
      repository.<timestamp>.md
      progression.<timestamp>.json
      progression.<timestamp>.md
      human-governance.<timestamp>.json
      human-governance.<timestamp>.md
      readiness.<timestamp>.json
      readiness.<timestamp>.md
      certification.<timestamp>.json
      certification.<timestamp>.md
```

Rules:

- JSON records are structured workflow evidence.
- Markdown records are deterministic human-readable projections.
- Workflow artifacts are derived evidence, not domain authority.
- Workflow timeline and history artifacts are reconstructable and disposable; they must never be required to decide whether execution, decisions, continuity, commit, or push are correct.
- Persist only evidence that helps explain, recover, audit, or certify workflow behavior. Do not persist duplicate copies of full domain state.
- IDs and report names are repository-scoped and timestamped with sortable UTC timestamps.
- Every structured artifact carries `schemaVersion`, `repositoryId`, `createdAt` or `generatedAt`, payload, and workflow fingerprint where applicable.
- Storage uses `IArtifactStore` and `ArtifactPath.ResolveRepositoryPath`.
- Persistence rejects absolute paths, path traversal, repository escapes, unsupported schema versions, and repository ownership mismatches.
- Recovery can rebuild workflow evidence from execution, decision, continuity, and git artifacts if workflow artifacts are missing, stale, or corrupt.

## Core Domain Model

Add primitives and models under `CommandCenter.Workflow`:

```text
WorkflowStage
WorkflowGateType
WorkflowGateStatus
WorkflowProgressState
WorkflowInstance
WorkflowTimeline
WorkflowTimelineEntry
WorkflowTimelineEventType
WorkflowTransition
WorkflowTransitionResult
WorkflowBlockingCondition
WorkflowGate
WorkflowGateEvidence
WorkflowGateResolution
WorkflowProjectionDiagnostics
WorkflowStateMachineDiagnostics
WorkflowGateDiagnostics
WorkflowRecoveryDiagnostics
WorkflowFingerprint
WorkflowHistoryProjection
WorkflowExecutionProjection
WorkflowExecutionStatus
WorkflowExecutionFailure
WorkflowExecutionDiagnostics
WorkflowHandoffProjection
WorkflowHandoffStatus
WorkflowHandoffValidation
WorkflowHandoffDiagnostics
WorkflowDecisionProjection
WorkflowDecisionStatus
WorkflowDecisionDiagnostics
WorkflowOperationalContextProjection
WorkflowOperationalContextStatus
WorkflowOperationalContextDiagnostics
WorkflowGitProjection
WorkflowGitStatus
WorkflowGitDiagnostics
WorkflowCompletionEvaluation
WorkflowContinuationEvaluation
WorkflowContinuationEvent
WorkflowContinuationDiagnostics
WorkflowPreparationEvaluation
WorkflowPreparationCommand
WorkflowPreparationEvent
WorkflowPreparationDiagnostics
WorkflowInfluenceTrace
WorkflowHealthAssessment
WorkflowCertificationResult
WorkflowCertificationFinding
RepositoryWorkflowReport
WorkflowProgressionReport
HumanGovernanceReport
WorkflowReadinessReport
```

Initial `WorkflowStage` values:

```text
Unknown
WorkSelection
Execution
Handoff
Decision
OperationalContext
Commit
Push
Completed
Blocked
Failed
```

Initial `WorkflowGateType` values:

```text
None
WorkSelection
ExecutionAcceptance
DecisionResolution
OperationalContextReview
OperationalContextPromotion
CommitApproval
PushApproval
```

Do not name the gate abstraction `GovernanceGate`. Decision governance already means advisory decision health analysis. Use `WorkflowGate`.

Initial `WorkflowProgressState` values:

```text
Ready
Active
AwaitingGate
Blocked
Completed
Failed
Recovering
WaitingForHuman
```

Initial `WorkflowBlockingCondition` values:

```text
MissingWorkSelection
MissingExecution
ExecutionRunning
ExecutionFailure
ExecutionCancelled
MissingHandoff
InvalidHandoff
PendingHandoffAcceptance
RejectedHandoff
MissingDecision
UnresolvedDecision
DecisionGovernanceBlock
PendingContextReview
PendingContextPromotion
PendingCommitApproval
PendingPushApproval
UnknownState
ConflictingEvidence
RecoveryConflict
```

## Service Contracts

Add service contracts under `CommandCenter.Workflow.Abstractions`:

```text
IWorkflowProjectionService
IWorkflowStateMachineService
IWorkflowRepository
IWorkflowRecoveryService
IWorkflowGateCatalogService
IWorkflowExecutionService
IWorkflowHandoffService
IWorkflowDecisionService
IWorkflowOperationalContextService
IWorkflowGitService
IWorkflowContinuationService
IWorkflowPreparationService
IWorkflowHealthService
IWorkflowCertificationService
IWorkflowReportService
```

Service boundaries:

- Projection services are read-only and deterministic.
- State-machine services evaluate transitions only.
- Gate catalog services discover, explain, and map gates but never execute authority.
- Recovery services rebuild workflow evidence from domains before trusting workflow artifacts.
- Continuation services are the only workflow services allowed to coordinate cross-domain progression, and only after the continuation milestone is implemented.
- Preparation services are the only workflow services allowed to request reviewable artifact creation from existing domain commands, and only after the continuation milestone defines the allowed preparation matrix.
- Certification services observe, evaluate, diagnose, and report. They never mutate workflow or domains.

## Backend API Surface

Add repository-scoped endpoints:

```text
GET  /api/repositories/{repositoryId}/workflow
GET  /api/repositories/{repositoryId}/workflow/diagnostics
GET  /api/repositories/{repositoryId}/workflow/timeline
GET  /api/repositories/{repositoryId}/workflow/history
GET  /api/repositories/{repositoryId}/workflow/transitions
GET  /api/repositories/{repositoryId}/workflow/gates
GET  /api/repositories/{repositoryId}/workflow/gates/history
GET  /api/repositories/{repositoryId}/workflow/recovery
POST /api/repositories/{repositoryId}/workflow/recover
GET  /api/repositories/{repositoryId}/workflow/execution
GET  /api/repositories/{repositoryId}/workflow/handoff
GET  /api/repositories/{repositoryId}/workflow/decisions
GET  /api/repositories/{repositoryId}/workflow/operational-context
GET  /api/repositories/{repositoryId}/workflow/git
GET  /api/repositories/{repositoryId}/workflow/continuation/evaluation
POST /api/repositories/{repositoryId}/workflow/continuation/run
GET  /api/repositories/{repositoryId}/workflow/preparation/evaluation
POST /api/repositories/{repositoryId}/workflow/preparation/run
GET  /api/repositories/{repositoryId}/workflow/preparation/history
GET  /api/repositories/{repositoryId}/workflow/health
GET  /api/repositories/{repositoryId}/workflow/reports/repository
GET  /api/repositories/{repositoryId}/workflow/reports/progression
GET  /api/repositories/{repositoryId}/workflow/reports/human-governance
GET  /api/repositories/{repositoryId}/workflow/reports/readiness
GET  /api/repositories/{repositoryId}/workflow/certification
POST /api/repositories/{repositoryId}/workflow/certification
GET  /api/repositories/{repositoryId}/workflow/certification/reports
```

Endpoint behavior:

- `400 BadRequest` for invalid IDs, invalid query parameters, unsafe paths, unsupported enums, invalid payloads, or validation failures.
- `404 NotFound` for missing repository or missing workflow report.
- `409 Conflict` for stale continuation requests, recovery conflicts, broken required evidence, invalid workflow transitions, or authority boundary violations.
- `200 OK` for successful reads and commands that return updated projections.
- Error bodies follow the existing `{ error = "..." }` convention.

## Tauri Bridge Updates

Add Rust bridge commands in `src/CommandCenter.Shell/src/main.rs`:

```text
get_workflow
get_workflow_diagnostics
get_workflow_timeline
get_workflow_history
get_workflow_transitions
get_workflow_gates
get_workflow_gate_history
get_workflow_recovery
recover_workflow
get_workflow_execution
get_workflow_handoff
get_workflow_decisions
get_workflow_operational_context
get_workflow_git
get_workflow_continuation_evaluation
run_workflow_continuation
get_workflow_preparation_evaluation
run_workflow_preparation
get_workflow_preparation_history
get_workflow_health
get_repository_workflow_report
get_workflow_progression_report
get_human_governance_report
get_workflow_readiness_report
get_workflow_certification
run_workflow_certification
list_workflow_certification_reports
```

Each command calls the backend HTTP endpoint, deserializes typed responses where practical, and uses the existing response-error path for non-success responses.

## UI Plan

Add workflow types, API calls, hooks, and a dedicated workflow workspace.

Update:

```text
src/CommandCenter.UI/src/state/shellState.ts
src/CommandCenter.UI/src/components/shell/WorkspaceTabs.tsx
src/CommandCenter.UI/src/components/shell/CommandPalette.tsx
src/CommandCenter.UI/src/lib/navigation.ts
src/CommandCenter.UI/src/App.tsx
src/CommandCenter.UI/src/devTauriMock.ts
src/CommandCenter.UI/src/types/index.ts
src/CommandCenter.UI/src/hooks/index.ts
```

Add:

```text
src/CommandCenter.UI/src/types/workflow.ts
src/CommandCenter.UI/src/api/workflow.ts
src/CommandCenter.UI/src/hooks/useWorkflowProjection.ts
src/CommandCenter.UI/src/hooks/useWorkflowTimeline.ts
src/CommandCenter.UI/src/hooks/useWorkflowGates.ts
src/CommandCenter.UI/src/hooks/useWorkflowRecovery.ts
src/CommandCenter.UI/src/hooks/useWorkflowContinuation.ts
src/CommandCenter.UI/src/hooks/useWorkflowReports.ts
src/CommandCenter.UI/src/hooks/useWorkflowCertification.ts
src/CommandCenter.UI/src/features/workflow/WorkflowTab.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowStagePanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowTimelinePanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowGatePanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowTransitionPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowDiagnosticsPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowRecoveryPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowContinuationPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowPreparationPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowHealthPanel.tsx
src/CommandCenter.UI/src/features/workflow/WorkflowCertificationPanel.tsx
src/CommandCenter.UI/src/test/characterization/workflow*.test.tsx
```

UI rules:

- Show workflow as coordination and explanation, not as domain authority.
- Keep current stage, next possible stages, and blocking gate visible near the top of the workflow tab.
- Show required human actions as commands the user must perform, not as workflow-owned approvals.
- Show gate evidence beside the gate it satisfies.
- Show continuation events separately from domain lifecycle events.
- Show preparation events separately from continuation events and domain lifecycle events.
- Disable continuation controls when any authority gate is open.
- Disable preparation controls when preparation would bypass a gate or duplicate an existing review artifact.
- Show diagnostics for blocked, failed, recovered, and unknown states.
- Keep workflow reports and certification evidence audit-oriented.
- Do not infer workflow authority or domain lifecycle transitions in React state.

## Workspace Projection Integration

Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection` with a workflow summary supplied by `IWorkflowProjectionService`.

Suggested summary:

```text
RepositoryWorkflowSummary
  CurrentStage
  PreviousStage
  ProgressState
  BlockingGate
  RequiredHumanAction
  OpenGateCount
  SatisfiedGateCount
  TimelineEntryCount
  ContinuationEventCount
  PreparationEventCount
  RecoveryEventCount
  LastProgressedAt
  LastGateSatisfiedAt
  LastRecoveredAt
  LastCertificationAt
  CertificationPassed
```

Keep these projections read-only and backend-owned.

## Command Dispatch Rules

Workflow has two separate kinds of mechanical work.

Workflow progression:

- evaluates current domain evidence
- selects the workflow stage that follows from that evidence
- records continuation events
- records timeline entries
- opens or closes workflow gates based on domain evidence
- never creates domain review artifacts
- never satisfies an authority gate

Workflow preparation:

- requests an existing domain command to create a reviewable artifact
- records preparation events
- records why the artifact was needed
- records which existing command was used
- never changes workflow stage by itself
- never satisfies an authority gate

Allowed progression actions after explicit work selection:

- observe execution state
- observe handoff state
- observe decision state
- observe operational-context state
- observe git state
- move the projected workflow stage when domain evidence proves the stage is complete
- persist workflow timeline, gate, recovery, continuation, health, report, and certification evidence
- open work selection gate after a completed work cycle

Allowed preparation actions after explicit work selection:

- call existing Decisions discovery/generation commands to create reviewable candidates or proposals when the Decisions domain allows it and no decision resolution gate is open
- call existing Continuity commands to create or link reviewable operational-context proposals when the Continuity domain allows it and no context review or promotion gate is open
- call existing Execution commit-preparation command when execution is awaiting commit and no commit approval has been satisfied
- persist workflow preparation evidence that records the command, source stage, input fingerprint, created artifact, and diagnostics

Always forbidden:

- selecting work
- starting execution without an explicit user request
- accepting or rejecting handoffs
- resolving, archiving, or superseding decisions
- accepting, editing, rejecting, or promoting operational context
- committing
- pushing
- bypassing an open gate
- direct writes into another domain artifact layout
- adding workflow-specific replacement commands for domain authority actions
- making artifact preparation imply stage progression

Continuation and preparation must both be idempotent. Re-running after restart must not duplicate proposals, context proposals, commit preparations, timeline entries, continuation events, or preparation events.

## Milestone 0: Workflow Coordination Foundation

Objective: create the workflow vocabulary and read-only projection substrate.

Deliver:

- `WorkflowStage`, `WorkflowGateType`, `WorkflowProgressState`, `WorkflowInstance`, and `WorkflowTimelineEntry`.
- `IWorkflowProjectionService`.
- explicit deterministic stage derivation from execution, decision, continuity, and git state.
- `WorkflowProjectionDiagnostics` with projection inputs, chosen stage, chosen gate, reasoning, unknown states, and conflicts.
- derived workflow timeline entries for execution started/completed, decision resolved, context promoted, commit executed, and push executed where evidence exists.
- repository integration methods and endpoints for workflow, diagnostics, and timeline.

Rules:

- No workflow state machine.
- No persistence beyond optional transient projection output.
- No recovery.
- No automation.
- No cross-domain mutation.

Tests:

- execution states map to workflow stages and gates.
- decision states map to workflow stages and gates.
- continuity states map to workflow stages and gates.
- git states map to workflow stages and gates.
- identical inputs produce identical projections.
- diagnostics explain every projection.
- workflow projection does not call mutating domain methods.

Exit criteria:

- workflow contracts exist.
- workflow projection exists.
- projection diagnostics exist.
- derived timeline exists.
- authority preservation tests pass.

## Milestone 1: Workflow State Machine

Objective: make workflow navigable without advancing it.

Deliver:

- `WorkflowTransition`.
- `WorkflowTransitionResult`.
- `IWorkflowStateMachineService`.
- canonical graph:

```text
WorkSelection -> Execution -> Handoff -> Decision -> OperationalContext -> Commit -> Push -> Completed
```

- `WorkflowGateResolution`.
- `WorkflowBlockingCondition`.
- `WorkflowStateMachineDiagnostics`.
- projection integration for valid transitions, blocked transitions, next possible stages, and blocking gates.

Rules:

- The state machine evaluates what can happen next.
- The state machine does not mutate domain state.
- The state machine does not persist authoritative workflow state.
- The state machine does not automate progression.

Tests:

- valid transitions pass.
- invalid transitions fail.
- unresolved decisions block decision-to-context transition.
- pending context review or promotion blocks context-to-commit transition.
- pending commit approval blocks commit-to-push transition.
- pending push approval blocks push-to-completed transition.
- every rejected transition explains itself.
- state-machine services never call mutating domain methods.

Exit criteria:

- workflow graph exists.
- transition validation exists.
- blocking model exists.
- gate requirements are modeled.
- diagnostics are operational.

## Milestone 2: Workflow Persistence and Recovery

Objective: make workflow evidence durable and recoverable without making it authoritative.

Deliver:

- `WorkflowTimeline` with repository id, current stage, previous stage, entries, generated timestamp, and fingerprint.
- `WorkflowTimelineEntry` with timestamp, stage, event type, reason, source domain, source artifact, and fingerprint.
- `IWorkflowRepository` with save/load/list/latest timeline operations plus report persistence.
- `IWorkflowRecoveryService` with rebuild timeline, recover current workflow, and validate recovered workflow operations.
- `WorkflowFingerprint` that includes current stage, timeline count, last timeline entry, blocking conditions, and gate state.
- `WorkflowRecoveryDiagnostics`.
- `WorkflowHistoryProjection` with timeline, gate history, progress summary, and recovery summary.
- `WorkflowRecoveryHostedService` that runs on application startup only.

Recovery rules:

- If workflow artifacts exist, load and validate them against domain evidence.
- If workflow artifacts are missing, rebuild from domain evidence.
- If workflow artifacts conflict with domain evidence, discard workflow view and rebuild.
- If workflow artifacts are partially corrupt, recover only what can be proven and record diagnostics.
- Domain artifacts always win.

Tests:

- timeline saves, loads, and lists.
- latest timeline lookup works.
- missing workflow artifacts rebuild from domains.
- corrupt workflow artifacts rebuild from domains.
- fingerprints are stable for identical evidence.
- fingerprints detect divergence.
- restart recovery restores workflow evidence.
- recovery never mutates execution, decisions, continuity, or git.

Exit criteria:

- workflow timeline persists.
- recovery works.
- rebuild works.
- fingerprinting works.
- recovery diagnostics exist.
- hosted startup recovery works.

## Milestone 3: Workflow Gate Catalog

Objective: unify human authority checkpoints as workflow gates.

Deliver:

- `WorkflowGate` with gate id, type, repository id, stage, status, required action, satisfying command, source domain, source artifact, created timestamp, satisfied timestamp, satisfied actor, and reason.
- `WorkflowGateStatus` with open, satisfied, rejected, bypassed, expired, and unknown.
- `WorkflowGateEvidence`.
- `IWorkflowGateCatalogService`.
- deterministic gate-to-command map:

```text
ExecutionAcceptance -> accept_execution_handoff or reject_execution_handoff
DecisionResolution -> resolve_decision
OperationalContextReview -> accept/edit/reject_context_proposal
OperationalContextPromotion -> promote_operational_context
CommitApproval -> commit_execution
PushApproval -> push_execution
WorkSelection -> explicit human repository and work target selection
```

- projection integration for open gates, satisfied gates, blocking gate, gate history, current gate reason, and required human action.
- `WorkflowGateDiagnostics`.
- gate history markdown projection.

Gate rules:

- Execution acceptance opens when execution completed and a handoff awaits acceptance.
- Decision resolution opens when a proposal is ready for resolution.
- Operational context review opens when an unreviewed context proposal exists.
- Operational context promotion opens when a proposal was accepted or edited and is not promoted.
- Commit approval opens when execution is awaiting commit.
- Push approval opens when execution is awaiting push.
- Work selection opens when a work cycle is completed and no explicit next work target has been selected.

Tests:

- each gate opens from the correct domain evidence.
- each gate satisfies only from domain evidence.
- every gate maps to an existing command name.
- gate catalog never executes commands.
- diagnostics explain why blocked, what action is required, and what evidence would satisfy the gate.

Exit criteria:

- workflow gate model exists.
- gate catalog service exists.
- gate satisfaction evidence exists.
- gate-to-command map exists.
- workflow projection includes gates.
- gate diagnostics exist.

## Milestone 4: Execution Workflow Integration

Objective: make workflow execution-aware while keeping Execution authoritative.

Deliver:

- `WorkflowExecutionProjection` with execution id, repository id, status, started timestamp, completed timestamp, failed timestamp, handoff presence, changes presence, and failure reason.
- `WorkflowExecutionStatus` with not started, running, completed, failed, cancelled, and awaiting acceptance.
- `IWorkflowExecutionService`.
- completion rules for running, completed, awaiting acceptance, failed, and cancelled.
- `WorkflowExecutionFailure`.
- `WorkflowExecutionDiagnostics`.
- timeline events: execution started, completed, failed, cancelled, accepted, and rejected.
- workflow projection fields for current execution, execution status, execution eligibility, execution failure, and execution diagnostics.
- recovery integration for execution state.

Rules:

- Workflow never launches execution.
- Workflow never cancels execution.
- Workflow never modifies execution.
- Workflow never talks to provider APIs.
- Workflow consumes execution sessions, events, history, and repository execution state.

Tests:

- running execution projects correctly.
- completed execution projects correctly.
- failed and cancelled execution block workflow.
- awaiting acceptance opens execution acceptance gate.
- execution recovery rebuilds workflow view after restart.
- workflow execution services never call execution mutators.

Exit criteria:

- execution projection exists.
- execution completion and failure evaluation works.
- execution timeline integration works.
- execution recovery integration works.
- execution diagnostics exist.

## Milestone 5: Handoff Workflow Integration

Objective: make workflow understand execution outcomes and handoff authority.

Deliver:

- `WorkflowHandoffProjection` with execution id, repository id, handoff id/path, status, created timestamp, accepted timestamp, rejected timestamp, changes presence, and summary.
- `WorkflowHandoffStatus` with missing, pending, accepted, rejected, and invalid.
- `IWorkflowHandoffService`.
- handoff completion rules for pending, accepted, rejected, missing, and invalid.
- `WorkflowHandoffValidation`.
- `WorkflowHandoffDiagnostics`.
- timeline events: handoff created, validated, accepted, rejected, and invalid.
- projection integration for current handoff, handoff status, validation status, blocking conditions, and acceptance/rejection evidence.
- recovery integration for handoff state.

Rules:

- Handoff creation, validation, acceptance, and rejection remain Execution authority.
- Workflow never accepts or rejects handoffs.
- Existing handoff acceptance and rejection commands remain canonical.

Tests:

- pending, accepted, rejected, missing, and invalid handoffs project correctly.
- pending handoff opens execution acceptance gate.
- accepted handoff makes decision stage eligible.
- rejected handoff blocks workflow until a new execution cycle.
- restart restores handoff state.
- workflow never mutates handoffs.

Exit criteria:

- handoff projection exists.
- handoff service exists.
- validation exists.
- diagnostics exist.
- timeline integration exists.
- recovery integration exists.

## Milestone 6: Decision Workflow Integration

Objective: make workflow decision-aware by consuming the existing Decisions domain.

Deliver:

- `WorkflowDecisionProjection` with decision id, candidate id, proposal id, package id, status, review state, resolution state, human authoring burden, created timestamp, and resolved timestamp.
- `WorkflowDecisionStatus` with missing, discovered, generated, under review, awaiting resolution, resolved, archived, and superseded.
- `IWorkflowDecisionService`.
- decision resolution rules for awaiting resolution, resolved, archived, and superseded.
- decision governance integration that treats healthy as eligible, advisory findings as eligible with warning, and blocked findings as workflow blocked.
- decision quality integration that surfaces human authoring burden, recommendation stability, tradeoff quality, context quality, and constraint quality as diagnostics.
- decision certification integration that surfaces certified, warning, and failed status as observability.
- `WorkflowDecisionDiagnostics`.
- timeline events: decision discovered, generated, reviewed, refined, resolved, archived, and superseded.
- recovery integration for decision status, governance, quality, certification, and resolution.

Rules:

- Workflow projection never mutates decisions. Later preparation may request existing Decisions discovery or generation commands, but workflow never refines, resolves, archives, supersedes, governs, or certifies decisions.
- Workflow never treats recommendations as authority.
- Progression eligibility must be based on resolved decision authority, not recommendation output.
- Superseded decisions follow replacement authority.

Tests:

- discovered, generated, awaiting resolution, resolved, archived, and superseded decisions project correctly.
- awaiting resolution opens decision resolution gate.
- resolved decisions close the gate and make operational context eligible.
- superseded decisions follow replacement lineage.
- governance healthy/advisory/blocked statuses project correctly.
- quality and certification signals surface as diagnostics.
- recovery rebuilds decision workflow state.
- workflow never mutates decisions.

Exit criteria:

- decision projection exists.
- decision integration works.
- resolution gates work.
- governance, quality, and certification signals surface.
- timeline integration exists.
- recovery integration exists.
- diagnostics exist.

## Milestone 7: Operational Context Workflow Integration

Objective: make workflow continuity-aware by observing operational-context proposals, reviews, and promotions.

Deliver:

- `WorkflowOperationalContextProjection` with proposal id, repository id, status, review state, promotion state, created timestamp, reviewed timestamp, promoted timestamp, reviewer, summary, source decision id, and source execution id.
- `WorkflowOperationalContextStatus` with missing, proposed, under review, accepted, edited, rejected, ready for promotion, promoted, and archived.
- `IWorkflowOperationalContextService`.
- context review rules for proposal exists, accepted or edited, rejected, promoted, and no context required.
- gate integration for operational context review and promotion.
- `WorkflowOperationalContextDiagnostics`.
- timeline events: operational context proposed, reviewed, accepted, edited, rejected, promoted, and archived.
- recovery integration for proposal state, review state, promotion state, gate state, and timeline events.
- decision-to-context linkage when evidence connects a resolved decision to an assimilation recommendation or context proposal.

Rules:

- Continuity remains authoritative.
- Workflow never accepts, edits, rejects, promotes, or mutates operational context.
- No context proposal required is eligible for commit, but diagnostics must explain why no continuity update is required.

Tests:

- proposed, accepted, edited, rejected, and promoted context projects correctly.
- unreviewed proposal opens review gate.
- accepted or edited unpromoted proposal opens promotion gate.
- rejected proposal closes context gates and makes commit eligible.
- promoted proposal closes context gates and makes commit eligible.
- no-context-required state explains itself.
- decision-to-context linkage works when evidence exists.
- recovery rebuilds context workflow state.
- workflow never mutates operational context.

Exit criteria:

- operational context projection exists.
- operational context service exists.
- review and promotion gate integration works.
- decision-to-context linkage works.
- timeline integration exists.
- recovery integration exists.
- diagnostics exist.

## Milestone 8: Git Workflow Integration

Objective: make workflow aware of commit, push, and repository lifecycle completion.

Deliver:

- `WorkflowGitProjection` with repository id, commit status, push status, commit id, branch, commit timestamp, push timestamp, pending changes flag, and unpushed changes flag.
- `WorkflowGitStatus` with not ready, awaiting commit, committed, awaiting push, pushed, and failed.
- `IWorkflowGitService`.
- commit rules for awaiting commit, committed, and commit not required.
- push rules for awaiting push, pushed, and push skipped.
- gate integration for commit approval and push approval.
- `WorkflowGitDiagnostics`.
- timeline events: commit prepared, commit approved, commit executed, push approved, push executed, and push skipped.
- recovery integration for commit status, push status, gate status, and timeline events.
- `WorkflowCompletionEvaluation`.

Rules:

- Git remains authoritative.
- Workflow never commits.
- Workflow never pushes.
- Existing commit and push commands remain canonical.
- No changes produced can complete the workflow without commit or push, with diagnostics.
- Push skipped can complete the workflow only when there is explicit domain evidence that push was intentionally skipped.

Tests:

- awaiting commit, committed, awaiting push, pushed, failed, no-change, and skip states project correctly.
- commit gate opens correctly.
- push gate opens correctly.
- completion evaluation closes workflow only for push completed, legitimate skip, or no changes.
- pending push does not complete workflow.
- recovery rebuilds git workflow state.
- workflow never mutates repository state.

Exit criteria:

- git projection exists.
- git service exists.
- commit and push integration works.
- completion evaluation works.
- timeline integration exists.
- recovery integration exists.
- diagnostics exist.

## Milestone 9: Workflow Continuation Engine

Objective: automate mechanical progression between non-authority stages, introduce separately governed artifact preparation, and stop at every human gate.

Deliver:

- `IWorkflowContinuationService`.
- `IWorkflowPreparationService`.
- `WorkflowContinuationEvaluation`.
- `WorkflowPreparationEvaluation`.
- continuation rules for execution complete, handoff accepted, decision resolved, context promoted/rejected/not required, commit executed, push executed, no changes, and completed workflow.
- preparation rules for decision discovery/generation, operational-context proposal generation/linkage, and commit preparation.
- gate halting for work selection, execution acceptance, decision resolution, operational context review, operational context promotion, commit approval, and push approval.
- `WorkflowContinuationDiagnostics`.
- `WorkflowPreparationDiagnostics`.
- `WorkflowContinuationEvent`.
- `WorkflowPreparationEvent`.
- recovery integration that reevaluates continuation after restart without duplicate progression.
- `WorkflowContinuationHostedService`.
- `WorkflowInfluenceTrace`.
- `WorkflowHealthAssessment`.

Progression rules:

- execution complete and no open execution gate projects to handoff.
- handoff accepted and no open execution acceptance gate projects to decision.
- decision resolved and no decision governance block projects to operational context.
- context promoted, rejected, or not required projects to commit.
- commit executed projects to push.
- push executed, legitimate push skip, or no repository changes projects to completed.
- after push completed or legitimate completion condition exists, persist completion evidence and open work selection gate.

Preparation rules:

- accepted handoff may trigger decision discovery and reviewable proposal generation through existing Decisions commands, but this does not resolve the decision gate and does not itself move the stage beyond decision.
- resolved decision may trigger operational-context proposal generation or linkage through existing Continuity commands, but this does not review or promote context and does not itself move the stage beyond operational context.
- context complete may trigger commit preparation through the existing Execution command, but this does not approve or execute commit and does not itself move the stage beyond commit.
- preparation must record command name, source stage, input fingerprint, created artifact identifiers, skipped reason, and diagnostics.
- preparation must skip when equivalent reviewable artifacts already exist for the same fingerprint.

Gate halting:

- Any open authority gate produces `WaitingForHuman` and stops continuation.
- Work selection gate must never be auto-satisfied.
- Commit and push gates must never be crossed by continuation.
- Decision resolution and context promotion must never be crossed by continuation.
- Preparation may not run when it would create artifacts on the far side of an unsatisfied gate.

Idempotency rules:

- continuation events carry fingerprints.
- preparation events carry fingerprints.
- before invoking an allowed preparation command, preparation checks whether equivalent domain evidence already exists.
- restart reevaluation must not duplicate proposals, commit preparations, timeline entries, or continuation events.
- restart reevaluation must not duplicate preparation events or reviewable artifacts.

Tests:

- eligible workflow advances mechanically.
- ineligible workflow does not advance.
- eligible preparation creates reviewable artifacts only through existing domain commands.
- ineligible preparation does not create artifacts.
- every open gate stops progression.
- open gates block preparation when the requested artifact would bypass authority.
- restart does not duplicate progression.
- restart does not duplicate preparation.
- identical workflow state produces identical continuation outcome.
- identical preparation inputs produce identical preparation outcome.
- continuation never selects work, resolves decisions, promotes context, commits, pushes, or accepts handoffs.
- preparation never creates parallel commands, satisfies gates, moves workflow stage, or performs authority actions.
- every continuation decision explains why it advanced or stopped.
- every preparation decision explains why it created, skipped, or refused an artifact.

Exit criteria:

- continuation service exists.
- preparation service exists.
- continuation rules exist.
- preparation rules exist.
- gate halting works.
- continuation history exists.
- preparation history exists.
- hosted runner exists.
- recovery integration works.
- health assessment exists.

## Milestone 10: Workflow Certification

Objective: prove the workflow is correct, recoverable, explainable, and authority-preserving.

Deliver:

- `IWorkflowCertificationService`.
- `WorkflowCertificationResult`.
- `WorkflowCertificationFinding`.
- `RepositoryWorkflowReport`.
- `WorkflowProgressionReport`.
- `HumanGovernanceReport`.
- `WorkflowReadinessReport`.
- authority certification.
- recovery certification.
- continuation certification.
- preparation certification.
- end-to-end workflow fixture.
- workflow history certification.
- workflow diagnostics certification.
- workflow health certification.

Certification finding categories:

```text
Authority
Recovery
Progression
Preparation
Gate
History
Continuity
Execution
Decision
Git
Workflow
```

Required scenarios:

- happy path from execution through completed workflow.
- unresolved decision halts at decision resolution gate.
- context not reviewed halts at operational context review gate.
- context accepted but not promoted halts at operational context promotion gate.
- commit approval required halts at commit approval gate.
- push approval required halts at push approval gate.
- application restart recovers workflow without duplicate progression.
- preparation creates reviewable artifacts idempotently through existing domain commands.
- execution failure is diagnosable and recoverable.
- missing work selection halts at work selection gate.

Certification failure conditions:

- workflow selected work.
- workflow resolved a decision.
- workflow accepted, edited, rejected, or promoted context.
- workflow approved or executed commit.
- workflow approved or executed push.
- workflow crossed an open gate.
- workflow created or used a parallel domain command.
- workflow preparation satisfied a gate.
- workflow preparation moved the workflow stage.
- workflow preparation created duplicate review artifacts for the same fingerprint.
- workflow state cannot be reconstructed from domain evidence.
- continuation duplicated progression after restart.
- preparation duplicated artifacts or preparation events after restart.
- blocked, recovered, or progressed states lack diagnostics.
- preparation decisions lack diagnostics.
- authority history cannot be reconstructed.

Tests:

- failures generate findings.
- passing scenarios generate readiness evidence.
- authority certification detects forbidden mutation.
- recovery certification detects lost state, corruption, and duplicate progression.
- continuation certification detects missed gate halting.
- preparation certification detects duplicate artifacts, parallel commands, and gate bypass attempts.
- end-to-end fixture validates progression, gates, recovery, diagnostics, history, and certification.

Exit criteria:

- certification service exists.
- repository, progression, human-governance, and readiness reports exist.
- authority certification passes.
- recovery certification passes.
- continuation certification passes.
- preparation certification passes.
- end-to-end fixture passes.
- diagnostics and health certification pass.

## Cross-Cutting Requirements

### Derived Evidence Discipline

Workflow persistence must stay deliberately small and reconstructable.

Persist:

- timeline entries that explain stage changes
- gate evidence that explains human authority checkpoints
- continuation events that explain mechanical progression
- preparation events that explain reviewable artifact creation
- recovery records that explain rebuild decisions
- reports and certification results requested by a user or required by certification

Do not persist:

- duplicate execution sessions
- duplicate decision records
- duplicate operational-context proposals
- duplicate git status history as workflow authority
- private workflow-only state required for correctness
- hidden caches that cannot be rebuilt from domain artifacts

Deleting `.agents/workflow` may remove audit convenience and historical reports, but it must not make execution, decisions, continuity, git state, or current workflow projection incorrect.

### Fingerprints

Use SHA-256 over normalized UTF-8 evidence for:

- workflow projection inputs
- timeline entries
- gate evidence
- transition evaluations
- recovery inputs
- continuation evaluations
- continuation events
- preparation evaluations
- preparation events
- reports
- certification inputs

Fingerprints must be stable for identical evidence and must detect meaningful divergence.

### Markdown Projection Rules

Generated markdown should be deterministic and human-readable.

Preferred workflow timeline projection order:

```text
Repository
Generated At
Workflow Fingerprint
Current Stage
Previous Stage
Progress State
Blocking Gate
Required Human Action
Timeline Entries
Gate History
Continuation History
Recovery History
Diagnostics
```

Markdown is never workflow authority.

### Diagnostics

Every service must expose enough diagnostics to answer:

- what evidence was included
- what evidence was missing
- what evidence conflicted
- why a stage was selected
- why a gate opened
- what action satisfies the gate
- why a transition was valid or invalid
- why continuation advanced
- why continuation stopped
- why preparation created, skipped, or refused a reviewable artifact
- why recovery rebuilt or discarded workflow evidence
- why certification passed or failed

### Recovery

Recovery always follows this order:

1. Load authoritative domain state.
2. Build current workflow projection.
3. Load workflow evidence.
4. Compare workflow evidence to domain-derived fingerprint.
5. Keep matching evidence.
6. Rebuild missing or conflicting evidence from domains.
7. Persist recovery diagnostics.
8. Reevaluate continuation without crossing gates.
9. Reevaluate preparation without duplicate artifact creation.

### Hosted Services

Register:

```text
WorkflowRecoveryHostedService
WorkflowContinuationHostedService
```

`WorkflowRecoveryHostedService` runs on startup and performs recovery only.

`WorkflowContinuationHostedService` runs only after the continuation milestone is implemented and must be guarded by configuration:

```text
CommandCenter:Workflow:ContinuationEnabled
CommandCenter:Workflow:ContinuationIntervalSeconds
```

When enabled, `WorkflowContinuationHostedService` evaluates progression and preparation separately. It may delegate to `IWorkflowPreparationService`, but preparation must still emit `WorkflowPreparationEvent` records and must not be recorded as a stage progression.

During rollout, the endpoint-triggered continuation path can be used before enabling background continuation. Certification must cover both endpoint-triggered and hosted continuation behavior.

### Artifact Discovery

Extend artifact discovery only for human-readable workflow projections and reports that belong in the generic artifact browser:

```text
.agents/workflow/timelines/*.md
.agents/workflow/gates/*.md
.agents/workflow/recovery/*.md
.agents/workflow/continuation/*.md
.agents/workflow/preparation/*.md
.agents/workflow/reports/*.md
```

Keep structured JSON out of the generic artifact editor unless a typed editor exists.

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

Run relevant subsets after each milestone and the full set before final certification.

## Test Plan

Add backend tests:

```text
WorkflowProjectionServiceTests
WorkflowStateMachineServiceTests
WorkflowRepositoryTests
WorkflowRecoveryServiceTests
WorkflowGateCatalogServiceTests
WorkflowExecutionServiceTests
WorkflowHandoffServiceTests
WorkflowDecisionServiceTests
WorkflowOperationalContextServiceTests
WorkflowGitServiceTests
WorkflowContinuationServiceTests
WorkflowPreparationServiceTests
WorkflowHealthServiceTests
WorkflowCertificationServiceTests
WorkflowEndpointTests
```

Add UI characterization tests:

```text
workflowStagePanel.test.tsx
workflowTimelinePanel.test.tsx
workflowGatePanel.test.tsx
workflowTransitionPanel.test.tsx
workflowDiagnosticsPanel.test.tsx
workflowRecoveryPanel.test.tsx
workflowContinuationPanel.test.tsx
workflowPreparationPanel.test.tsx
workflowHealthPanel.test.tsx
workflowCertificationPanel.test.tsx
```

Add an end-to-end workflow fixture that exercises:

```text
Execution
Handoff
Decision
Operational Context
Commit
Push
Completed
Recovery
Certification
```

The fixture must validate both successful progression and gate-halting cases.

## Non-Goals

Do not implement:

- autonomous work selection
- autonomous prioritization
- autonomous execution launch without explicit user-selected work
- automatic handoff acceptance or rejection
- automatic decision resolution
- automatic decision archival or supersession
- automatic operational-context acceptance, edit, rejection, or promotion
- automatic commit execution
- automatic push execution
- provider interaction from workflow services
- direct mutation of execution, decision, continuity, git, or reasoning artifacts
- workflow-owned domain state
- hidden private workflow database
- client-side workflow authority
- Tauri-owned workflow rules
- metrics-driven lifecycle mutation
- background filesystem watchers for lifecycle mutation
- raw conversation transcript storage
- productivity scoring
- a single opaque workflow quality score without supporting evidence

## Final Exit State

Command Center has a dedicated Workflow capability that can:

- project current repository workflow state across existing domains
- determine current stage, previous stage, next possible stages, and blocking gate
- map human authority gates to existing domain commands
- persist derived workflow timeline, gate history, continuation history, recovery history, reports, and certification evidence
- persist preparation history separately from continuation history
- recover workflow evidence after restart from authoritative domain artifacts
- integrate execution, handoff, decisions, operational context, commit, and push into one lifecycle view
- mechanically progress between non-authority stages after explicit work selection
- prepare reviewable artifacts through existing domain commands without treating preparation as progression
- halt at every human authority gate
- prevent duplicate progression after restart
- prevent duplicate preparation after restart
- explain every progression, block, recovery, and failure
- explain every artifact preparation, skipped preparation, and refused preparation
- report workflow health, progression, governance participation, and readiness
- certify authority preservation, recovery, continuation, preparation, gate halting, history reconstruction, diagnostics, and end-to-end repository workflow behavior

Repository files remain authoritative, humans remain governors, existing domains remain owners of their state, and workflow becomes the mechanical coordinator that observes, progresses, recovers, explains, and audits repository work without becoming the authority over it.
