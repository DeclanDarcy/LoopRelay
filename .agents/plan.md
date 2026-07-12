# Canonical Architecture Convergence Implementation Plan

## 1. Objective

Converge LoopRelay on one production architecture with:

- one typed application boundary and one production composition root;
- one immutable, versioned workflow catalog;
- one product-driven orchestration kernel;
- one logical workspace-state and evidence authority, currently backed by SQLite;
- one prompt composition and dispatch path;
- one runtime authorization path based on resolved policy and exact provider capability evidence;
- one durable effect protocol, recovery protocol, interaction protocol, completion authority, and canonical read model;
- one-way import for supported pre-canonical workspaces; and
- no reachable legacy workflow engines, direct required mutations, raw-table clients, hidden policy, or unowned runtime assets.

Completion means both roadmap producers feed the same Plan contract, Plan feeds Execute only after all readiness and publication obligations settle, Execute reaches certified completion across restart and cancellation boundaries, and every public claim is traceable to durable evidence.

## 2. Scope and constraints

### 2.1 In scope

- Production code under `src/`, the matching component tests under `tests/`, schema evolution, composition, public CLI behavior, deterministic certification, applicable live certification, and physical retirement of superseded code.
- Traditional Roadmap, Eval Roadmap, Plan, and Execute workflows.
- Git and filesystem effects, nested `.agents` repository publication, parent gitlink publication, archives, projections, storage operations, interactions, recovery, and completion closure.
- Preservation of provider/runtime diagnostics, telemetry, usage-limit behavior, prerequisite checks, prompt evidence, and causal lineage when their ownership is moved.

### 2.2 Out of scope

- Parallel workflow scheduling, concurrent active runs in one workspace, provider fallback, additional production providers, distributed workers, and cross-workspace scaling.
- Making SQLite, console rendering, or filesystem projections architectural authorities.
- Permanent compatibility fallbacks, dual writes, or runtime reads from imported legacy sources.
- New human-facing documentation except deletion or correction of claims that become false.

### 2.3 Delivery rule

Each milestone is a vertical slice. It must close contracts, durable state, production routing, failure/restart behavior, evidence, tests, and removal of the superseded route before it is accepted. A green test or certification run proves observed behavior; it does not prove singular ownership. Every milestone therefore includes both behavioral tests and an ownership/reachability check.

## 3. Current implementation baseline

The implementation starts from these production seams:

- `src/LoopRelay.Cli/Program.cs` enters `UnifiedCliComposition.CreateProduction`, then `CanonicalCliApplicationService`, `WorkflowChainRunner`, `WorkflowController`, `TransitionRuntime`, and `TransitionEffectCoordinator`.
- `src/LoopRelay.Orchestration.Primitives/Workflows/CanonicalWorkflowDefinitionSketches.cs` builds four workflows and two chains, but is provisional, repeatedly constructed, lacks complete output-surface contracts, uses string validators, and declares feature-authored Git/publication effects.
- `src/LoopRelay.Orchestration.Primitives/Runtime/TransitionRuntime.cs` already executes one authorized attempt with durable attempts, read receipts, composed prompt facts, dispatch lifecycle, normalized output, candidates, validation, freshness, atomic promotion, and effect intents.
- `src/LoopRelay.Orchestration.Primitives/Runtime/TransitionEffectCoordinator.cs` immediately walks the current attempt's effects. It has no durable work scanner, lease, restart worker, typed executor registry, receipt model, or unknown-outcome reconciler.
- `src/LoopRelay.Orchestration.Primitives/Runtime/TransitionFaultsAndRecovery.cs` and `src/LoopRelay.Orchestration.Primitives/Recovery/` implement two overlapping recovery models. Decision continuity, warm Plan/Execute checkpoints, transition recovery, effect recovery, and completion recovery are not one authority.
- `src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs` owns the canonical `looprelay.workspace-state` / `CanonicalWorkspace` logical-v9 physical manifest and convergence logic. New durable contracts must evolve this single logical sequence; they must not create a feature database.
- `src/LoopRelay.Orchestration.Primitives/Persistence/CanonicalPersistenceReadModel.cs` exposes only workflow persistence and chain boundaries. `RepositoryObserver` and `CanonicalCliApplicationService` still read physical tables and compatibility files directly.
- `src/LoopRelay.Cli/Services/Application/ApplicationBoundaryContracts.cs` has a partial internal boundary. Its result vocabulary collapses effect-pending, recovery-required, human-decision-required, unsupported-capability, compatibility-import-required, and specific cannot-proceed causes.
- `src/LoopRelay.Cli/Services/Cli/UnifiedCliRunner.cs` performs migration, direct SQL, storage mutation, run-loop progression, status assembly, and completion cleanup.
- `src/LoopRelay.Cli/Services/Cli/UnifiedCliComposition.cs` contains composition plus workflow-specific prompt execution, product interpretation/materialization, feature branching, Git publication, archive execution, checkpoint cleanup, progression, and effect execution. These nested bodies must move to their owners.
- `src/LoopRelay.Completion/` contains useful completion policy, prompt, evidence, and archive mechanisms, but completion decisions and mutations are split between that project and CLI feature bodies.
- `src/LoopRelay.Plan.Cli/`, `src/LoopRelay.Roadmap.Cli/`, `LoopRunner`, `ExecutionStep`, `CommitGate`, feature-specific continuity stores, and their tests remain parity-only legacy surfaces. They receive no new production authority and are deleted only at their named parity gates.

Before the first implementation commit, capture a clean baseline with `dotnet build LoopRelay.slnx --no-restore` and `dotnet test LoopRelay.slnx --no-restore`. Record exact counts and skipped-test reasons in machine evidence for the commit; do not encode counts as permanent product constants.

## 4. Non-negotiable architecture contracts

### 4.1 Authority and causality

- Every supported behavior has exactly one owner and one production change location.
- Preserve the opaque causal spine `workspace -> root run -> workflow instance -> transition run -> attempt -> session -> turn`. Effects, recovery cases/actions, interactions, completion decisions/plans, products, prompt facts, and storage/import operations add stable identities linked to that spine.
- Ledger insertion order is authoritative. Wall-clock time and ULID lexical order are diagnostic only.
- Collaboration files under `.agents/**` are filesystem-authoritative and read at use. Each consumption records commit, normalized relative path, per-file SHA-256, aggregate surface hash, validation, and causal lineage.
- System facts are ledger-authoritative. Files representing history, evidence, status, or recommendations are projections or import inputs and never outrank ledger facts.

### 4.2 State, prompts, policy, and runtime

- Raw configuration resolves to validated configuration with provenance. Policy Authority then resolves attempt/session policy using configuration, workspace facts, catalog policy, capabilities, and recommendations. Runtime consumes only the resolved policy/profile.
- Remove direct `BrainConfiguration` use from `AgentSpecs`, workflow handlers, and provider adapters. Keep it only as a configuration input translated by Policy Authority.
- Every provider-visible byte is composed from an invariant prompt template, a versioned prompt-policy profile, and consumed inputs before hashing and persistence. Transport loads by rendered-prompt-fact identity and may not append or alter instructions.
- Prompt fact plus `Planned` and `Authorized` dispatch lifecycle facts must be durable before `Started` or any provider write.
- A thrown or missing normalized result after dispatch start is unknown provider work and enters recovery; it never authorizes a blind resend.
- Execution recommendations are immutable causal evidence. Only a durable policy evaluation may produce an effective runtime profile, and recommendations can never raise permission, approval, sandbox, provider, or network ceilings.
- Exact Codex version plus app-server schema determines supported resume/read/fork behavior. Unsupported exact capability fails closed; there is no provider fallback.

