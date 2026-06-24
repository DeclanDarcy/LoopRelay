# Milestone 5: Workflow And Repository Consumption

Objective: let workflow and repository summaries consume governance-session state without owning it.

Add workflow models in `src/CommandCenter.Workflow/Models`:

- [ ] `WorkflowDecisionSessionProjection`
- [ ] `WorkflowGovernanceSummary`
- [ ] `WorkflowTransferProjection`
- [ ] `WorkflowGovernanceHealthProjection`
- [ ] `WorkflowGovernanceInfluenceProjection`
- [ ] `WorkflowGovernanceReadiness`
- [ ] `DecisionSessionWorkflowDiagnostics`

Add workflow service abstraction and implementation:

- [ ] `IWorkflowDecisionSessionService`
- [ ] `WorkflowDecisionSessionService`

Integrate into:

- [ ] `WorkflowProjectionService`
- [ ] `WorkflowHealthService`
- [ ] `WorkflowReportService`
- [ ] `WorkflowCertificationService`

Workflow projection fields:

- [ ] Decision session id.
- [ ] Decision session state.
- [ ] Estimated token count.
- [ ] Estimated cache TTL.
- [ ] Estimated cache miss risk.
- [ ] Reuse score.
- [ ] Transfer score.
- [ ] Coherence score.
- [ ] Transfer pressure.
- [ ] Current lifecycle decision.
- [ ] Transfer eligibility status.
- [ ] Continuity artifact id and fingerprint when relevant.
- [ ] Transfer lineage.
- [ ] Governance health dimensions.

Repository summary integration:

- [ ] Add `RepositoryDecisionSessionSummary` in `src/CommandCenter.Middle/Projections`.
- [ ] Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection`.
- [ ] Extend `RepositoryProjectionService` through an optional decision-session observability dependency, matching the existing optional reasoning dependency pattern.

Backend endpoints:

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/health`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/influence`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/summary`

Authority rules:

- [ ] Workflow may display, report, explain, and certify consumption.
- [ ] Workflow may not change lifecycle decisions.
- [ ] Workflow may not evaluate transfer eligibility as authority.
- [ ] Workflow may not execute transfer.
- [ ] Workflow may not retire, create, or activate sessions.

Tests:

- [ ] Lifecycle state appears in workflow projection.
- [ ] Continue and transfer decisions are visible.
- [ ] Eligibility status is visible.
- [ ] Continuity artifact lineage is projected.
- [ ] Transfer lineage is projected.
- [ ] Lifecycle health appears in workflow health.
- [ ] Influence trace appears in workflow influence projection.
- [ ] Repository summary includes decision-session state, TTL, cache risk, and health.
- [ ] Workflow cannot call mutating lifecycle APIs.
- [ ] Deleted workflow projection is rebuilt.

Exit criteria:

- [ ] Workflow can answer active governance session, current lifecycle recommendation, transfer eligibility, health, recent transfer lineage, continuity artifact lineage, and increasing transfer pressure.
- [ ] The decision-session lifecycle remains authoritative.




