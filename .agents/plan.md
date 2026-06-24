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

(See ./milestones/m0-capability-verification.md)

## Milestone 1: Workflow Engine Integration

(See ./milestones/m1-workflow-engine.md)

## Milestone 2: Governance Workspace Integration

(See ./milestones/m2-governance-workspace.md)

## Milestone 3: Decision Pipeline Completion

(See ./milestones/m3-decision-pipeline.md)

## Milestone 4: Decision Transparency

(See ./milestones/m4-decision-transparency.md)

## Milestone 5: Execution Transparency

(See ./milestones/m5-execution-transparency.md)

## Milestone 6: Reasoning Transparency

(See ./milestones/m6-reasoning-transparency.md)

## Milestone 7: Continuity and Operational Context Transparency

(See ./milestones/m7-continuity-context.md)

## Milestone 8: Unified Explainability Layer

(See ./milestones/m8-explainability-layer.md)

## Milestone 9: Product Cohesion

(See ./milestones/m9-product-cohesion.md)

## Milestone 10: MVP Closure and Release Readiness

(See ./milestones/m10-release-readiness.md)

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