### 4.3 Outcomes

Use one reason-bearing outcome model across kernel, persistence, read model, application result, renderer, and exit mapping. It must distinguish:

- completed/certified;
- waiting/paused;
- failed;
- cancelled;
- stalled;
- ambiguous/conflicted;
- input invalidated/concurrent state conflict;
- effect pending;
- recovery required;
- human decision required;
- unsupported provider capability;
- compatibility import required;
- missing runtime prerequisite; and
- specific cannot-proceed reasons such as missing input, dirty input, unversioned input, unusable storage, corrupt storage, unsupported schema, and invalid response.

Generic `Blocked` and `OperatorUnblock` are not canonical outcome values. They may exist temporarily only in import/compatibility translation. Replace `CompletionCertificationServiceOutcome.Blocked`, `NonImplementationCompletionReviewStatus.Blocked`, `LoopOutcome.CompletionBlocked`, and `TransitionRecoveryDisposition.OperatorUnblock` with a specific reason-bearing result before Completion closes. Cannot-proceed state is derived from current evidence, not a manually cleared latch.

Exit mapping remains explicit and tested: completed and passive waiting use `0`; failure uses `1`; syntax/composition input errors use `2`; stalled uses `3`; ambiguous, effect-pending, recovery-required, human-action-required, import-required, unsupported-capability, and specific cannot-proceed use `4`; cancellation uses `130`. Sharing an exit code must not collapse the typed application discriminant or reason.

### 4.4 Effects and recovery

- Authoritative state and effect intents commit atomically; outward work happens afterward.
- One intent represents exactly one semantic mutation and carries one stable idempotency key, typed target, executor version, preconditions, postconditions, ordering/dependencies, requiredness, and reconciliation policy.
- `Unknown` is never treated as `Planned`. Independent observation must reconcile it before repeat.
- Local output-surface commit is blocking for transition progress. Remote push is a separate required-asynchronous effect: a transition may report it pending, but readiness or certified closure cannot settle until it succeeds.
- Recovery classification reads durable evidence only. The selected plan is immutable and durable before any recovery action. A retry preserves the transition-run identity, creates a new attempt identity, and never mutates the source attempt.
- Caller cancellation stops new work but terminal evidence uses a non-cancelled token. Work that may already have escaped the process is reconciled.

### 4.5 Storage, import, and reads

- Schema identity, family, logical version, and physical-shape fingerprint are interpreted together.
- Do not mutate the physical v9 manifest without a logical-version transition. The first new durable contract creates v10; later milestones increment the logical version whenever the durable semantic model changes. Every migration has fresh-create, prior-version upgrade, interruption, retry, corruption, identity-preservation, and idempotent-second-pass tests.
- `status`, `storage verify`, import detection, and import preview are strictly read-only and byte-for-byte non-mutating. Required migration is reported as an action, not performed implicitly.
- Import is explicit and one-way. No dual write, silent merge, fabricated historical evidence, or runtime fallback follows a successful import.
- Read models are immutable projections. They never repair, migrate, or write implicit defaults; missing evidence remains explicit unknown state.

## 5. Required owner decisions

These decisions are part of the implementation contract. Record each accepted ruling in an enduring ADR and encode it in executable tests before the blocking milestone begins.

| ID | Decision and recommended ruling | Blocks |
|---|---|---|
| D1 | Keep template + versioned prompt-policy profile + consumed-input composition before hashing. Remove any test or code that assumes policy prose is appended after render. | M8 entry |
| D2 | Use specific reason-bearing cannot-proceed outcomes. Permit generic `Blocked` only in compatibility translation and prose. | M8 entry, M15 |
| D3 | Keep canonical logical-v9 as the starting state model, with raw configuration and resolved policy as separate authorities. Evolve through v10+ rather than creating feature stores. | M8 entry |
| D4 | Require durable rendered-prompt facts and pre-dispatch lifecycle facts before every production send. | M8 entry |
| D5 | Cancellation salvage: before dispatch, record Cancelled with no provider work; after dispatch start, record Cancelled plus a recovery case and reconcile; after validated output, retain candidates and continue deterministic work only if inputs remain fresh; during effects, stop scheduling and reconcile any started effect; during completion, keep completed effects and leave closure pending/recoverable. | M9 |
| D6 | Interaction policy is category-specific and explicit. Dirty-input offers, import conflicts, recovery ambiguity, and completion ambiguity have no timeout default; headless mode returns a typed fault. Persist trust/authorization evidence whenever a response authorizes mutation. Use compare-and-set response isolation and one active resolution per request. | M10 |
| D7 | `status` is strictly read-only and reports migration/convergence required. Add an explicit storage migration/convergence command. | M11, M16 |
| D8 | Supported import portfolio is: canonical v8/recognized partial-v9 schema convergence; LegacyContinuity v3; pre-unification roadmap state and its decision, artifact lifecycle, split-family, selection/projection, execution-preparation, and transition-journal facts; partial planning artifacts; decision sessions; numbered histories; and completion archives. Unknown or mixed ambiguous formats fail closed and require an interaction response or an explicit adapter decision. | M12 |
| D9 | Release claims that must survive machines use a scrubbed durable evidence owner outside ignored `.tmp`; local `.tmp` remains disposable diagnostic evidence. If cross-machine release provenance is not required, the read model must state that the claim is local-only. | M16, M21 |
| D10 | Promote an exact Codex profile only after static protocol fixtures and live capability evidence pass. Retire only after no active durable lineage references it and a replacement path is proven. | M9, M16 |
| D11 | `Planning/CreateNewRoadmap` remains unavailable until a complete product contract and workflow transition are accepted. Do not wire a reserved asset merely because it exists; delete it at retirement if no intent is accepted. | M17 |
| D12 | On Plan restart, exact-profile mismatch may use a certified reconstruction mechanism; otherwise return unsupported-capability/human-action-required. Never silently create a fresh warm session. | M18 |
| D13 | Execute order is catalog-declared: readiness -> decision and authorization -> implementation -> handoff/context -> publication -> repository/milestone evaluation -> non-implementation review -> completion decision/closure. Continue to another slice only from an explicit nonterminal completion route. | M19 |

If an owner rejects a recommended ruling, update the enduring ADR, this plan's affected acceptance criteria, and tests in the same change. Do not create a second decision docket.

## 6. Dependency and commit sequence

Implement and accept in this order:

```text
Baseline contract ratification
  -> M8 Effect Coordinator
  -> M9 Recovery Coordinator
  -> M10 Interaction Broker
  -> M11 Workspace Storage Authority
  -> M12 Import Gateway
  -> M13 Workflow Catalog
  -> M14 Orchestration Kernel
  -> M15 Completion Authority
  -> M16 Canonical Read Model
  -> M17 Roadmap convergence
  -> M18 Plan convergence
  -> M19 Execute convergence
  -> M20 Application Boundary
  -> M21 Retirement completion
```

