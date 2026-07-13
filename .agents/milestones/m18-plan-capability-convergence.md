# M18 — Plan capability convergence


### Implementation

- [x] Encode Plan's ordered lifecycle in the catalog: warm authoring -> adversarial projection -> read-only review -> warm revision -> operational context/details/milestones -> refinement -> publication -> readiness.
- [x] Move warm continuity behind M9. Bind checkpoints to session/turn, exact profile, prompt facts, input receipts, plan hash, and causal spine. Apply D12 on restart.
- [x] Use a scoped artifact transaction for candidate Plan files. Snapshot only declared output surfaces; failed validation restores the candidate surface and leaves promoted products unchanged. Record rollback evidence through effects.
- [x] Promote `ExecutablePlan`, `OperationalContext`, `ExecutionDetails`, and `ExecutionMilestoneSet` only after schema/gate/freshness validation.
- [x] Derive publication from declared output surfaces. Order nested `.agents` materialization/commit/push before the parent gitlink/repository commit/push. Reconcile receipts against both repositories.
- [x] Promote `ExecutionReadiness` only after every required product, gate, blocking commit, and required push has settled.
- [x] Port useful Plan tests for preflight, authoring, review/revision, rollback, milestones, warm restart, independent `.agents` topology, and publication to canonical owners.
- [x] After parity and owner acceptance, delete `src/LoopRelay.Plan.Cli/`, its test project, solution/project references, publish scripts/artifacts, and Plan-only shims.

### Exit gate

- [x] Both Roadmap producers enter identical Plan contracts; restart needs no lost object; failed revision preserves the prior products; nested and parent repository observations equal effect receipts; readiness cannot be observed early; deletion changes no supported behavior.

### Scoped artifact transaction and publication order

Before mutation, record a manifest of the declared candidate output surface: repository identity,
commit, normalized paths, existence, hashes, modes, and aggregate surface hash. Apply changes to a
candidate/staging surface through effect-owned writes. Review and validation read the candidate;
promoted product facts and live output surfaces remain unchanged until schema, gates, and input
freshness pass. Rejection/validation failure reconciles an effect-owned restore/removal to the
recorded manifest and appends rollback evidence.

Publication order is strict: nested `.agents` materialize -> nested commit -> required nested push
settled -> parent gitlink/materialize -> parent commit -> required parent push settled -> promote
`ExecutionReadiness`. A receipt identifies repository top, ref, commit, tree/surface hash, and
remote postcondition. Product validation alone never permits early readiness.

### Warm continuity mismatch

A warm checkpoint binds session/turn, exact executable/app-server profile, rendered prompt facts,
input receipts/surface hash, plan candidate hash, catalog identity, and causal spine. On mismatch,
use only the owner-accepted D12 mechanism: certified reconstruction if available; otherwise
unsupported-capability/human-action-required. Never silently resume under a different profile or
start a fresh warm session and call it continuation.

Run both Traditional and Eval full chains for this shared-chain convergence milestone.
