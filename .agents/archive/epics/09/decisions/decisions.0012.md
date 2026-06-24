# Decisions

## Newly Authorized

- Treat Milestones 1, 2, and 3 as complete; Milestone 4 observability is ready to begin.
- Preserve the lifecycle authority hierarchy: authoritative state feeds recovery, and recovery rebuilds derived state.
- Do not allow derived state to drive recovery of authoritative state.
- Preserve the acyclic boundary where recovery and eligibility both use lower-level evidence; recovery must not depend on eligibility.
- Introduce Milestone 4 observability as a pure projection layer below registry, analysis, policy, eligibility, transfer, and recovery.
- Observability must not become lifecycle authority or a hidden control plane.
- Build Milestone 4 in this order:
  1. Projection.
  2. History.
  3. Influence.
  4. Health.
- Start Milestone 4A with `DecisionSessionLifecycleProjection`, `DecisionSessionLifecycleHistory`, and `DecisionSessionObservabilityService`.
- Milestone 4A should compose current lifecycle state only from existing artifacts.
- Milestone 4A should not add health, influence traces, or new persistence.
- Lifecycle history must be reconstructed from durable evidence rather than maintained as separate authority.
- Lifecycle history should include created, activated, policy evaluated, eligibility evaluated, artifact created, transfer started, transfer completed, retired, and recovered events.
- Influence projections should be added only after projection and history exist.
- Influence should trace metrics, economics, coherence, policy, eligibility, transfer, and recovery.
- Health should be decomposed by registry, analysis, policy, eligibility, artifact, transfer, and recovery.
- Do not introduce a single composite lifecycle health score.