Additional hard edges are M8 + M10 -> M11, M9 + M12 -> M13, M10 + M13 -> M14, M8 + M9 + M14 -> M15, M12 + M15 -> M16, and M15 + M18 -> M19.

Use one reviewable commit per completed vertical slice unless a slice needs an explicitly temporary preparatory commit. Temporary adapters must name their owner, callers, evidence, and deletion milestone in code. Do not merge two implementations that both claim ownership of the same operation.

## 7. Phase 0 — baseline contract ratification

### Implementation

1. Add or update enduring ADRs for D1-D4 and D2's obstacle vocabulary.
2. Add architecture tests asserting:
   - provider sends require a persisted rendered-prompt fact and authorized dispatch identity;
   - transport receives identity-only prompt input and cannot append provider-visible content;
   - runtime consumes a resolved policy/runtime profile rather than raw configuration or recommendations;
   - canonical outcome enums contain no new generic blocker/latch values; and
   - fresh and upgraded canonical databases agree on identity/family/version/shape.
3. Replace stale tests that encode post-render policy append, optional prompt evidence, or a public `unblock` latch.
4. Capture build, full component suite, CLI component suite, and static exact-profile compatibility results.

### Exit gate

- D1-D4 are accepted and executable.
- Active canonical tests agree on prompt, policy, schema, dispatch, and outcome vocabulary.
- Build and component suites pass without unexpected warnings.
- No M8+ authority is claimed by this gate.

## 8. M8 — Effect Coordinator

### Target design

Create `Effects/` contracts and services in `LoopRelay.Orchestration.Primitives`; keep OS/Git/filesystem adapters in `LoopRelay.Infrastructure`; move feature-specific effect handlers out of `UnifiedCliComposition` and `LoopRelay.Completion` mutation paths.

Core contracts:

- `EffectIntent`: effect ID, causal spine, semantic operation key, executor key/version, target descriptor, typed payload hash, ordering/dependencies, `BlockingLocal` or `RequiredAsync`, precondition, postcondition, reconciliation strategy, and idempotency key.
- `EffectLifecycle`: Planned, Leased, Started, Pending, Succeeded, Failed, Stalled, Cancelled, Unknown, Reconciling, RetryAuthorized, and HumanActionRequired.
- `EffectReceipt`: immutable receipt ID, intent ID, executor/version, observed target identity, before/after facts, postcondition verdict, external correlation, and evidence IDs.
- `IEffectWorkStore`: scan, lease with compare-and-set row version/expiry, append lifecycle event, record receipt, and settle a plan transactionally.
- `IEffectExecutorRegistry` and one typed executor per semantic operation; `IEffectReconciler` observes the target independently.
- `EffectWorker` scans all unsettled required work at process start and before/after kernel cycles. Only one worker is scheduled now, but durable leases make crash recovery deterministic.

### Persistence

1. Introduce logical schema v10 with versioned migration from v9.
2. Evolve `canonical_effect_intents` rather than creating a competing effect store. Add causal workspace/run/workflow-instance fields, executor identity/version, target and payload documents/hashes, requiredness, dependency document, pre/postcondition documents, row version, lease owner/expiry, attempt count, and terminal receipt reference.
3. Replace `canonical_effect_records` as semantic state with an append-only lifecycle event stream; migrate existing rows without inventing unobserved receipts. Add immutable receipt and reconciliation-attempt tables.
4. Add indexes for unsettled scan order, lease expiry, transition/attempt, semantic operation key, and receipt lookup.
5. Split `CanonicalWorkflowPersistenceStore.RecordEffectIntentStateAsync`: the effect store owns effect lifecycle, while a settlement transaction owned by workspace state derives attempt/transition progress only after required postconditions are verified.

### Routing and extraction

1. Replace `TransitionEffectCoordinator.CoordinateAsync(TransitionRuntimeResult, ...)` with intent discovery/coordination by durable plan or transition identity. Never depend on the in-memory attempt result to find work.
2. Replace `UnifiedEffectExecutor` branching with registered executors for:
   - product/evidence materialization;
   - filesystem artifact write/move/archive;
   - projection materialization;
   - nested `.agents` commit;
   - nested `.agents` push;
   - parent repository commit/gitlink update;
   - parent push;
   - export package write; and
   - lifecycle/checkpoint cleanup where modeled as an effect.
3. Make each executor perform exactly one semantic mutation. Remove methods that both materialize multiple artifacts and advance workflow/stage state.
4. Keep current workflow-authored publication intents temporarily, but translate them through the new protocol. M13 removes this temporary declaration duplication and synthesizes Git effects from output surfaces.
5. Route `AgentsSubmodulePublisher`, `CommitGate`, archive writes, `HistoryProjectionEffectRunner`, `LoopArtifacts` rotations, and direct completion/feature filesystem mutation behind typed effects. Leave no supported direct call site after parity tests pass.

### Verification

- Fault inject before call, after outward mutation/before receipt, after receipt/before settlement, between ordered effects, during cancellation, and during process restart.
- Prove duplicate scanning and expired leases cause at most one semantic mutation.
- Reconcile a completed local commit after receipt loss without a second commit.
- Leave push pending while the remote is unavailable and prove transition/Plan/completion settlement remains disallowed where required.
- Verify receipts against independent Git/filesystem observation.
- Run component suites and applicable publication certification cases.

### Exit gate

Every required mutation is discoverable after restart, ordered, idempotent, receipted, independently reconcilable, and unable to produce a false completion claim. No supported feature body directly performs a required external mutation.

## 9. M9 — Recovery Coordinator

### Target design

Unify `TransitionRecoveryCoordinator`, the richer `RecoveryRuntime`, `DecisionSessionRecoveryCoordinator`, Plan/Execute warm continuity, effect recovery, and completion recovery behind one case/classification/plan/action model in `src/LoopRelay.Orchestration.Primitives/Recovery/`.

1. Define `RecoveryCaseIdentity`, scope kind, causal subject, durable-boundary classification, observed source facts, immutable plan, action journal, and terminal disposition.
2. Canonical classifications must distinguish NotStarted, InFlight, AcceptedUnknown, SucceededUncommitted, Failed, Cancelled, ProviderUnknown, PartiallyEffected, CompletionPartiallyClosed, Corrupt, and EvidenceIncomplete.
3. Plan actions are ReconcileProvider, ReconcileEffects, ResumeSession, ReconstructContext, NativeFork, ReuseRawOutput, RetryNewAttempt, Compensate, Wait, or RequestHumanDecision.
4. The classifier is pure over durable facts. The planner applies D5, D10, resolved policy, and exact capability evidence, persists the plan, then the runtime executes only that plan.
5. Reuse the same root run/workflow instance/transition run where semantically appropriate. A new provider attempt always receives a new attempt ID and preserves the source attempt unchanged.
6. Add typed application use cases for recovery inspect, plan, and execute. They may initially be adapted by the current CLI but must not contain recovery logic there.

### Persistence and migration

