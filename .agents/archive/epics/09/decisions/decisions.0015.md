# Decisions

## Newly Authorized

- Treat Milestone 4 as complete.
- Continue to preserve observability as disposable read-model state only.
- Preserve the authority direction:
  - Lifecycle core feeds projection.
  - Lifecycle core feeds history.
  - Lifecycle core feeds influence.
  - Lifecycle core feeds health.
  - Observability must not feed lifecycle authority.
- Keep registry, analysis, policy, eligibility, transfer, and recovery as authority layers.
- Keep projection, history, influence, and health as consumption layers.
- Start Milestone 5 with workflow consumption.
- Workflow may display, summarize, report, surface health, surface transfer lineage, surface influence, and surface pressure.
- Workflow must not create sessions, activate sessions, retire sessions, transfer sessions, override policy, override eligibility, or repair lifecycle state.
- Prefer workflow consumption through `IDecisionSessionObservabilityService`.
- Do not let workflow consume analysis, policy, transfer, or recovery services directly.
- Build Milestone 5A first with:
  - `WorkflowDecisionSessionProjection`
  - `WorkflowGovernanceSummary`
  - `WorkflowGovernanceHealthProjection`
- Feed Milestone 5A entirely from `IDecisionSessionObservabilityService`.
- Build Milestone 5B after workflow projection by adding `RepositoryDecisionSessionSummary` to `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection` through the existing optional dependency pattern.
- Build Milestone 5C only after projection and summary are working, adding workflow-facing transfer lineage, artifact lineage, and governance influence.
