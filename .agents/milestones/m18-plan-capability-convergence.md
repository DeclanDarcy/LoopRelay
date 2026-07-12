# M18 — Plan capability convergence


### Implementation

- [ ] Encode Plan's ordered lifecycle in the catalog: warm authoring -> adversarial projection -> read-only review -> warm revision -> operational context/details/milestones -> refinement -> publication -> readiness.
- [ ] Move warm continuity behind M9. Bind checkpoints to session/turn, exact profile, prompt facts, input receipts, plan hash, and causal spine. Apply D12 on restart.
- [ ] Use a scoped artifact transaction for candidate Plan files. Snapshot only declared output surfaces; failed validation restores the candidate surface and leaves promoted products unchanged. Record rollback evidence through effects.
- [ ] Promote `ExecutablePlan`, `OperationalContext`, `ExecutionDetails`, and `ExecutionMilestoneSet` only after schema/gate/freshness validation.
- [ ] Derive publication from declared output surfaces. Order nested `.agents` materialization/commit/push before the parent gitlink/repository commit/push. Reconcile receipts against both repositories.
- [ ] Promote `ExecutionReadiness` only after every required product, gate, blocking commit, and required push has settled.
- [ ] Port useful Plan tests for preflight, authoring, review/revision, rollback, milestones, warm restart, independent `.agents` topology, and publication to canonical owners.
- [ ] After parity and owner acceptance, delete `src/LoopRelay.Plan.Cli/`, its test project, solution/project references, publish scripts/artifacts, and Plan-only shims.

### Exit gate

- [ ] Both Roadmap producers enter identical Plan contracts; restart needs no lost object; failed revision preserves the prior products; nested and parent repository observations equal effect receipts; readiness cannot be observed early; deletion changes no supported behavior.