- Evolve the schema for recovery cases, immutable classifications, plans, source-evidence links, and action events.
- Import existing `transition_recovery_plans` and `session_recovery_*` facts into the unified projection without losing IDs. Keep legacy tables read-only behind a migration adapter until verification proves parity, then remove their runtime readers.
- Make cancellation terminal evidence durable even when the caller token is cancelled.

### Verification

- Build a matrix covering every durable transition boundary, prompt lifecycle boundary, effect boundary, warm-session boundary, decision turn, and completion-closure boundary.
- Prove no recovery command can bypass classification or reconciliation.
- Prove exact-profile denial fails closed and selects only an allowed reconstruction/human path.
- Restart during every recovery action and resume the same action idempotently.
- Assert `OperatorUnblock` and workflow-local retry loops are unreachable.

### Exit gate

Every interruption has one evidence-based classification and persisted plan; no recovery depends on an undiscoverable in-memory object; cancellation and unknown work remain distinct from failure and not-started.

## 10. M10 — Interaction Broker

### Target design

Add `Interactions/` to `LoopRelay.Orchestration.Primitives` with:

- typed category and request IDs;
- question/presentation data separated from a versioned response JSON schema and schema hash;
- causal subject, creation evidence, resolved category policy, deadline/default policy, and required trust/authorization evidence;
- append-only Presented, Responded, Rejected, Expired, Defaulted, Cancelled, and Resolved events;
- immutable response IDs and semantic idempotency keys; and
- broker commands to create, list, show, respond, cancel, and resolve.

The store persists a request before any renderer can present it. A semantically identical duplicate response returns the existing response; a conflicting duplicate, late response, or schema-invalid response is a typed rejection and leaves the request unchanged. Kernel/recovery resume only from a validated resolved-response fact.

### First production integration

1. Replace the clean-input gate's current dirty-surface result for interactive invocations with a durable `DirtyInputCommitOffer` request that names the exact declared surface and Git evidence.
2. On acceptance, create a scoped commit effect through M8; on rejection, return the specific dirty-input result. Never commit directly from the broker.
3. In headless mode, return `DirtyInputSurface` immediately with no request, no commit, and no indefinite wait.
4. Add application/CLI list, show, and respond paths. Workflow handlers must not read stdin or console.

### Persistence and tests

- Add request, response, lifecycle-event, and policy-evaluation tables linked to the causal spine.
- Test restart with an outstanding request; valid, invalid, late, expired, identical duplicate, and conflicting duplicate responses; compare-and-set conflicts; cancellation; headless behavior; and renderer purity.
- Add an architecture test rejecting console/input dependencies from workflow, kernel, recovery, effect, completion, storage, and import assemblies.

### Exit gate

Every required human action has a stable request identity, exact response contract, visible policy, and restart-safe resolution. Status can name the action without relying on ephemeral console state.

## 11. M11 — Workspace Storage Authority

### Contracts

Place logical storage operation contracts under `LoopRelay.Orchestration.Primitives/Storage/`; keep raw SQLite inspection/migration primitives in `LoopRelay.Core/Services/Persistence/`.

- `storage verify`: strictly read-only inspection of existence, bytes, identity, family, version, physical shape, corruption, unsupported version, unresolved references, and interrupted operations.
- `storage init`: create a fresh canonical workspace only when no authority exists; ambiguous/existing state is a typed refusal.
- `storage migrate`: explicitly plan and execute a supported schema upgrade/convergence through M8/M9.
- `storage export`: emit a versioned semantic package, manifest, hashes, and logical fingerprint through effects.
- `storage sync`: reconcile rebuildable projections/effect work with canonical facts; it is never bidirectional legacy import.
- `storage import`: delegate source interpretation and one-way import to M12.

### Implementation

1. Split `LoopRelayWorkspaceDatabase` into read-only inspector, version manifest/migration catalog, connection factory, and migration executors while preserving one schema authority.
2. Make inspection APIs impossible to open a read-write connection. Byte-hash the workspace before and after verify/status tests.
3. Add durable storage-operation plan/event/receipt records. Persist the plan before database/filesystem mutation; execute through typed effects; recover through M9.
4. Define a versioned export DTO for every logical domain, explicit null/unknown historical fields, and stable semantic fingerprint independent of row order and SQLite bytes.
5. Implement export -> fresh import -> canonical projection comparison. A file-format match alone is insufficient.
6. Remove migration and direct SQL from `CanonicalCliApplicationService`/`UnifiedCliRunner`. Remove the current storage commands that merely ensure schema or write `workspace_metadata` while claiming import/export/sync.
7. Change startup/status to inspect and return `MigrationRequired`; only `storage migrate` may mutate.

### Verification and exit gate

Test healthy v9+, v8 migration required, recognized partial-v9 convergence, unknown/stamped-incomplete schema, corruption, interrupted init/migrate/sync, repeated operations, and semantic export round-trip. Verification and status must preserve every byte. No application or observer code may issue SQL. Command labels and typed results must exactly describe performed work.

## 12. M12 — Import Gateway

### Portfolio and adapters

Implement ingress-only adapters for D8. Distinguish schema convergence from domain import:

- Canonical v8 and recognized partial-v9 shapes use Storage Authority migration/convergence.
- LegacyContinuity v3 maps session scopes, lineage, turns, recovery plans/attempts, and correlation into canonical facts.
- Pre-unification roadmap adapters map roadmap state, decision ledger, artifact lifecycle, split families/order, selection provenance, projection manifests, execution preparation, transition journal, and compatible history/evidence.
- Planning adapters detect incomplete plan, detail, milestone, operational-context, projection, and publication surfaces.
- Execute adapters detect decision sessions, numbered histories, handoffs, evidence, and completion archives.

### Lifecycle

1. Detect source kind/version/fingerprint read-only.
2. Produce a durable preview with complete domain identity mapping, conflicts, unsupported facts, unknown fields, and semantic delta. A source change invalidates the preview.
3. Require explicit approval; ambiguity creates an M10 request and never guesses.
4. Persist an import plan and execute all-or-nothing canonical writes/effects.
5. Compare logical source and target projections. Preserve source identities when valid; map with durable correspondence when not. Leave unobserved historical fields null.
6. Commit a receipt only after semantic verification, then write a monotonic canonical-only marker and mark the source non-authoritative.
7. Guard runtime source selection: once canonical-only is set, any legacy reader invocation is a defect and fails tests.
8. Track portfolio/adapter exhaustion. Delete an adapter only after every owned fixture imports and runs canonical-only with the adapter disabled.

### Persistence and verification

Add detection, preview, source-fingerprint, mapping, operation/event, verification, receipt, and canonical-only facts. Reuse existing compatibility operation facts through a migration/projection adapter, not as a second store.

For each portfolio fixture test no-write detection/preview, ambiguity, conflict, malformed input, rollback, crash/restart, semantic fidelity, receipt idempotency, no dual write, and canonical-only runtime. The acceptance fixture disables/removes the legacy reader after import and proves behavior is unchanged.

## 13. M13 — Workflow Catalog

### Catalog model

Replace `CanonicalWorkflowDefinitionSketches` with a single `CanonicalWorkflowCatalog` snapshot constructed once and injected everywhere. Its stable identity is a canonical hash over semantic declarations; its explicit version changes whenever semantics change. Persist catalog identity/version on root runs and workflow instances.

