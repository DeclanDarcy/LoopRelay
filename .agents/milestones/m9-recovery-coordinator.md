# M9 — Recovery Coordinator


### Target design

- [ ] Unify `TransitionRecoveryCoordinator`, the richer `RecoveryRuntime`, `DecisionSessionRecoveryCoordinator`, Plan/Execute warm continuity, effect recovery, and completion recovery behind one case/classification/plan/action model in `src/LoopRelay.Orchestration.Primitives/Recovery/`.

- [ ] Define `RecoveryCaseIdentity`, scope kind, causal subject, durable-boundary classification, observed source facts, immutable plan, action journal, and terminal disposition.
- [ ] Canonical classifications must distinguish NotStarted, InFlight, AcceptedUnknown, SucceededUncommitted, Failed, Cancelled, ProviderUnknown, PartiallyEffected, CompletionPartiallyClosed, Corrupt, and EvidenceIncomplete.
- [ ] Plan actions are ReconcileProvider, ReconcileEffects, ResumeSession, ReconstructContext, NativeFork, ReuseRawOutput, RetryNewAttempt, Compensate, Wait, or RequestHumanDecision.
- [ ] The classifier is pure over durable facts. The planner applies D5, D10, resolved policy, and exact capability evidence, persists the plan, then the runtime executes only that plan.
- [ ] Reuse the same root run/workflow instance/transition run where semantically appropriate. A new provider attempt always receives a new attempt ID and preserves the source attempt unchanged.
- [ ] Add typed application use cases for recovery inspect, plan, and execute. They may initially be adapted by the current CLI but must not contain recovery logic there.

### Persistence and migration

- [ ] Evolve the schema for recovery cases, immutable classifications, plans, source-evidence links, and action events.
- [ ] Import existing `transition_recovery_plans` and `session_recovery_*` facts into the unified projection without losing IDs. Keep legacy tables read-only behind a migration adapter until verification proves parity, then remove their runtime readers.
- [ ] Make cancellation terminal evidence durable even when the caller token is cancelled.

### Verification

- [ ] Build a matrix covering every durable transition boundary, prompt lifecycle boundary, effect boundary, warm-session boundary, decision turn, and completion-closure boundary.
- [ ] Prove no recovery command can bypass classification or reconciliation.
- [ ] Prove exact-profile denial fails closed and selects only an allowed reconstruction/human path.
- [ ] Restart during every recovery action and resume the same action idempotently.
- [ ] Assert `OperatorUnblock` and workflow-local retry loops are unreachable.

### Exit gate

- [ ] Every interruption has one evidence-based classification and persisted plan; no recovery depends on an undiscoverable in-memory object; cancellation and unknown work remain distinct from failure and not-started.

