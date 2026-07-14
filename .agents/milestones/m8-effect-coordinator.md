# M8 — Effect Coordinator


### Target design

- [x] Create `Effects/` contracts and services in `LoopRelay.Orchestration.Primitives`; keep OS/Git/filesystem adapters in `LoopRelay.Infrastructure`; move feature-specific effect handlers out of `UnifiedCliComposition` and `LoopRelay.Completion` mutation paths.

Core contracts:

- [x] `EffectIntent`: effect ID, causal spine, semantic operation key, executor key/version, target descriptor, typed payload hash, ordering/dependencies, `BlockingLocal` or `RequiredAsync`, precondition, postcondition, reconciliation strategy, and idempotency key.
- [x] `EffectLifecycle`: Planned, Leased, Started, Pending, Succeeded, Failed, Stalled, Cancelled, Unknown, Reconciling, RetryAuthorized, and HumanActionRequired.
- [x] `EffectReceipt`: immutable receipt ID, intent ID, executor/version, observed target identity, before/after facts, postcondition verdict, external correlation, and evidence IDs.
- [x] `IEffectWorkStore`: scan, lease with compare-and-set row version/expiry, append lifecycle event, record receipt, and settle a plan transactionally.
- [x] `IEffectExecutorRegistry` and one typed executor per semantic operation; `IEffectReconciler` observes the target independently.
- [x] `EffectWorker` scans all unsettled required work at process start and before/after kernel cycles. Only one worker is scheduled now, but durable leases make crash recovery deterministic.

### Persistence

- [x] Introduce logical schema v10 with versioned migration from v9.
- [x] Evolve `canonical_effect_intents` rather than creating a competing effect store. Add causal workspace/run/workflow-instance fields, executor identity/version, target and payload documents/hashes, requiredness, dependency document, pre/postcondition documents, row version, lease owner/expiry, attempt count, and terminal receipt reference.
- [x] Replace `canonical_effect_records` as semantic state with an append-only lifecycle event stream; migrate existing rows without inventing unobserved receipts. Add immutable receipt and reconciliation-attempt tables.
- [x] Add indexes for unsettled scan order, lease expiry, transition/attempt, semantic operation key, and receipt lookup.
- [x] Split `CanonicalWorkflowPersistenceStore.RecordEffectIntentStateAsync`: the effect store owns effect lifecycle, while a settlement transaction owned by workspace state derives attempt/transition progress only after required postconditions are verified.

### Routing and extraction

- [x] Replace `TransitionEffectCoordinator.CoordinateAsync(TransitionRuntimeResult, ...)` with intent discovery/coordination by durable plan or transition identity. Never depend on the in-memory attempt result to find work.
- [x] Replace `UnifiedEffectExecutor` branching with registered executors for:
   - [x] product/evidence materialization;
   - [x] filesystem artifact write/move/archive;
   - [x] projection materialization;
   - [x] nested `.agents` commit;
   - [x] nested `.agents` push;
   - [x] parent repository commit/gitlink update;
   - [x] parent push;
   - [x] export package write; and
   - [x] lifecycle/checkpoint cleanup where modeled as an effect.
- [x] Make each executor perform exactly one semantic mutation. Remove methods that both materialize multiple artifacts and advance workflow/stage state.
- [x] Keep current workflow-authored publication intents temporarily, but translate them through the new protocol. M13 removes this temporary declaration duplication and synthesizes Git effects from output surfaces.
- [x] Route `AgentsSubmodulePublisher`, `CommitGate`, archive writes, `HistoryProjectionEffectRunner`, `LoopArtifacts` rotations, and direct completion/feature filesystem mutation behind typed effects. Leave no supported direct call site after parity tests pass.

### Verification

- [x] Fault inject before call, after outward mutation/before receipt, after receipt/before settlement, between ordered effects, during cancellation, and during process restart.
- [x] Prove duplicate scanning and expired leases cause at most one semantic mutation.
- [x] Reconcile a completed local commit after receipt loss without a second commit.
- [x] Leave push pending while the remote is unavailable and prove transition/Plan/completion settlement remains disallowed where required.
- [x] Verify receipts against independent Git/filesystem observation.
- [x] Run component suites and applicable publication certification cases.

### Exit gate

- [x] Every required mutation is discoverable after restart, ordered, idempotent, receipted, independently reconcilable, and unable to produce a false completion claim. No supported feature body directly performs a required external mutation.

### Lifecycle and retry-authority details

Use an append-only lifecycle with a derived current state. The minimum legal flow is:

```text
Planned -> Leased -> Started -> Pending | Succeeded | Failed | Stalled | Cancelled | Unknown
Unknown -> Reconciling -> Succeeded | Failed | Stalled | RetryAuthorized | HumanActionRequired
Failed/Stalled -> RetryAuthorized only through policy/recovery -> Leased
expired Leased with no Started fact -> Planned/lease-available
expired Leased after Started or indeterminate executor evidence -> Unknown
```

`Pending` means known incomplete required-asynchronous work, not uncertainty. `Unknown` means the
mutation may have occurred. Neither failed, stalled, cancelled, nor unknown work is automatically
executed merely because the scanner finds it. Only an unstarted plan or a durable
`RetryAuthorized` fact is executable. Every transition validates row version, lease owner/expiry,
dependency settlement, executor version, and preconditions.

A local output-surface commit is `BlockingLocal`. Its verified receipt permits the attempt to
advance to the point allowed by the catalog. A push is a distinct `RequiredAsync` intent; the
public result may expose it as pending, but Plan readiness, certified completion, and any catalog
boundary that declares it required cannot settle until its postcondition is verified.

### Receipt, reconciliation, and retirement details

Every receipt needs intent and attempt identity, semantic operation/idempotency key, executor
key/version, normalized target identity, before observation, after observation, postcondition
verdict, external correlation (commit/ref/path/process identifier as applicable), evidence IDs,
and observation time as diagnostic data. Reconciliation must use an independent observer rather
than trusting the executor's returned success.

The decisive negative fixture is mutation success followed by receipt-write loss: restart must
observe the postcondition, append a receipt/reconciliation fact, and create no second semantic
mutation. Add equivalent cases for duplicate leases, cancellation, dependency-order violation,
unavailable remote push, and a crash after receipt but before state settlement.

Inventory call sites for Git, filesystem writes/moves/deletes, archive materialization, exports,
projection/history materialization, nested `.agents` publication, parent gitlink publication,
checkpoint cleanup, and completion cleanup. The architecture registry must identify the
allowlisted effect adapter for each operation and reject production calls from feature handlers,
kernel, application, completion decision code, or CLI. A handler may construct typed payload data;
it may not invoke the outward adapter or advance workflow state.

M8 owns the first post-baseline logical schema version, v10. Existing v8 and partial-v9 cases remain
ingress tests after the production version advances.