Extend declarations with typed, validated contracts for:

- workflow, stage, transition, product, product schema/version, entry/exit, successor, and terminal outcome;
- required input products and complete filesystem input surfaces;
- complete output surfaces with repository target (`Workspace`, nested `Agents`, or parent gitlink), mutation kind, ownership, validation, commit policy, and push policy;
- typed validator identities resolved from an owner registry, replacing ungoverned strings;
- prompt template identity, prompt-policy profile requirement, execution posture, resolved-policy requirements, and exact runtime capabilities;
- explicit interaction categories, effect categories, recovery strategies, and completion behavior; and
- gate requirements, warnings, conflicts, unsupported cases, and specific failure outcomes.

### Validation and derivation

1. Validate unique identities, references, product schemas, graph reachability, cycles, stage/transition successors, entry/exit compatibility, all terminal paths, prompt assets, policy/profile requirements, runtime capabilities, validator ownership, and effect/recovery ownership.
2. Require every disk read and write to be covered by a normalized repository-relative surface with no root escape.
3. Derive blocking commit and required-asynchronous push effects from output surfaces. Workflow authors may declare domain mutations but may not repeat Git publication mechanics.
4. Include derived effects in catalog identity and the production obligation ledger.
5. Construct the snapshot once in the composition root. Kernel, resolver, prompt asset lookup, effect coordinator, and certification all consume that exact instance.
6. Add a deterministic obligation enumerator over catalog, prompt assets, exact profiles, schema manifest, known risks, effects, products, and chains. A one-item semantic change must produce one stable changed obligation rather than reorder the denominator.

### Verification and exit gate

Build an invalid-catalog corpus for dangling references, duplicate IDs, unsupported capabilities, unowned validator, missing input/output surface, output without generated publication effects, cycles, unreachable terminal, missing prompt asset, and ambiguous successor. Production startup must return all validation errors before workspace access. All four workflows and both chains resolve from one catalog, and adding a fixture workflow requires declarations/handlers only—no kernel branch.

## 14. M14 — Orchestration Kernel

### Kernel lifecycle

Create a kernel coordinator, using the existing resolver/runtime/controller/chain components where their contracts remain valid, that owns this universal sequence:

```text
observe -> resolve -> gate -> interact -> authorize attempt -> dispatch one attempt
-> interpret -> validate -> freshness check -> atomic product/state/effect intent
-> coordinate/reconcile effects -> recover if required -> reobserve -> chain -> project
```

### Implementation

1. Move fresh-attempt authorization from `WorkflowChainRunner` into the kernel. Select fresh versus recovered authorization from durable run state and M9 plans.
2. Re-enter an existing active root run/workflow instance after restart instead of minting a new run for every CLI invocation. New successor workflows receive new workflow-instance IDs under the same root run.
3. Keep `TransitionRuntime` single-attempt and single-dispatch. It must not gain retry, recovery, effect execution, interaction presentation, or chaining policy.
4. Make all causally required writes fail closed: run/workflow-instance creation, policy resolution, attempt intent, receipts, prompt/dispatch facts, candidate/evaluation facts, promotion/effect intent, recovery/interaction/chain facts. Remove best-effort catches around required run and policy writes.
5. Reobserve through owner projections after every attempt/effect/recovery/interaction cycle. A prompt or handler output can only create candidates; only the atomic commit store can promote products/state.
6. Move unbounded continuation guards, progression, successor choice, and completion-route looping from `CanonicalCliApplicationService` and `UnifiedEffectExecutor` into catalog-driven kernel decisions.
7. Extract nested prompt handlers, context builders, interpreters, product validators, and local artifact handlers from `UnifiedCliComposition` into owner modules. They may contain workflow-specific transformation logic but no progression, policy, persistence selection, effects, recovery, or prompt framing.
8. Replace `RepositoryObserver` raw SQL and compatibility heuristics with canonical domain projections plus independent filesystem/Git observation.
9. Expose one typed kernel command/result containing outcome, causal identities, evidence, pending effects, recovery case, interaction request, and snapshot identity.
10. Inventory every agent role in CLI, completion, projection, decision, review, and roadmap/plan/execute handlers. Replace raw `BrainConfiguration` constructor inputs with a durable role/session policy resolved by Policy Authority and linked to the attempt.
11. Move `CODEX_HOME` and other ambient provider inputs behind validated configuration resolution with provenance. No handler or decision path may read ambient environment state directly.
12. Replace provisional `runtime_cli_application` and `prompt_policy_cli_application` literals with identities produced by the resolved runtime profile and prompt-policy profile. Reject missing profile facts before attempt authorization.
13. Stop synthesizing adaptive capability evidence. Provider operations are authorized only by the exact compatibility profile actually observed for that executable/app-server schema.
14. Keep telemetry, usage-limit wait/retry, input-wait reporting, runtime prerequisite checks, and terminal session evidence as composed Runtime/Policy services. Add production-composition tests proving configured enable/disable values change the active wrapper graph or are rejected.

### Verification and exit gate

Fault-inject after every durable lifecycle phase for both chains. Restart must preserve lineage and avoid duplicate provider/effect work. Test every success/non-success outcome, freshness conflict, no eligible transition, ambiguous selection, interaction, recovery, chain boundary, and required-write failure. Architecture tests must prove no feature runner or client advances canonical state and there is one reachable production kernel.

## 15. M15 — Completion Authority

### Decision and closure contracts

Consolidate completion in `LoopRelay.Completion` behind:

- `CompletionDecision`: CertifiedCandidate, Continue, Waiting, Failed, Cancelled, or SpecificCannotProceed with evidence/gate/review identities;
- `CompletionCertificate`: immutable certificate only for a valid CertifiedCandidate;
- `CompletionClosurePlan`: immutable ordered operations and dependencies; and
- `CompletionSettlement`: EffectsPending, RecoveryRequired, Failed, Cancelled, SpecificCannotProceed, or CertifiedTerminal.

Exactly one service decides completion. Decision is pure with respect to external mutation. Persist the decision/certificate and complete closure plan before executing closure effects.

### Ordered closure

The catalog/plan builder must explicitly order:

1. archive materialization and semantic verification;
2. roadmap completion-context update/materialization;
3. nested `.agents` commit and required push when that surface changed;
4. parent gitlink/repository commit and required push when applicable;
5. completion-route evidence and independent postcondition checks;
6. decision, warm-session, and certification-checkpoint retirement; and
7. certified terminal fact.

The final fact is unavailable while any required effect is pending, unknown, failed, or stalled. Completed effects are never rolled back merely because a later effect failed; M9 resumes/reconciles the same closure plan.

### Refactoring

1. Replace generic completion/non-implementation blocker enums per D2.
2. Move archive, context, Git, cleanup, and terminal mutations out of `CompletionCertificationService`, `UnifiedPromptExecutor`, `UnifiedEffectExecutor`, and CLI cleanup into typed M8 executors.
3. Delete direct `CommitGate` usage from completion.
4. Replace opaque completion checkpoint metadata with typed decision/plan/effect identities.
5. Add a dedicated completion subprojection consumed by M16.

### Verification and exit gate

