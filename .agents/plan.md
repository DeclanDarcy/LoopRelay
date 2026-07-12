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

(See ./milestones/m0-baseline-contract-ratification.md)

## 8. M8 — Effect Coordinator

(See ./milestones/m8-effect-coordinator.md)

## 9. M9 — Recovery Coordinator

(See ./milestones/m9-recovery-coordinator.md)

## 10. M10 — Interaction Broker

(See ./milestones/m10-interaction-broker.md)

## 11. M11 — Workspace Storage Authority

(See ./milestones/m11-workspace-storage-authority.md)

## 12. M12 — Import Gateway

(See ./milestones/m12-import-gateway.md)

## 13. M13 — Workflow Catalog

(See ./milestones/m13-workflow-catalog.md)

## 14. M14 — Orchestration Kernel

(See ./milestones/m14-orchestration-kernel.md)

## 15. M15 — Completion Authority

(See ./milestones/m15-completion-authority.md)

## 16. M16 — Canonical Read Model

(See ./milestones/m16-canonical-read-model.md)

## 17. M17 — Roadmap capability convergence

(See ./milestones/m17-roadmap-capability-convergence.md)

## 18. M18 — Plan capability convergence

(See ./milestones/m18-plan-capability-convergence.md)

## 19. M19 — Execute capability convergence

(See ./milestones/m19-execute-capability-convergence.md)

## 20. M20 — Application Boundary convergence

(See ./milestones/m20-application-boundary-convergence.md)

## 21. M21 — Retirement completion

(See ./milestones/m21-retirement-completion.md)

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
