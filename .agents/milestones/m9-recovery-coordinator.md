# M9 — Recovery Coordinator


### Target design

- [x] Unify `TransitionRecoveryCoordinator`, the richer `RecoveryRuntime`, `DecisionSessionRecoveryCoordinator`, Plan/Execute warm continuity, effect recovery, and completion recovery behind one case/classification/plan/action model in `src/LoopRelay.Orchestration.Primitives/Recovery/`.
  - [x] Transition, effect, warm-session, decision, and completion interruption paths emit canonical cases/classifications and persist canonical plans before authorized recovery work.
  - [x] Retire the remaining rich-session runtime dependency on legacy `session_recovery_*` reads/writes after parity.

- [x] Define `RecoveryCaseIdentity`, scope kind, causal subject, durable-boundary classification, observed source facts, immutable plan, action journal, and terminal disposition.
- [x] Canonical classifications must distinguish NotStarted, InFlight, AcceptedUnknown, SucceededUncommitted, Failed, Cancelled, ProviderUnknown, PartiallyEffected, CompletionPartiallyClosed, Corrupt, and EvidenceIncomplete.
- [x] Plan actions are ReconcileProvider, ReconcileEffects, ResumeSession, ReconstructContext, NativeFork, ReuseRawOutput, RetryNewAttempt, Compensate, Wait, or RequestHumanDecision.
- [x] The classifier is pure over durable facts. The planner applies D5, D10, resolved policy, and exact capability evidence, persists the plan, then the runtime executes only that plan.
- [x] Reuse the same root run/workflow instance/transition run where semantically appropriate. A new provider attempt always receives a new attempt ID and preserves the source attempt unchanged.
- [x] Add typed application use cases for recovery inspect, plan, and execute. They may initially be adapted by the current CLI but must not contain recovery logic there.

### Persistence and migration

- [x] Evolve the schema for recovery cases, immutable classifications, plans, source-evidence links, and action events.
- [x] Import existing `transition_recovery_plans` and `session_recovery_*` facts into the unified projection without losing IDs. Keep legacy tables read-only behind a migration adapter until verification proves parity, then remove their runtime readers.
  - [x] v11 imports transition plan IDs, session attempt IDs, classifications, plans, and source links idempotently.
  - [x] `transition_recovery_plans` has no post-migration production reader or writer.
  - [x] Make `session_recovery_*` migration-only and remove its runtime readers/writers.
- [x] Make cancellation terminal evidence durable even when the caller token is cancelled.

### Verification

- [x] Build a matrix covering every durable transition boundary, prompt lifecycle boundary, effect boundary, warm-session boundary, decision turn, and completion-closure boundary.
- [x] Prove no recovery command can bypass classification or reconciliation.
- [x] Prove exact-profile denial fails closed and selects only an allowed reconstruction/human path.
- [x] Restart during every recovery action and resume the same action idempotently.
- [x] Assert `OperatorUnblock` and workflow-local retry loops are unreachable.

### Exit gate

- [x] Every interruption has one evidence-based classification and persisted plan; no recovery depends on an undiscoverable in-memory object; cancellation and unknown work remain distinct from failure and not-started.
  - [x] Transition/provider, effect, Plan/Execute warm-session, decision, and completion cases are restart-discoverable in v11.
  - [x] Remove the final legacy session-recovery authority before claiming the singular-owner exit gate.

### Classification precedence

Use one boundary taxonomy rather than overlapping `AcceptedUnknown` and `ProviderUnknown` meanings:

| Last durable evidence | Classification | Minimum permitted next step |
|---|---|---|
| no authorized attempt/dispatch/effect start | `NotStarted` | authorize normal work |
| authorized work, no outward-start fact | `InFlight` only while a valid lease/process correlation exists; otherwise reclassify from evidence | wait or inspect |
| provider/effect start, no terminal observation | `AcceptedUnknown` | reconcile; never resend/re-execute |
| normalized provider output durable, promotion absent | `SucceededUncommitted` | validate freshness and reuse output or supersede via a new plan |
| durable explicit terminal failure | `Failed` | policy-gated new plan/attempt |
| durable cancellation | `Cancelled` plus the boundary-specific salvage facts | apply accepted D5 ruling |
| one or more effects settled and required work remains | `PartiallyEffected` | reconcile/resume the same effect plan |
| completion closure partly settled | `CompletionPartiallyClosed` | resume the same closure plan |
| facts conflict or required boundary evidence is absent/corrupt | `EvidenceIncomplete` or `Corrupt` | fail closed; repair/import/human decision |

Classifications are immutable observations. New evidence appends a new classification that
supersedes the prior identity; it never edits the prior fact. The planner must persist the exact
source-evidence set and selected mechanism before action.

### Action legality and cancellation ruling

Each recovery action records plan/action identity, source attempt/effect/session/completion IDs,
required capability/profile and policy evidence, pre/postconditions, idempotency key, and result.
`ResumeSession`, `NativeFork`, and provider read are authorized only by the exact observed profile.
`ReconstructContext` must bind the reconstructed input receipts and prompt facts.
`RetryNewAttempt` keeps root run, workflow instance, and transition-run identity but mints a new
immutable attempt. `ReuseRawOutput` never creates a second dispatch. `Compensate` is an effect plan,
not an in-memory undo.

Add the lost-provider-thread fixture with retained rollout evidence. It selects only a certified
salvage/reconstruction path and does not infer resume/fork support from the interface alone.

D5 remains a proposal until the owner rules cancellation before dispatch, after outward
acceptance, after validated output, during partial effects, and during partial completion closure.
Tests cover caller cancellation plus terminal evidence written with a non-cancelled evidence
token. No ruling may erase accepted/unknown work or convert cancellation into ordinary failure.