Test certified, continue, each specific cannot-proceed reason, failed, cancelled, effect-pending, and recovery-required through persistence, read model, application result, renderer, and exit code. Fault-inject at every closure step, including archive success/push failure and unknown Git result. After settlement, rerun must create zero provider sessions/turns/effects and no user-tree or Git mutation. Only then may `CertifiedCompletion` be promoted.

## 16. M16 — Canonical Read Model

### Model

Replace the narrow `CanonicalPersistenceReadModel`/`CanonicalCliStatusSnapshot` assembly path with one immutable `CanonicalWorkspaceSnapshot` composed from typed owner projections. Include:

- workspace/schema/storage health, migration/import state, canonical-only marker, and compatibility uncertainty;
- invocation mode, chain alternatives, selection conflicts, selected workflow/stage/transition, root/workflow/transition/attempt lineage, and terminal classification;
- products, gates, warnings, read receipts, freshness, and exact evidence identities;
- raw configuration identity/provenance summary, resolved policy, recommendation evaluation, effective runtime profile, exact provider profile/capabilities, and prerequisite findings;
- rendered prompt/dispatch/session/turn evidence without exposing secret/provider payload content;
- effect plans, states, receipts, unknowns, dependencies, and pending required pushes;
- recovery classification/plan/action and allowed next operation;
- outstanding interaction request identity, category, response schema, policy, deadline, and state;
- completion decision, closure plan, pending operations, certificate, and terminal fact; and
- certification obligations with exact credited evidence tier/version or explicit uncredited status.

Every claim carries an evidence/source identity or an explicit `Unknown` reason. Conflicting owner projections surface ambiguity; the composer does not choose a winner.

### Implementation

1. Define one projection interface per owner; each reads only its canonical store/contracts. The aggregate composer may join projections but may not reinterpret domain semantics.
2. Remove raw store/table queries from `CanonicalCliApplicationService`, `LedgerEvidenceRetrieval`, `RepositoryObserver`, and formatters.
3. Make status/export renderers pure functions of the snapshot and test them with no repository/database/provider dependencies.
4. Add stable source watermarks/snapshot identity so consumers can detect staleness without treating the snapshot as authority.
5. Extend certification to emit stable obligation evidence links keyed by catalog/schema/profile/asset version. A campaign-level pass never silently credits every obligation.
6. Implement D9 and D10's evidence/profile lifecycle in the release subprojection.

### Verification and exit gate

Query a fixture containing a pending push, unknown effect, recovery plan, interaction, migration-required storage, import conflict, and partial completion closure. One snapshot must expose all identities/actions. Render twice and prove byte/storage non-mutation. Trace every displayed claim to a fact. Read-model rebuild after restart must be semantically identical and must not repair state.

## 17. M17 — Roadmap capability convergence

### Implementation

1. Express Traditional and Eval Roadmap progression exclusively in the M13 catalog and M14 kernel. Move only candidate construction, parsing, and validation behavior from `LoopRelay.Roadmap.Cli` into canonical handlers/owners.
2. Enforce one shared, versioned schema and gate contract for `PreparedEpic` and `MilestoneSpecificationSet`; preserve producer provenance without allowing downstream Plan behavior to branch on the producer.
3. Route every prompt through the canonical prompt gateway and every mutation/review/recovery/interaction through its owner.
4. Validate and either fully implement or remove unavailable declarations for `CreateEvalDependencyInventory`, `CreateEvalHypothesisInventory`, and `CreateEvalDag`. Apply D11 to `Planning/CreateNewRoadmap`.
5. Port useful Roadmap tests to canonical project tests before deletion: state/resume, selection, split-family lineage, promotion, prompt contracts, transition journaling, storage/import, projections, failure persistence, and completion routing.
6. Prove no new run or recovery command can enter `LoopRelay.Roadmap.Cli` or its readers/state machines.
7. After component and live parity plus owner acceptance, delete `src/LoopRelay.Roadmap.Cli/`, its test project, solution/project references, publish artifacts/scripts, and last-only prompt/readers/assets.

### Exit gate

Traditional and Eval routes produce the same validated downstream products/gates under canonical authorities, with distinct provenance only. Default and forced selection, recovery, publication, and downstream Plan entry pass. Building and running after physical deletion changes no supported behavior.

## 18. M18 — Plan capability convergence

### Implementation

1. Encode Plan's ordered lifecycle in the catalog: warm authoring -> adversarial projection -> read-only review -> warm revision -> operational context/details/milestones -> refinement -> publication -> readiness.
2. Move warm continuity behind M9. Bind checkpoints to session/turn, exact profile, prompt facts, input receipts, plan hash, and causal spine. Apply D12 on restart.
3. Use a scoped artifact transaction for candidate Plan files. Snapshot only declared output surfaces; failed validation restores the candidate surface and leaves promoted products unchanged. Record rollback evidence through effects.
4. Promote `ExecutablePlan`, `OperationalContext`, `ExecutionDetails`, and `ExecutionMilestoneSet` only after schema/gate/freshness validation.
5. Derive publication from declared output surfaces. Order nested `.agents` materialization/commit/push before the parent gitlink/repository commit/push. Reconcile receipts against both repositories.
6. Promote `ExecutionReadiness` only after every required product, gate, blocking commit, and required push has settled.
7. Port useful Plan tests for preflight, authoring, review/revision, rollback, milestones, warm restart, independent `.agents` topology, and publication to canonical owners.
8. After parity and owner acceptance, delete `src/LoopRelay.Plan.Cli/`, its test project, solution/project references, publish scripts/artifacts, and Plan-only shims.

### Exit gate

Both Roadmap producers enter identical Plan contracts; restart needs no lost object; failed revision preserves the prior products; nested and parent repository observations equal effect receipts; readiness cannot be observed early; deletion changes no supported behavior.

## 19. M19 — Execute capability convergence

### Implementation

1. Encode D13 in the catalog, including explicit nonterminal routes back to decision/readiness and the only terminal route into M15.
2. Bind decision products, recommendation evidence, policy evaluation, effective runtime profile, prompt fact, and input manifest into one `ExecutionAuthorization`. Remove all paths where a recommendation or raw model/effort reaches `AgentSpecs` directly.
3. Make decision, implementation, handoff, operational-context update, publication, repository evaluation, milestone evaluation, non-implementation review, and completion handlers candidate/evidence producers only.
4. Derive stall/no-substantive-change from current Git/product/history evidence. Delete counters/latches that require manual unblock.
5. Route every filesystem/Git mutation through M8, every retry/resume/fork through M9, every human decision through M10, and every completion decision/closure through M15.
6. Apply D5 at every provider/effect boundary. Unknown decision, implementation, handoff, publication, or completion work reconciles before repeat.
7. Preserve all outcome discriminants through M16 and the application boundary.
8. Port useful `LoopRunner`, `ExecutionStep`, `CommitGate`, history, milestone, decision-session, recovery, handoff, and completion tests to canonical owners.
9. After both full chains, boundary-fault campaigns, and owner acceptance pass, delete `LoopRunner.cs`, `ExecutionStep.cs`, `CommitGate.cs`, feature-specific progression/policy fallbacks, superseded warm/checkpoint stores, and last-only consumers/tests.

### Exit gate

