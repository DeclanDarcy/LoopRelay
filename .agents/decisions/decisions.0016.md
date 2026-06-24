# Decisions

## Newly Authorized

- Treat Milestone 5A workflow consumption as correct and complete.
- Preserve the lifecycle-to-workflow authority direction:
  - Registry, analysis, policy, eligibility, transfer, and recovery feed observability.
  - Observability feeds workflow consumption.
  - Workflow must not consume policy, transfer, recovery, eligibility, or registry authority services directly.
- Workflow may summarize, report, surface health, surface influence, surface lineage, and certify consumption.
- Workflow must not create sessions, activate sessions, retire sessions, transfer sessions, override policy, override eligibility, or repair lifecycle state.
- Keep workflow report and certification consumption routed through observability rather than lifecycle internals.
- Start Milestone 5B with repository consumption.
- Add `RepositoryDecisionSessionSummary` as a thin projection model.
- Repository decision-session summary should expose:
  - Decision session id.
  - State.
  - Lifecycle decision.
  - Eligibility status.
  - Coherence score.
  - Transfer pressure.
  - Estimated token count.
  - Cache risk.
  - Health dimensions.
  - Recent transfer lineage.
- Keep detailed lifecycle state in dedicated lifecycle endpoints rather than repository summaries.
- Use the existing optional dependency pattern in `CommandCenter.Middle` so repository projections work whether decision sessions are enabled or absent.
- `CommandCenter.Middle` must consume decision-session state through `IDecisionSessionObservabilityService`.
- Do not introduce direct references from `CommandCenter.Middle` to policy, transfer, recovery, eligibility, or other lifecycle authority services.
