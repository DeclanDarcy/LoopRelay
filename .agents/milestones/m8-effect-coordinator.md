# M8 — Effect Coordinator


### Target design

- [ ] Create `Effects/` contracts and services in `LoopRelay.Orchestration.Primitives`; keep OS/Git/filesystem adapters in `LoopRelay.Infrastructure`; move feature-specific effect handlers out of `UnifiedCliComposition` and `LoopRelay.Completion` mutation paths.

Core contracts:

- [ ] `EffectIntent`: effect ID, causal spine, semantic operation key, executor key/version, target descriptor, typed payload hash, ordering/dependencies, `BlockingLocal` or `RequiredAsync`, precondition, postcondition, reconciliation strategy, and idempotency key.
- [ ] `EffectLifecycle`: Planned, Leased, Started, Pending, Succeeded, Failed, Stalled, Cancelled, Unknown, Reconciling, RetryAuthorized, and HumanActionRequired.
- [ ] `EffectReceipt`: immutable receipt ID, intent ID, executor/version, observed target identity, before/after facts, postcondition verdict, external correlation, and evidence IDs.
- [ ] `IEffectWorkStore`: scan, lease with compare-and-set row version/expiry, append lifecycle event, record receipt, and settle a plan transactionally.
- [ ] `IEffectExecutorRegistry` and one typed executor per semantic operation; `IEffectReconciler` observes the target independently.
- [ ] `EffectWorker` scans all unsettled required work at process start and before/after kernel cycles. Only one worker is scheduled now, but durable leases make crash recovery deterministic.

### Persistence

- [ ] Introduce logical schema v10 with versioned migration from v9.
- [ ] Evolve `canonical_effect_intents` rather than creating a competing effect store. Add causal workspace/run/workflow-instance fields, executor identity/version, target and payload documents/hashes, requiredness, dependency document, pre/postcondition documents, row version, lease owner/expiry, attempt count, and terminal receipt reference.
- [ ] Replace `canonical_effect_records` as semantic state with an append-only lifecycle event stream; migrate existing rows without inventing unobserved receipts. Add immutable receipt and reconciliation-attempt tables.
- [ ] Add indexes for unsettled scan order, lease expiry, transition/attempt, semantic operation key, and receipt lookup.
- [ ] Split `CanonicalWorkflowPersistenceStore.RecordEffectIntentStateAsync`: the effect store owns effect lifecycle, while a settlement transaction owned by workspace state derives attempt/transition progress only after required postconditions are verified.

### Routing and extraction

- [ ] Replace `TransitionEffectCoordinator.CoordinateAsync(TransitionRuntimeResult, ...)` with intent discovery/coordination by durable plan or transition identity. Never depend on the in-memory attempt result to find work.
- [ ] Replace `UnifiedEffectExecutor` branching with registered executors for:
   - [ ] product/evidence materialization;
   - [ ] filesystem artifact write/move/archive;
   - [ ] projection materialization;
   - [ ] nested `.agents` commit;
   - [ ] nested `.agents` push;
   - [ ] parent repository commit/gitlink update;
   - [ ] parent push;
   - [ ] export package write; and
   - [ ] lifecycle/checkpoint cleanup where modeled as an effect.
- [ ] Make each executor perform exactly one semantic mutation. Remove methods that both materialize multiple artifacts and advance workflow/stage state.
- [ ] Keep current workflow-authored publication intents temporarily, but translate them through the new protocol. M13 removes this temporary declaration duplication and synthesizes Git effects from output surfaces.
- [ ] Route `AgentsSubmodulePublisher`, `CommitGate`, archive writes, `HistoryProjectionEffectRunner`, `LoopArtifacts` rotations, and direct completion/feature filesystem mutation behind typed effects. Leave no supported direct call site after parity tests pass.

### Verification

- [ ] Fault inject before call, after outward mutation/before receipt, after receipt/before settlement, between ordered effects, during cancellation, and during process restart.
- [ ] Prove duplicate scanning and expired leases cause at most one semantic mutation.
- [ ] Reconcile a completed local commit after receipt loss without a second commit.
- [ ] Leave push pending while the remote is unavailable and prove transition/Plan/completion settlement remains disallowed where required.
- [ ] Verify receipts against independent Git/filesystem observation.
- [ ] Run component suites and applicable publication certification cases.

### Exit gate

- [ ] Every required mutation is discoverable after restart, ordered, idempotent, receipted, independently reconcilable, and unable to produce a false completion claim. No supported feature body directly performs a required external mutation.