Execute is explainable and restart-safe from readiness through certified terminal state. Cancellation, stall, unknown work, partial effects, partial closure, and specific cannot-proceed remain distinct; no blind repeat or legacy progression/policy path is reachable; deletion changes no supported behavior.

## 20. M20 — Application Boundary convergence

### Project and contracts

Create `src/LoopRelay.Application/` as a reusable class library for public commands, queries, results, the thin coordinator, and application-level outcome/exit semantics. It may reference owner contracts but must not reference CLI parsing/rendering, `Microsoft.Data.Sqlite`, provider transports, or filesystem/Git implementations.

The command/query matrix must cover:

- run default/forced/bounded workflow;
- canonical status;
- storage verify/init/migrate/export/sync;
- import detect/preview/execute/verify;
- recovery inspect/plan/execute;
- interaction list/show/respond/cancel;
- completion status/reconcile; and
- capability/prerequisite diagnostics.

Every result carries correlation and causal IDs, exact outcome/reason, evidence links, warnings, pending effects, recovery/interaction/required actions, snapshot identity where applicable, and suggested exit code.

### Refactoring

1. Replace internal invocation-wrapping requests with use-case-specific typed inputs. Repository/workspace identity and invocation policy overrides are explicit fields, not hidden access to CLI objects.
2. Split `UnifiedCliComposition.cs` into owner modules and one production `LoopRelayCompositionRoot`. Resolve configuration and policy once, build one validated catalog, validate exact capabilities and unique owner registrations, then construct one `ILoopRelayApplication`.
3. Make missing or duplicate required owner registrations a typed startup failure before workspace/provider work.
4. Reduce `Program`, `CliArguments`, and `UnifiedCliRunner` to parse -> request -> application -> render -> returned suggested exit code. Formatters accept results/snapshots only.
5. Add dependency tests proving CLI parser/renderer assemblies cannot reference workspace stores, SQL, kernel internals, effect/recovery implementations, completion mutation, or provider transports.
6. Remove retired Roadmap/Plan entrypoints and every alternate composition factory. Update solution and publish scripts so only `LoopRelay.Cli` is a supported application executable; keep `LoopRelay.Certification` as the independent certification executable.

### Verification and exit gate

Exercise the full command/query and typed-outcome matrix through the published CLI and directly through the application library. Assert delegation to the correct owner, renderer purity, cancellation forwarding, exact exit mapping, missing/duplicate composition failure, and absence of historical binaries. One boundary and one composition root must be reachable in the production graph.

## 21. M21 — Retirement completion

### Deletion and reachability audit

1. Add a machine-derived architecture verifier over solution projects, production entrypoints, composition registrations, catalog definitions, executor/recovery/interaction registries, prompt assets, schema/import adapters, and public result claims.
2. Delete exhausted import/compatibility adapters only when portfolio exhaustion facts and adapter-disabled canonical runs pass.
3. Delete provisional bridges, direct table readers, direct required mutations, feature persistence/retry/recovery, duplicate prompt catalogs/framing, dead declarations, stale settings, and unowned prompt/generated assets.
4. Remove stale supported-behavior claims, including any claim that `unblock` is a public command, narrow storage commands perform full import/export/sync, retired executables remain supported, or an uncertified provider capability is available.
5. Remove obsolete project references, tests that only exercise deleted authorities, publish scripts, build artifacts, and compatibility fixtures whose supported portfolio is exhausted. Preserve useful behavior tests against the canonical owners.
6. Build and test the reduced solution after physical deletion; use Git history as the recovery mechanism for accepted deletions.

### Exact final metrics

The verifier must report:

| Metric | Target |
|---|---:|
| Behaviors with zero or multiple owners | 0 |
| Production application boundaries | 1 |
| Production composition roots | 1 |
| Production orchestration kernels | 1 |
| Production workflow catalogs | 1 |
| Logical authoritative mutable stores | 1 |
| Direct required effects outside Effect Coordinator | 0 |
| Workflow-specific persistence/retry/recovery paths | 0 |
| Behavior reachable only through retired code | 0 |
| Unowned runtime/generated prompt assets | 0 |
| Public operational claims without evidence identity or explicit unknown | 0 |

### Exit gate

All metrics equal target, all former routes are absent, imported workspaces run with adapters disabled, both full chains pass from the published CLI, exact provider and platform evidence is truthful, and the owner accepts the single-authority production graph.

## 22. Schema and persistence execution rules

Apply these rules to every v10+ migration:

1. Define logical records and invariants before SQL shape.
2. Add the new physical manifest, required indexes, and a migration/convergence receipt.
3. Preserve workspace and causal identities. Never fabricate provider, effect, recovery, interaction, or historical observations.
4. Run migration in a transaction where SQLite can cover the work. For external projection/import work, commit a durable operation/effect plan first and reconcile afterward.
5. Stamp target version/fingerprint only after full shape and invariant validation.
6. Treat stamped-incomplete or unknown-family/version state as fail-closed, never as an empty workspace.
7. Test fresh creation and every supported predecessor to the same semantic projection.
8. Test interruption at every statement/effect boundary, rollback/restart, repeat migration, and byte-preserving read-only inspection.
9. Keep physical table access behind owner stores/projections. Application, CLI, renderers, handlers, and repository observer receive typed contracts only.
10. Add bounded indexes for unsettled work scans and causal lookup; measure query cardinality and reconstruction time before adding batching/concurrency.

## 23. Module and project placement

Use the following ownership layout unless an accepted ADR records a better dependency boundary:

| Area | Primary location | Rule |
|---|---|---|
| Logical database identity, inspection, migration primitives | `src/LoopRelay.Core/Services/Persistence/` | No workflow, CLI, or provider policy |
| Catalog, kernel, effects, recovery, interactions, storage/import orchestration, read-model contracts | `src/LoopRelay.Orchestration.Primitives/` | Typed authority contracts and domain services |
| Git, filesystem, process, and platform executors/observers | `src/LoopRelay.Infrastructure/` | Implements typed ports; does not advance workflow state |
| Provider runtime/session/capability behavior | `src/LoopRelay.Agents/` | Consumes resolved profiles and prompt identities only |
| Completion decision and closure-plan construction | `src/LoopRelay.Completion/` | No direct Git/filesystem/archive mutation |
| Prompt templates and generated prompt asset metadata | `src/LoopRelay.Core/Prompts/` and `LoopRelay.Prompts.Generator` | Every runtime asset is catalog-owned and hash covered |
| Reusable application contracts/coordinator | new `src/LoopRelay.Application/` | No CLI, SQLite, Git, filesystem, or provider implementation dependencies |
| Only production composition root and CLI adapters | `src/LoopRelay.Cli/` | Composition, parsing, rendering, cancellation forwarding only |
| Independent release/campaign authority | `src/LoopRelay.Certification/` | Invokes the published CLI and uses independent observations |

Move code in small compiling steps: introduce the typed port at the owner, adapt the current call site, route production through it, prove parity, then move/delete the old body. Do not create a generic `Common` or `Utilities` owner for domain behavior.

## 24. Observability, performance, and safety

### 24.1 Observability

