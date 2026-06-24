# Decisions

## Newly Authorized

- Accept Milestone 5B repository consumption as correct.
- Preserve the lifecycle-to-consumer authority direction:
  - Lifecycle core feeds observability.
  - Observability feeds workflow.
  - Observability feeds middle.
- `CommandCenter.Middle` must consume decision-session state only through `IDecisionSessionObservabilityService`.
- `CommandCenter.Middle` must not consume registry, policy, transfer, recovery, eligibility, or other lifecycle authority services directly.
- Repository summaries should remain thin navigation and summarization views.
- Detailed lifecycle state should remain in dedicated decision-session analysis, lifecycle, and workflow endpoints.
- Preserve optional decision-session observability consumption so repository projections build when decision sessions are disabled, unavailable, or absent.
- Treat Milestone 5A workflow consumption and Milestone 5B repository consumption as complete.
- Continue next with Milestone 5 hardening rather than new functionality.
- Prioritize hardening tests for workflow visibility across continue, transfer, eligibility, transfer lineage, artifact lineage, influence, and health.
- Prioritize authority regression tests proving workflow cannot create sessions, activate sessions, retire sessions, transfer sessions, override policy, or override eligibility.
- Add projection rebuild tests for deleted, missing, and corrupt disposable projections without mutating authoritative lifecycle state.
- Keep or add explicit repository projection coverage proving observability-missing repositories still build.
