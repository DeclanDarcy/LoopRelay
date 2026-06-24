# Milestone 5: Workflow And Repository Consumption

Objective: let workflow and repository summaries consume governance-session state without owning it.

Add workflow models in `src/CommandCenter.Workflow/Models`:

- [x] `WorkflowDecisionSessionProjection`
- [x] `WorkflowGovernanceSummary`
- [x] `WorkflowTransferProjection`
- [x] `WorkflowGovernanceHealthProjection`
- [x] `WorkflowGovernanceInfluenceProjection`
- [x] `WorkflowGovernanceReadiness`
- [x] `DecisionSessionWorkflowDiagnostics`

Add workflow service abstraction and implementation:

- [x] `IWorkflowDecisionSessionService`
- [x] `WorkflowDecisionSessionService`

Integrate into:

- [x] `WorkflowProjectionService`
- [x] `WorkflowHealthService`
- [x] `WorkflowReportService`
- [x] `WorkflowCertificationService`

Workflow projection fields:

- [x] Decision session id.
- [x] Decision session state.
- [x] Estimated token count.
- [x] Estimated cache TTL.
- [x] Estimated cache miss risk.
- [x] Reuse score.
- [x] Transfer score.
- [x] Coherence score.
- [x] Transfer pressure.
- [x] Current lifecycle decision.
- [x] Transfer eligibility status.
- [x] Continuity artifact id and fingerprint when relevant.
- [x] Transfer lineage.
- [x] Governance health dimensions.

Repository summary integration:

- [x] Add `RepositoryDecisionSessionSummary` in `src/CommandCenter.Middle/Projections`.
- [x] Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection`.
- [x] Extend `RepositoryProjectionService` through an optional decision-session observability dependency, matching the existing optional reasoning dependency pattern.

Backend endpoints:

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/health`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/influence`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/summary`

Authority rules:

- [x] Workflow may display, report, explain, and certify consumption.
- [x] Workflow may not change lifecycle decisions.
- [x] Workflow may not evaluate transfer eligibility as authority.
- [x] Workflow may not execute transfer.
- [x] Workflow may not retire, create, or activate sessions.

Tests:

- [x] Lifecycle state appears in workflow projection.
- [x] Continue and transfer decisions are visible.
- [x] Eligibility status is visible.
- [x] Continuity artifact lineage is projected.
- [x] Transfer lineage is projected.
- [x] Lifecycle health appears in workflow health.
- [x] Influence trace appears in workflow influence projection.
- [x] Repository summary includes decision-session state, TTL, cache risk, and health.
- [x] Workflow cannot call mutating lifecycle APIs.
- [x] Deleted workflow projection is rebuilt.

Exit criteria:

- [x] Workflow can answer active governance session, current lifecycle recommendation, transfer eligibility, health, recent transfer lineage, continuity artifact lineage, and increasing transfer pressure.
- [x] The decision-session lifecycle remains authoritative.