- Structured events include workspace, root run, workflow instance, transition run, attempt, session/turn, prompt/dispatch, effect, recovery, interaction, storage/import, and completion identities when present.
- Metrics count legal lifecycle transitions, rejections, unknown/recovery-required outcomes, duplicate coordination, unsettled required work, latency, and restart reconstruction. Metrics are diagnostics, never authority.
- Health/status checks report dependency availability, exact provider profile, schema compatibility, and unsettled work without mutating state.
- External diagnostics link to evidence identities, use bounded single-line summaries where required, scrub paths, credentials, auth material, environment secrets, and provider payloads, and never expose response bodies merely to explain a state.
- Debug views consume the canonical read model; they cannot query raw tables or repair state.

### 24.2 Performance

- Preserve the current one-active-run-per-workspace and linear first-eligible model.
- Measure SQLite query latency/cardinality, transaction duration, repository surface hashing, catalog validation, effect scan size, provider/effect waits, and restart projection/recovery time.
- Use indexed bounded scans, watermarks/cursors, and stable pagination. Add batching only after measurements. Do not introduce parallel scheduling or multiple effect workers as an unreviewed optimization.
- Add a performance smoke fixture with the full catalog, representative ledger history, unsettled effects, and complete read snapshot. It must prove bounded queries and no duplicate outward work, not an arbitrary throughput target.

### 24.3 Security and safety

- Treat repository files, configuration, imported data, provider output, and human responses as untrusted typed input.
- Normalize repository-relative paths and reject root escape, symlink/repository-topology ambiguity, unsupported schema/profile, stale input, invalid response schema, and unrecognized import format before mutation.
- Enforce resolved permission, approval, sandbox, and network ceilings at runtime and effect executors. Prompt payloads, recommendations, imports, and interaction responses cannot elevate them.
- Effects use an allowlisted executor registry with exact preconditions/postconditions. Destructive or remote operations require explicit typed policy and durable authorization evidence.
- Hash/correlate evidence without storing credentials. Cancellation and crash paths preserve terminal facts using a non-cancelled evidence token.

## 25. Testing and certification program

### 25.1 Test layers

Every milestone must add the lowest layer that proves each contract and preserve higher-layer evidence when behavior crosses those boundaries:

1. Unit: pure validators, classifiers, state machines, canonical hashing, outcome/exit mapping.
2. Store/migration: fresh/upgrade/convergence, compare-and-set, append order, idempotency, corruption, replay.
3. Integration: production stores plus fake external adapters, fault injection at every durable boundary, independent postcondition observation.
4. Architecture: dependency/reachability, singular owner, no raw-table client, no console read, no direct effect, no hidden prompt/policy mutation, asset ownership.
5. Public contract: complete application and CLI command/outcome matrix.
6. Static provider compatibility: exact version/schema fixtures for every relied-on capability.
7. Deterministic certification: disposable production-like repositories and independent oracles.
8. Live transition/full-chain certification when provider or end-to-end behavior changes.
9. Genuine platform certification for every platform claimed by release evidence.

### 25.2 Required fixtures

All production-like fixtures explicitly include the nine-file Project Context contract and independent nested `.agents` Git topology. The exact Project Context surface is:

- `.agents/ctx/01-purpose.md`
- `.agents/ctx/02-capability-model.md`
- `.agents/ctx/03-invariants.md`
- `.agents/ctx/04-strategic-structure.md`
- `.agents/ctx/05-authority-model.md`
- `.agents/ctx/06-evaluation-model.md`
- `.agents/ctx/07-drift-and-false-success.md`
- `.agents/ctx/08-vocabulary.md`
- `.agents/ctx/09-eval-details.md`

Maintain fixtures for:

- clean and dirty declared surfaces, unrelated dirty paths, unversioned input, and input freshness changes;
- every prompt dispatch and transition durable boundary;
- local commit, unavailable push, mutation-before-receipt crash, duplicate lease, unknown effect, and partial ordered effects;
- every recovery classification and exact-profile capability denial;
- outstanding/late/invalid/duplicate interaction responses;
- v8, recognized partial-v9, complete current schema, corrupt/unknown/stamped-incomplete schema, and interrupted storage operation;
- every import portfolio format plus malformed, mixed, ambiguous, and conflicting cases;
- invalid catalogs and changed single obligations;
- both roadmap producers, cold/warm/reconstructed Plan, revision rollback, nested/parent publication; and
- first/continued/stalled/cancelled/unknown Execute plus partial and settled completion closure.

### 25.3 Commands and evidence

At each milestone run:

```powershell
dotnet build LoopRelay.slnx --no-restore
dotnet test LoopRelay.slnx --no-restore
```

Run focused changed-project tests during development, then the whole solution at the gate. Build before certification. Use the certification executable's deterministic `canary`, `milestone12`, and `milestone15` campaigns where applicable; run individual live milestones needed by the changed behavior, including Traditional and Eval full chains, rather than assuming an aggregate command runs them. Run the release aggregate only after its underlying evidence exists.

Machine evidence records commit, catalog/schema/profile identities, command, environment/platform, case IDs, test/campaign results, obligation links, independent observations, privacy scan, and skipped/unsupported reasons. Scrub secrets and provider payloads. A missing platform or capability remains missing; it is never averaged into a pass.

## 26. Per-milestone acceptance checklist

Before owner acceptance and commit, answer all items explicitly:

- What permanent property is now enforceable?
- Which production-derived obligations were added, changed, or invalidated, and what executable evidence covers each?
- Which exact owner and production route now implement the behavior?
- Which former route was removed or made unreachable?
- Can restart reproduce the same classification from durable facts alone?
- Do failure, cancellation, unknown, pending, interaction, recovery, and specific cannot-proceed remain distinct?
- Do independent repository/provider observations agree with receipts and claims?
- Do read-only paths leave bytes and semantics unchanged?
- Do build, full component tests, relevant compatibility, deterministic, live-chain, and platform checks pass?
- Which final metric moved, and what temporary duplication remains with its owner and deletion milestone?

A milestone is not complete while a required decision is unresolved, a required effect is pending/unknown, an alternate owner is reachable, evidence is only best effort, or the owner has not accepted architectural closure.

## 27. Program completion checklist

- Both roadmap producers create valid producer-neutral `PreparedEpic` and `MilestoneSpecificationSet` products.
- Plan produces and publishes all five readiness products only after validation and both repository publication protocols settle.
- Execute uses resolved recommendation/profile evidence, survives every provider/effect interruption, derives stall state, and closes only through Completion Authority.
- Every collaboration read has commit/file/surface hashes and lineage; every system fact is ledger-authoritative.
- Every required mutation is journaled, idempotent, independently reconciled, and receipted.
- Every interruption and required human action is durable and restart-safe.
- Verification/status are read-only; storage mutations are explicit/recoverable; imports are one-way and canonical-only afterward.
- Every provider send has one complete hash-covered prompt identity and exact-profile authorization.
- One canonical snapshot explains state, evidence, uncertainty, pending work, and next action.
- Only one application boundary, composition root, catalog, kernel, logical store, effect protocol, recovery protocol, completion authority, and read model remain.
- Legacy workflow bodies, exhausted adapters, direct paths, dead declarations, unowned assets, stale claims, and historical binaries are physically absent.
- The reduced repository builds and tests cleanly, required live/platform evidence is truthful, all final metrics are at target, and the owner accepts the end state.
