# Canonical Architecture and Convergence Audit

Architecture contract version: 1.0  
Audit date: 2026-07-10  
Current-state evidence revision: `777d30cb07bbd65c968e13a0a0b4ac488bdee765`  
Target horizon: post-migration, provider-neutral, legacy-free LoopRelay  
Relationship to prior audits: Audit 1 (`orphaned-code-audit.md`) establishes reachability; Audit 2 (`architectural-retirement-strategy-audit.md`) establishes behavioral retirement constraints; this audit defines the architecture against which both migration and retirement must be judged.

## 1. Executive Summary

The optimal LoopRelay end state is not the current unified implementation with old projects removed. It is a single-authority automation platform in which workflow intent is declarative, execution mechanics are universal, durable state has one transactional owner, external effects are journaled and idempotent, compatibility is isolated and expiring, and every architectural decision is explainable from evidence.

The target has these defining properties:

1. **One application surface and one orchestration kernel.** CLI, future interactive UI, and automation APIs are clients of the same application boundary. None owns workflow progression.
2. **One versioned workflow catalog.** TraditionalRoadmap, EvalRoadmap, Plan, and Execute are definitions consumed by the kernel, not independent orchestration implementations.
3. **One logical workspace state store.** Workflow state, products, history, attempts, effects, blockers, recovery, sessions, interaction requests, and compatibility migrations share a transactional identity model. Physical SQLite and content-addressed files are implementation details of that owner.
4. **One canonical product lifecycle.** Agent- or human-written files begin as candidates. Validation promotes immutable, causally identified products. `.agents` is a materialized collaboration and publication view, not a second workflow database.
5. **One outcome model.** Completed, waiting, blocked, failed, cancelled, stalled, ambiguous, and recovery-required remain distinct from prompt transport results through CLI exit mapping.
6. **One effect protocol.** Publication, Git, archive, export, and other repository mutations execute from a durable effect ledger with idempotency keys and explicit partial-outcome recovery.
7. **One compatibility boundary.** Legacy formats may be detected, verified, and migrated by registered adapters. They never become alternate runtime stores, silent fallbacks, or permanent dual-write paths.
8. **Evidence-driven convergence.** A behavior is not migrated because a similarly named class exists. It converges only when the target owner produces equivalent validated outcomes, recovery behavior, effects, and evidence under success and failure.

Against that target, the repository is in the **Hybrid** state, with an estimated weighted convergence of **2.12/5 (42%)**. The canonical workflow catalog, resolver, transition runtime, controller, chain runner, and unified CLI are meaningful progress. The largest remaining architectural gaps are split persistence/history authority; non-durable execution continuity and recovery; declared-but-not-executed effects; incomplete storage compatibility; completion outcome conflation; bypassed operational policy; and compiled legacy workflow authorities that still contain unique behavioral specifications.

The globally efficient migration order is therefore not “delete the oldest code first.” It is:

```text
Ratify target authorities
  -> unify durable identity, state, history, and effect journaling
  -> make transition/session recovery complete
  -> converge missing behavior into target owners
  -> isolate and certify compatibility
  -> prove every behavior through the target
  -> remove alternate authorities and compatibility whose obligations ended
```

This document's target sections are normative. The convergence assessment is a dated snapshot and should be refreshed as the repository changes.

## 2. Audit Framing

### 2.1 Primary question

This audit asks:

> What is the optimal canonical architecture, what invariants define it, how far is the repository from it, and what evidence proves convergence?

It does not infer the target from surviving code. The reasoning order is:

```text
Desired product capabilities
  -> canonical authorities and contracts
  -> target execution and persistence model
  -> current repository projection
  -> gaps, risks, and convergence gates
```

### 2.2 Stable product capabilities

The architecture must support these capabilities independent of their current implementation:

- Transform traditional roadmap intent or evaluation intent into the same prepared-epic and milestone-specification contract.
- Transform planning products into an executable plan, operational context, execution details, milestones, and execution readiness.
- Execute implementation slices with decisions, handoffs, repository evidence, publication, Git evaluation, stall detection, and certified completion.
- Resume after process, provider, or machine interruption without silently repeating unknown external work.
- Verify, import, export, migrate, and repair workspace state without ambiguous storage authority.
- Enforce prompt, permission, model/effort, approval, artifact, and runtime policies from named, versioned policy sources.
- Explain workflow selection, transition eligibility, evidence, blockers, uncertainty, required human action, and compatibility state.
- Add workflows, agent providers, storage implementations, prompt policies, and compatibility adapters without adding competing orchestration authority.
- Support future interactive control while a loop runs without moving authority into the UI.

### 2.3 Normative and informative sections

- Sections 3 through 8 and 12 through 16 define the target architecture and its enduring contracts.
- Sections 9 through 11 and 17 assess the current repository and propose convergence sequencing.
- Current paths and measurements are evidence, not target names or required packaging.
- “Owner” means the sole component authorized to decide or mutate a behavior. Other components may supply pure rules, data, or adapters but may not make the same decision independently.

## 3. Target Canonical Architecture

### 3.1 Architectural shape

The target is a ports-and-adapters application centered on one orchestration kernel and one logical workspace store:

```text
Operator / automation / future interactive UI
                         |
                Application Boundary
                         |
       +-----------------+-----------------+
       |                                   |
 Read models / commands              Orchestration Kernel
                                           |
          +-------------+------------------+------------------+
          |             |                  |                  |
   Workflow Catalog  Evaluation       Execution          Effect Coordinator
          |          Authority         Authority                |
          |             |                  |                     |
          +-------------+---------+--------+---------------------+
                                  |
                       Workspace State Authority
                 SQLite metadata + immutable object store
                                  |
             +--------------------+--------------------+
             |                    |                    |
       Agent providers     Repository/Git       Publication/export
         and prompts          adapters               adapters

Compatibility Gateway -> canonical import transactions only
Interaction Broker     -> durable requests/responses only
```

The CLI is a thin adapter. It parses intent, invokes the application boundary, renders read models, and maps canonical outcomes to exit codes. It does not select stages, infer progress, execute effects, or reconstruct state.

### 3.2 Target logical modules

Physical project names may differ, but code must fit these dependency-directed modules:

| Logical module | Owns | Must not own |
|---|---|---|
| Domain | Identities, workflow/product/effect/outcome contracts, invariant vocabulary | I/O, process launch, SQLite, filesystem, CLI |
| Application | Workflow catalog, orchestration kernel, evaluation, completion, recovery, interaction coordination, application commands/queries | Provider protocols and concrete persistence |
| Runtime | Normalized agent/session execution, capability negotiation, runtime policy application | Workflow eligibility or product promotion |
| Persistence | Workspace transactions, journal, immutable object store, projections, schema evolution | Workflow policy or CLI rendering |
| Infrastructure | Filesystem, Git, publication, telemetry export, clocks, process and provider adapters | Domain decisions |
| CLI | Parsing, rendering, cancellation forwarding, exit mapping, composition | Orchestration logic or independent state |
| Build tooling | Prompt generation and architecture validation tooling | Runtime behavior |

The dependency direction is inward: CLI and adapters depend on application contracts; application depends on domain; domain depends on nothing. Runtime and persistence implement ports defined at the application/domain boundary. Cycles are forbidden.

### 3.3 Target workflow model

The canonical workflow chain is product-driven:

```text
TraditionalRoadmap ----+
                       +-> PreparedEpic + MilestoneSpecificationSet
EvalRoadmap -----------+                    |
                                            v
                                           Plan
                                            |
                 ExecutablePlan + OperationalContext
                 + ExecutionDetails + MilestoneSet
                 + ExecutionReadiness
                                            |
                                            v
                                          Execute
                                            |
                                  CertifiedCompletion
```

Workflow definitions declare identity, stages, transitions, products, gates, policies, effects, recovery semantics, allowed successors, and completion conditions. Definitions do not execute code, choose persistence, render CLI text, or contain provider-specific behavior.

The orchestration kernel interprets every definition through the same lifecycle. Adding a workflow means registering a valid definition and its transition handlers; it does not mean adding a state machine, pipeline, runner, composition root, or executable.

### 3.4 Universal transition lifecycle

Every transition uses this protocol:

```text
Observe authoritative state
  -> resolve workflow/stage/transition eligibility
  -> evaluate input gate
  -> record attempt intent and causal inputs
  -> build policy-bound prompt or deterministic operation
  -> execute through normalized runtime
  -> record raw result
  -> validate candidate products
  -> evaluate output gate
  -> atomically promote products and workflow state
  -> enqueue ordered effects
  -> execute/reconcile effects idempotently
  -> emit read model and evidence
```

Prompt completion is never transition completion. External work is never considered absent merely because the process did not observe its response. A transition is complete only when its output gate is satisfied, state is committed, and required effects are either complete or represented by an explicit recoverable outcome allowed by the definition.

### 3.5 Canonical state and data topology

The target workspace has one logical persistence authority with three representations:

| Representation | Purpose | Authority rule |
|---|---|---|
| `.LoopRelay/persistence/looprelay.sqlite3` | Transactional control state, identities, journals, attempts, effect ledger, blockers, recovery, sessions, interaction, compatibility registry | Canonical for mutable orchestration state and history |
| Workspace object store (SQLite BLOBs or `.LoopRelay/objects/<content-hash>`) | Immutable product bodies, raw outputs, snapshots, and large evidence | Canonical content store, addressed only through state-store records; physical form is replaceable |
| `.agents/**` | Human/agent collaboration view and Git-publication projection | Derived or candidate representation; never an implicit fallback database |

Repository source files and Git remain authoritative for implementation content, but repository observations are versioned evidence consumed by the workflow. Git history is not the orchestration journal. JSONL, Markdown reports, archives, and exports are projections with provenance and schema versions.

An edit under `.agents` becomes authoritative only through an explicit candidate-import, validation, and product-promotion transaction. A materialized file always carries or can resolve a product identity, version, content hash, and causal origin. There is no “latest file wins” fallback.

### 3.6 Transaction and effect boundaries

External agent, Git, filesystem publication, and remote operations cannot be enclosed in a database transaction. The target therefore uses a durable intent/result/effect protocol:

1. Record an attempt with stable workspace, workflow, transition, attempt, correlation, policy, prompt, model/effort, and input-snapshot identities.
2. Execute the external operation.
3. Record completed, failed, cancelled, or unknown raw outcome without promoting products.
4. Validate and promote products in one transaction with workflow state and evidence.
5. Enqueue required effects in that same transaction.
6. Claim and execute effects by idempotency key; record started, succeeded, failed, or unknown.
7. Reconcile unknown outcomes from provider, repository, and effect evidence before retry.

This model makes interruption between implementation and handoff, publication and parent-gitlink recording, certification and archive, or export and metadata update recoverable without relying on live objects.

### 3.7 Target physical deployment

LoopRelay has one supported deployable: `looprelay`. Future UI, service, or automation surfaces must call the same application API and state authority. Provider plugins may be separately packaged, but they implement ports and cannot register workflow progression logic.

The target project budget is one deployable plus no more than six cohesive libraries/build tools corresponding to the logical modules above. An extra project requires a distinct deployment, trust, or build-time boundary; historical product identity is not a valid reason.

## 4. Canonical Authority Model

### 4.1 Authority registry

The end state contains the following single owners. Names are conceptual roles, not mandated class names.

| Behavior | Sole target owner | Owner decides | Explicit non-owners |
|---|---|---|---|
| Product intent | Product Contract Authority | Required business outcomes and observable semantics | Legacy tests, prompts, CLI text |
| Workflow definition | Workflow Catalog | Stages, transitions, dependencies, gates, policies, effects, successors | CLI, transition handlers, repository observer |
| Workflow resolution and chaining | Orchestration Kernel | Which declared workflow and transition is eligible from authoritative state | Individual workflows, adapters, file existence heuristics |
| Execution authority | Orchestration Kernel | Eligibility and lifecycle state changes; application of declared retry/recovery plans | Agent provider, prompt runner, effect adapter |
| Runtime authority | Agent Runtime Gateway | How a normalized session specification is executed by a capable provider | Workflows and providers deciding policy independently |
| Configuration authority | Configuration Resolver | Effective validated configuration and provenance | Environment readers scattered through features |
| Policy authority | Policy Authority | Versioned permission, model/effort, prompt, approval, retry, isolation, and artifact policies | Agent factories and prompt templates with hidden policy |
| Prompt authority | Prompt Authority | Prompt identity, version/hash, variables, policy sections, and rendered evidence | Workflow handlers embedding prompt text |
| Product authority | Product Authority | Candidate registration, promotion after an Evaluation Authority decision, version, freshness, lineage, and canonical representation | Raw files or SQLite rows selected by fallback |
| Evaluation authority | Gate and Validation Authority | Product validity, gate outcome, uncertainty, and supporting evidence | Prompt success or effect success |
| Persistence authority | Workspace State Store | Atomic durable state and schema evolution | Feature-specific stores, JSON sidecars, Git |
| History authority | Evidence Ledger within Workspace State Store | Append-only facts, attempts, outcomes, lineage, and replay order | Numbered files, telemetry export, console output |
| Storage authority | Workspace Storage Authority | Verify, initialize, import, export, migrate, sync, repair plan, and selected format | Workflow implementations and compatibility readers |
| Effect authority | Effect Coordinator | Ordering, idempotency, execution, and reconciliation of publication/Git/archive/export effects | Transition handlers directly performing unjournaled effects |
| Recovery authority | Recovery Coordinator | Classification, bounded evidence gathering, allowed recovery plan, and lineage | Provider error-string branches in individual workflows |
| Completion authority | Completion Authority | Whether completion is valid, certified, blocked, or failed, and required closure effects | Roadmap observer, CLI, archive service |
| Human interaction authority | Interaction Broker | Durable request identity, allowed responses, resolution, timeout, and resume | Console prompts embedded in workflows |
| Repository observation authority | Repository Observer | Normalized evidence about source, Git, workspace projections, and external effects | Workflow-specific filesystem scans |
| Compatibility authority | Compatibility Gateway | Supported legacy formats, migration adapter selection, support window, and retirement eligibility | Runtime fallback chains and old composition roots |
| Observability authority | Canonical Read Model | Status, diagnostics, telemetry facts, authority provenance, uncertainty, and user-action rendering | Ad hoc console-only messages |

Every behavior in production must appear exactly once in a machine-readable authority registry with owner, contract, consumers, evidence, compatibility obligations, and architecture tests. Two owners for one behavior is a defect; no owner is also a defect.

### 4.2 Authority boundaries

Some authorities collaborate without sharing ownership:

- The Workflow Catalog declares an output gate; the Evaluation Authority evaluates it; the Orchestration Kernel acts on the result.
- The Evaluation Authority decides whether a candidate is valid; the Product Authority alone registers and promotes the resulting product version.
- The Policy Authority selects a runtime profile; the Prompt Authority renders the declared prompt policy; the Agent Runtime Gateway enforces and transports the runtime profile; the provider cannot reinterpret either silently.
- The Completion Authority returns a certified domain decision; the Effect Coordinator archives and publishes it; the Workspace State Store commits closure.
- The Recovery Coordinator classifies durable evidence and returns an allowed plan; the Orchestration Kernel alone applies that plan to workflow lifecycle state.
- The Compatibility Gateway translates a legacy representation into a canonical import command; the Workspace Storage Authority performs the transaction; the legacy adapter never becomes the active store.
- The Repository Observer reports facts; it never selects a workflow, promotes a product, or repairs state.

## 5. Architectural Invariants

These are permanent architecture contracts. A change that violates one requires an explicit architecture-version change, not an incidental implementation exception.

### 5.1 Authority invariants

| ID | Invariant |
|---|---|
| ACI-01 | Every production behavior has exactly one registered authority. |
| ACI-02 | There is exactly one supported application entry boundary and one orchestration kernel. |
| ACI-03 | Workflow-specific code declares domain behavior but cannot own scheduling, persistence, retry, chaining, or CLI dispatch. |
| ACI-04 | No compiled legacy implementation may remain a plausible alternate authority after its proof gate passes. |
| ACI-05 | UI, CLI, tests, and adapters consume canonical contracts; none defines production truth. |

### 5.2 Workflow and outcome invariants

| ID | Invariant |
|---|---|
| ACI-06 | Every workflow executes through the same transition lifecycle and outcome vocabulary. |
| ACI-07 | Prompt or provider success cannot directly complete a transition, stage, workflow, or epic. |
| ACI-08 | Workflow and transition eligibility are derived from canonical products, gates, state, and evidence—not untyped file-existence order. |
| ACI-09 | Completed, waiting, blocked, failed, cancelled, stalled, ambiguous, and recovery-required outcomes remain distinguishable end to end. |
| ACI-10 | A downstream workflow consumes declared products and never branches on which upstream workflow produced an equivalent contract. |

### 5.3 Persistence and evidence invariants

| ID | Invariant |
|---|---|
| ACI-11 | Mutable orchestration state and history have one logical transactional owner. |
| ACI-12 | Every promoted product has identity, schema version, content hash, producer, causal inputs, validation result, policy identity, and lifecycle state. |
| ACI-13 | Every external attempt is durably recorded before execution and reconciled after interruption. |
| ACI-14 | Current-state projections are rebuildable from canonical records, or transactionally checked against the same ledger sequence. |
| ACI-15 | Derived files and exports never silently outrank canonical records. |
| ACI-16 | Storage verification is read-only; repair and migration require explicit commands and durable plans. |

### 5.4 Effect, recovery, and completion invariants

| ID | Invariant |
|---|---|
| ACI-17 | Every required external mutation is a declared, ordered, journaled, idempotent effect. |
| ACI-18 | Unknown external outcomes are reconciled before retry; absence of an observed response is not proof of absence of an effect. |
| ACI-19 | Process restart cannot require an undiscoverable in-memory session to determine the next legal transition. |
| ACI-20 | Cancellation preserves all already-observed evidence and returns a distinct canonical outcome. |
| ACI-21 | Completion has one evaluator, one certificate contract, one closure transaction, and one effect plan. |
| ACI-22 | Publication, Git recording, archive, and export failures cannot be reported as workflow completion. |

### 5.5 Policy, compatibility, and evolution invariants

| ID | Invariant |
|---|---|
| ACI-23 | Effective policy and configuration are resolved once per attempt, versioned, validated, and included in causal evidence. |
| ACI-24 | Prompt content, policy sections, model/effort, permissions, and provider capabilities are explicit inputs—not hidden ambient defaults. |
| ACI-25 | Compatibility is one-way into the canonical model; runtime dual authority and indefinite dual write are forbidden. |
| ACI-26 | Every compatibility obligation has an owner, introduction version, supported inputs, migration path, telemetry, deprecation point, and objective retirement gate. |
| ACI-27 | New workflows, stores, providers, and policy modes extend registered contracts without changing kernel authority. |
| ACI-28 | Every orchestration decision is explainable by authority, evidence, ignored evidence, conflicts, uncertainty, and required human action. |
| ACI-29 | Architectural validation runs in CI against the same workflow catalog and authority registry used by production. |
| ACI-30 | No compatibility, provider, or test-only assembly may be discovered by broad reflection or convention and thereby acquire undeclared authority. |

## 6. Behavioral Ownership Model

The target separates domain ownership from mechanical ownership:

| Behavioral domain | Domain contract owner | Mechanical executor | Canonical input | Canonical output |
|---|---|---|---|---|
| Traditional roadmap | TraditionalRoadmap definition | Orchestration Kernel | Roadmap intent, project and repository context | Prepared Epic, Milestone Specification Set |
| Evaluation roadmap | EvalRoadmap definition | Orchestration Kernel | Evaluation intent, project and repository context | Prepared Epic, Milestone Specification Set |
| Planning | Plan definition | Orchestration Kernel | Prepared Epic, Milestone Specification Set | Executable Plan, Operational Context, Execution Details, Milestone Set, Readiness |
| Implementation | Execute definition | Orchestration Kernel + Agent Runtime Gateway | Readiness products and Decision Set | Implementation Slice, Repository Changes, Handoff |
| Decision generation | Execute transition contract | Agent Runtime Gateway | Execution context and prior handoff lineage | Validated Decision Set and execution configuration |
| Publication and Git | Effect contracts | Effect Coordinator | Promoted products and repository snapshot | Effect receipts and reconciled repository evidence |
| Completion decision | Completion contract | Completion Authority | Milestone, repository, review, and certification evidence | Certified Completion or explicit non-success decision |
| Completion closure | Completion effect contracts | Effect Coordinator | Certified Completion and closure plan | Archive/publication/context receipts; Kernel commits lifecycle state |
| Storage | Storage contract | Workspace Storage Authority | Workspace and explicit operation | Verified/migrated/exported canonical state plus report |
| Recovery | Recovery contract | Recovery Coordinator | Pending attempts/effects, provider and repository evidence | Recovery plan and lineage; Kernel applies the canonical lifecycle outcome |
| Human interaction | Interaction contract | Interaction Broker | Typed request from workflow/policy/recovery | Durable response or waiting/blocked outcome |
| Prompt rendering | Prompt contract | Prompt Authority | Prompt identity, product snapshots, policy profile | Hashed rendered prompt and provenance |
| Agent execution | Runtime contract | Agent Runtime Gateway | Validated session specification and prompt | Normalized turns, usage, provider evidence, and session identity |
| Product validation | Product contract | Evaluation Authority | Candidate content and causal context | Validation decision; Product Authority performs any promotion |
| Status and diagnostics | Read-model contract | Canonical Read Model | Canonical state and observations | Explainable operator view |

No row permits its domain workflow to invent a private runner. Reuse occurs by shared contracts and kernel mechanics, not by one workflow calling another workflow's implementation.

## 7. Authority Graph

The authority graph is the required organizing structure for implementation and audit evidence:

```text
Behavior
  -> Sole Authority
  -> Declared Dependencies
  -> Consumers
  -> Durable Evidence
  -> Convergence Proof
  -> Retirement Gate
```

| Behavior | Authority | Dependencies | Consumers | Required evidence | Retirement gate for prior owner |
|---|---|---|---|---|---|
| Workflow catalog | Workflow Catalog | Domain contracts, policy identities | Kernel, validation, status | Version/hash and validation report | All production/test definitions resolve from one catalog |
| Workflow selection | Orchestration Kernel | Observation, products, gates, requested mode | CLI/API, chain runner | Resolution decision with alternatives/conflicts | No other selector or CLI-specific branch is reachable |
| Transition execution | Orchestration Kernel | Definition, input snapshot, policy, handler | Workflows, status, recovery | Attempt/result/state sequence | All behaviors pass lifecycle and fault matrix through kernel |
| Product promotion | Product Authority | Candidate, validation decision, causal inputs | Gates, prompts, downstream workflows | Product record and immutable content hash | File/legacy stores cannot be selected as alternate authority |
| Gate evaluation | Evaluation Authority | Product records, observation, policy | Kernel, completion, status | Structured result and supporting evidence | Prompt-success/file-existence gates removed |
| Agent session | Agent Runtime Gateway | Capability negotiation, policy-bound spec | Transition handlers | Session/turn identity, normalized outcome, usage | Direct provider process calls absent outside adapter |
| Durable state | Workspace State Store | Schema and transaction protocol | All application services | Transaction sequence and integrity verification | Feature-specific authoritative state stores retired |
| History and evidence | Evidence Ledger | Workspace transaction sequence and stable identities | Status, recovery, completion, audit | Append-only facts, lineage, and integrity verification | Numbered files/exports cannot act as history authority |
| Effects | Effect Coordinator | Effect definition, promoted products | Publication, Git, archive, export | Idempotency key, attempt, receipt, reconciliation | Direct unjournaled effects and null bypasses removed |
| Recovery | Recovery Coordinator | Pending attempt/effect, provider/repository evidence | Kernel, interaction, status | Recovery journal, plan, lineage | Workflow-local recovery branches removed |
| Completion | Completion Authority | Validated completion inputs and policy | Execute, archive, status | Certificate or typed block/failure | Alternate roadmap/console completion ownership removed |
| Storage | Workspace Storage Authority | Store, compatibility registry | Startup verification, storage commands | Read-only report or migration transaction | Old storage coordinator no longer needed for any fixture |
| Prompt rendering | Prompt Authority | Versioned templates and selected prompt-policy identity | Runtime handlers, evidence | Rendered hash and source provenance | Hidden fragments and embedded prompt text eliminated |
| Policy resolution | Policy Authority | Validated configuration and versioned profiles | Prompt Authority, Runtime Gateway, Kernel | Effective values and provenance | Per-call-site defaults eliminated |
| Human interaction | Interaction Broker | Typed request and policy | UI/CLI, kernel, recovery | Request/response/timeout records | Embedded console prompts and empty placeholder contracts removed |
| Compatibility | Compatibility Gateway | Registered adapters and support policy | Storage import/verify | Detection, migration, usage, retirement report | Support window ended and all fixtures migrate canonically |

## 8. Enduring Architecture Contracts

### 8.1 Workflow definition contract

A definition is valid only when it declares:

- Stable workflow, stage, transition, product, gate, effect, policy, and recovery identities.
- Entry and exit products and gates.
- Dependency strength and invalidation/freshness rules.
- Allowed successors and terminal outcomes.
- Transition execution posture and required provider capabilities without naming a provider.
- Ordered effects and their failure semantics.
- Recovery behavior for cancellation, failure, unknown result, and partial effect.
- Compatibility version of its inputs and outputs.

Definitions are immutable within a version. A semantic change creates a new version and migration rule. Production startup and CI validate the exact same catalog.

### 8.2 Product contract

A promoted product record contains at minimum:

```text
ProductIdentity + ProductVersion
ContentHash + SchemaVersion
ProducerWorkflow + ProducerTransition + AttemptIdentity
CausalInputIdentities + RepositorySnapshotIdentity
PromptIdentity/Hash + PolicyIdentity + RuntimeConfigurationIdentity when applicable
ValidationResult + Freshness/Invalidation State
Lifecycle State + Materialization Locations
CreatedAt + Supersedes/SupersededBy
```

Product bodies are immutable. Revision creates a new version; it does not overwrite history. A materialized `.agents` path may point to one promoted version or contain an unpromoted candidate, never both ambiguously.

### 8.3 Gate and evaluation contract

Every gate returns one of `Satisfied`, `Unsatisfied`, `Blocked`, `Waiting`, `Invalid`, or `Ambiguous`, with requirement-level results, authority, evidence identities, missing inputs, conflicts, uncertainty, and remediation. Evaluation is pure with respect to durable state; the kernel persists and acts on the result.

### 8.4 Attempt and runtime contract

Every external run has stable workspace, run, workflow, transition, attempt, session, and turn identities. The effective prompt, policy, model/effort, permissions, workspace scope, provider, provider capabilities, token/usage limits, retry policy, and input snapshot are fixed and recorded before launch.

Providers return normalized states and typed diagnostics. Provider-specific strings are retained as evidence but are not the domain outcome classifier. Persistent session resume, read, or fork is capability-negotiated. Missing capability yields a policy decision or typed block, never an unrecorded fallback.

### 8.5 Effect contract

Every effect declares:

- Identity, category, trigger, inputs, ordering group, and dependencies.
- Deterministic idempotency key and expected postcondition.
- Whether compensation is legal; compensation is never assumed.
- Retry and reconciliation policy.
- Observable success, failure, and unknown-outcome evidence.
- Whether workflow progression waits for completion.

Effect adapters cannot mark workflow state directly. The coordinator records receipts and asks the kernel to re-evaluate.

### 8.6 Persistence and history contract

All state-changing application operations use the Workspace State Store. Feature-specific repositories may be query helpers, but they cannot select a different source of truth. Schema migrations are transactional, monotonic, versioned, backed up, and tested against every supported input version. History is append-only; corrections append superseding facts.

### 8.7 Recovery contract

Recovery begins from durable pending work, not exception text. It classifies `not-started`, `in-flight`, `succeeded-uncommitted`, `failed`, `cancelled`, `unknown`, and `partially-effected`. It gathers bounded provider, repository, product, and effect evidence; chooses reconcile, resume, fork, retry, compensate, wait, block, or human decision; and records lineage before action.

### 8.8 Completion contract

Completion requires validated milestone state, repository evaluation, non-implementation review when policy requires it, certification, discoverable evidence, and closure effects. The certificate includes all causal evidence and policy identities. Archive and roadmap-context updates are effects of one closure plan, not independent completion authorities. A blocked certification stays blocked; it cannot be translated to generic prompt failure.

### 8.9 Prompt and policy contract

Prompts are versioned assets referenced by identity. Policy sections are composed explicitly and contribute to the rendered hash. Effective policy has a single resolved source with layered provenance, conflict validation, and no free-form escape strings outside a declared extension field. Agent factories accept a resolved session policy; they do not choose their own defaults.

### 8.10 Interaction and explainability contract

Human interaction is a durable domain object with category, question, allowed response shape, reason, requesting authority, deadline, default policy, and correlation identity. Waiting for input is a canonical state. Any client may render/respond through the application boundary, enabling future interactive CLI or UI without embedding interaction in orchestration.

Every status response includes selected workflow/stage/transition, satisfied and unsatisfied gates, blockers, pending recovery/effects, storage authority, compatibility state, human action, evidence, alternatives rejected, conflicts, and uncertainty.

### 8.11 Compatibility contract

Compatibility adapters are pure readers/translators or explicitly journaled exporters. Import creates canonical identities and a migration receipt. After successful import, runtime reads canonical state only. A legacy representation may remain for rollback/export but is marked non-authoritative. Silent fallback and bidirectional merge are forbidden.

## 9. Architectural Convergence Model

### 9.1 Scoring scale

| Score | State | Meaning |
|---:|---|---|
| 0 | Absent | Target behavior or owner does not exist. |
| 1 | Fragmented | Behavior exists in legacy, bypassed, split, or ambiguous owners. |
| 2 | Hybrid | Canonical path exists but important behavior, evidence, or failures remain elsewhere. |
| 3 | Mostly canonical | Canonical owner handles normal production flow; recovery, compatibility, or proof is incomplete. |
| 4 | Fully canonical | All production flow uses the target owner; compatibility is isolated and evidence is complete. |
| 5 | Legacy-free | Fully canonical, proof-certified, alternate owners removed, and architecture contracts enforced in CI. |

Scores measure architectural convergence, not code quality or test pass percentage. Target convergence is 5 for every subsystem.

### 9.2 Current convergence assessment

| Subsystem | Weight | Current | Target | Principal blocker | Confidence |
|---|---:|---:|---:|---|---|
| CLI and composition boundary | 5% | 4 | 5 | Retired executable projects still compile; composition does not enforce every target policy | High |
| Workflow catalog | 8% | 3 | 5 | Canonical definitions exist, but duplicate wrappers/catalogs and legacy workflow specifications remain | High |
| Resolution and chaining | 7% | 4 | 5 | Unified path exists; full explainability and human-interaction evidence are incomplete | High |
| Transition execution | 10% | 3 | 5 | Normal lifecycle exists, but workflow-specific prompt execution retains hidden sequencing/session state | High |
| Products, gates, and evaluation | 8% | 3 | 5 | Canonical records exist; file/SQLite representation and parity of detailed validators remain incomplete | Medium |
| Persistence and history | 12% | 1 | 5 | Canonical workflow SQLite, file-backed loop history, unused SQLite history, exports, and archives compete | High |
| Effects, publication, and Git | 8% | 2 | 5 | Effects are declared, but some are evidence-only or executed directly without one durable effect protocol | High |
| Recovery and resume | 10% | 1 | 5 | Execution continuity depends on in-memory sessions; cancellation and unknown outcomes lack one journal | High |
| Completion | 7% | 2 | 5 | Core service exists, but blocked/failure semantics, archive recovery, and closure ownership remain split | High |
| Storage and compatibility | 8% | 1 | 5 | Named import/export/sync behavior and legacy round-trip verification are incomplete; windows are undefined | High |
| Prompt and policy authority | 5% | 2 | 5 | Prompt-specific policy is omitted, configuration ownership is distributed, and generated remnants remain | High |
| Agent runtime and operability | 5% | 1 | 5 | Telemetry, usage-limit handling, input-wait diagnostics, runtime prerequisites, and selection evidence are bypassed/distributed | High |
| Human interaction and explainability | 3% | 1 | 5 | Structured interaction type is never populated; status can advertise action without durable requests | High |
| Behavioral certification | 4% | 2 | 5 | Extensive tests exist, but many certify legacy owners rather than target-owner equivalence and fault recovery | High |
| **Weighted total** | **100%** | **2.12/5 (42%)** | **5/5** | Durable authority and behavioral proof are the limiting factors | **High** |

### 9.3 Convergence stages

```text
Current/Fragmented
  -> Hybrid
  -> Mostly Canonical
  -> Fully Canonical
  -> Legacy-Free
```

- **Hybrid:** canonical and alternate owners coexist; deletion can lose behavior.
- **Mostly Canonical:** all new work lands in target owners, but some failure, recovery, or compatibility paths still depend on old behavior.
- **Fully Canonical:** all supported behavior and compatibility run through target contracts; old code is observationally unnecessary.
- **Legacy-Free:** old implementations, tests that instantiate them, stale contracts, and expired adapters are removed; CI prevents reintroduction.

## 10. Architectural Gap Analysis

| Gap | Category | Current evidence | Target violation | Convergence requirement |
|---|---|---|---|---|
| GAP-01 | Duplicate capability | Retired Roadmap state machine, Plan pipeline, and old Execute loop remain compiled/tested | ACI-02/03/04/06 | Move every retained behavior and test to target contracts, prove parity, then remove alternate runners/projects |
| GAP-02 | Split ownership | Canonical workflow state uses SQLite while active decision/handoff/delta history defaults to files and completion queries SQLite history | ACI-11/14/15 | One Workspace State Store and product/history identity; derived files only |
| GAP-03 | Missing capability | Unified storage import/export/sync do not perform the full named data movement and verification behavior | ACI-16/25/26 | Implement storage operations and compatibility migration through one authority with fixture certification |
| GAP-04 | Incomplete migration | Plan declares publication and parent-gitlink effects, but active handling does not execute the complete behavior | ACI-17/22 | Execute both through durable ordered effects and prove restart/idempotency |
| GAP-05 | Incomplete migration | Execute handoff requires the prior in-memory implementation session | ACI-13/18/19 | Persist attempt/session/turn lineage and capability-aware resume/fork/recovery |
| GAP-06 | Incomplete migration | Cancellation salvage, review ordering, resume cleanup, and restart outcomes differ from preserved behavior | ACI-09/20/21 | Ratify desired semantics, encode them in contracts, and certify target outcomes |
| GAP-07 | Split ownership | Completion evaluation, archive materialization/recovery, roadmap-context updates, and CLI mapping cross multiple owners | ACI-21/22 | One completion decision and closure plan; effects journaled separately |
| GAP-08 | Architectural ambiguity | `.agents`, legacy files, canonical rows, and archives can each appear authoritative for a product/history | ACI-11/12/15 | Product Authority with explicit canonical version and materialization provenance |
| GAP-09 | Missing capability | Telemetry, usage-limit wait/retry, and input-wait diagnostics are implemented but bypassed in production | ACI-23/24/28 | Decide policy in target, compose once at runtime boundary, and make evidence canonical |
| GAP-10 | Split ownership | Effort/model/runtime settings and prompt/artifact policy are chosen by scattered factories/call sites or ambient defaults | ACI-23/24 | Configuration and Policy Authorities produce one recorded session policy |
| GAP-11 | Incomplete migration | Prompt-specific roadmap rigor/policy fragments and detailed semantic evidence are not proven in canonical rendering | ACI-07/12/23 | Versioned prompt/policy profiles and behavioral proof against required outcomes |
| GAP-12 | Missing capability | Structured human-interaction requirements are always empty and request capture is not composed | ACI-09/28 | Durable Interaction Broker integrated with kernel and status |
| GAP-13 | Temporary bridge | Retirement stubs and duplicate workflow catalogs/wrappers preserve historical navigation paths | ACI-02/04/29 | One deployable, one catalog, architecture tests, migration guidance outside executable projects |
| GAP-14 | Obsolete compatibility | Legacy filesystem/SQLite histories, exports, archives, JSONL consumers, and old state readers have no declared support end | ACI-25/26 | Compatibility register with owner, usage evidence, deprecation and objective retirement trigger |
| GAP-15 | Historical artifact | Unused prompts, wrappers, outcome DTOs, ownership constants, and adapters survive generation/compilation | ACI-04/30 | Remove once product-intent checks pass; CI rejects unowned generated/runtime types |
| GAP-16 | Architectural ambiguity | External attempts and effects lack a universal unknown-outcome and idempotency protocol | ACI-13/17/18 | Durable attempt/effect ledger and reconciliation contract |
| GAP-17 | Incomplete migration | Runtime workflow-definition validation is separate from production catalog construction | ACI-29 | Validate the exact production catalog at build and startup; fail closed |
| GAP-18 | Missing capability | Status cannot fully report policy provenance, pending effects, recovery lineage, alternatives, and uncertainty | ACI-28 | Canonical explainability read model consumed by every UI |
| GAP-19 | Architectural ambiguity | Compatibility and product readers can be selected by optional/default constructors and null adapters | ACI-01/11/25 | Composition validates exactly one owner and rejects required-effect null implementations |
| GAP-20 | Incomplete proof | Green legacy tests demonstrate old behavior, not target-owner equivalence | ACI-04/29 | Behavior-level convergence bundles and production-reachability proof |

## 11. Complexity Reduction Analysis

### 11.1 Dated baseline

At the assessed revision:

| Measure | Current baseline | End-state target |
|---|---:|---:|
| Supported deployables | 1 published plus 2 compiled retirement stubs | 1 |
| Production projects | 11 | At most 7, each aligned to a target logical boundary |
| Production project-reference edges | 29 | At most 15, acyclic and inward-directed |
| C# source files | 710 | Net below the current baseline after target capabilities land |
| C# source lines | 54,553 physical / 48,115 nonblank | Net below the current baseline; line count is secondary to authority reduction |
| Audited non-production inventory | At least 21,000 lines across 300+ assets | 0 unowned runtime assets |
| Retired Roadmap/Plan implementation | 239 files, about 17,366 lines | 0 |
| Old Execute loop | 4 files/assets, about 381 lines | 0 |
| Compiled orchestration models | Canonical kernel plus Roadmap state machine, Plan pipeline, and LoopRunner | 1 kernel |
| Public type declarations | Approximately 456 | Every public type has a registered cross-module consumer; no legacy public surface |
| Compatibility obligations | Multiple implicit/undated | 100% registered; 0 silent fallbacks; 0 expired adapters |

The target does not reward deleting lines that must be reintroduced to preserve a capability. The hard reduction is conceptual: one behavior owner, one lifecycle, one history, one effect protocol, one recovery protocol, and one compatibility boundary.

### 11.2 Certified minimum simplification

Once behavioral convergence is proven, removing the retired Roadmap/Plan bodies and old Execute loop eliminates at least:

- 243 implementation files/assets.
- Approximately 17,747 lines.
- Two executable projects and their test-only navigation paths.
- Three alternate orchestration concepts: roadmap state machine, fixed Plan pipeline, and LoopRunner.
- Duplicate workflow chain/catalog wrappers and legacy completion/persistence selection paths.

The broader 21,000-line inventory must resolve to one of two outcomes: it becomes reachable behavior owned by a target authority, or it disappears. “Compiled and tested but not owned” is not an allowed end state.

### 11.3 Cognitive and navigation metrics

These metrics matter more than LOC and should be tracked per release:

| Metric | Target |
|---|---:|
| Behaviors with zero or multiple registered owners | 0 |
| Production orchestration kernels/runners | 1 |
| Production workflow catalogs | 1 |
| Authoritative mutable workspace stores | 1 logical owner |
| Direct external effects outside Effect Coordinator | 0 |
| Workflow-specific persistence/retry/recovery implementations | 0 |
| Steps from authority registry to production handler | At most 2 |
| Steps from status item to supporting evidence | 1 stable identity lookup |
| Required behavior tested only through legacy owner | 0 |
| Compatibility branches lacking usage/expiry evidence | 0 |
| Required dependencies satisfied by null/no-op adapters | 0 |

## 12. Compatibility Strategy

### 12.1 Compatibility lifecycle

Every compatibility obligation follows:

```text
Detect read-only
  -> identify exact legacy version and authority conflict
  -> produce migration plan and preview
  -> explicit canonical import transaction
  -> verify semantic equivalence and receipt
  -> mark legacy representation non-authoritative
  -> observe usage during declared support window
  -> retire adapter when objective gate is satisfied
```

Mutation cannot begin while authority is ambiguous. Verification cannot repair. Runtime cannot fall back to a legacy source after successful canonical import.

### 12.2 Compatibility register contract

Each entry records:

- Obligation identity and accountable owner.
- Legacy producer and format/schema versions.
- Detection and conflict rules.
- Supported read/import/export operations.
- Semantic mapping and known losses.
- Migration trigger and rollback boundary.
- Introduced, deprecated, not-before-retirement version/date.
- Usage counters or explicit consumer inventory.
- Certification fixtures.
- Retirement trigger and removal revision.

No entry may ship with an undefined owner or retirement trigger. A calendar date alone is insufficient; semantic migration and consumer evidence are required.

### 12.3 Current obligations requiring registration

| Obligation | Target owner | Migration trigger | Legitimate retirement trigger |
|---|---|---|---|
| Pre-unification roadmap state, lifecycle rows, journals, blockers, and recovery markers | Compatibility Gateway + Storage Authority | Workspace verification detects supported legacy identity | All certified fixtures import; no supported workspace requires old reader; deprecation window elapsed |
| Partial Plan artifacts | Product import adapter | Plan entry detects unpromoted legacy artifacts | Every supported partial state maps to promoted products or an explicit block; usage window elapsed |
| Legacy decision-session JSON | Session-state import adapter | File detected with no conflicting canonical row | Import is shipped and observed; no unresolved conflict fixtures; support window elapsed |
| Filesystem and older SQLite loop histories/exports | History import adapter | Completion/history query detects legacy source | Histories import with stable order/hash and completion parity; consumers migrated |
| Completion archives and archive indexes | Completion archive adapter | Completion/status encounters supported archive version | Discovery and import certified across collision/partial cases; old format usage reaches declared threshold |
| JSONL telemetry | Export adapter only | Explicit export configuration | Consumer inventory is empty or migrated; deprecation window elapsed |
| Old CLI names/scripts | Documentation/installer migration aid | Invocation or publish attempt | Successor command available for declared window; no runtime state depends on old executable |
| Historical prompt/product layouts | Product/prompt import adapter | Versioned artifact detected | All supported layouts promote to current product schema and no active workflow emits old layout |

The current repository does not declare release windows or consumer telemetry for these obligations. Establishing those values is a convergence input, not permission to preserve adapters indefinitely.

## 13. Behavioral Convergence Proof Requirements

### 13.1 Proof chain

For every behavior:

```text
Behavior Contract
  -> Target Authority and Handler
  -> Canonical Inputs and Causal Identity
  -> Equivalent Validated Outcome
  -> Equivalent Effects and Recovery
  -> Observable Evidence
  -> Production Reachability Through Target Only
  -> Legacy Removal
```

Code similarity, shared prompts, green unit tests, and an unreachable replacement class are not proof.

### 13.2 Required convergence bundle

Each migrated or intentionally retired behavior has a versioned bundle containing:

1. **Contract:** observable success, non-success, side-effect, and recovery semantics.
2. **Authority mapping:** old owner, target owner, consumers, and invariants.
3. **Fixture set:** representative real and synthetic states, including every supported legacy format.
4. **Target tests:** tests invoke the production catalog/composition, not the handler or old implementation directly unless testing a pure contract.
5. **Differential evidence:** old and target outcomes compared where the old behavior is preserved.
6. **Intentional-difference record:** ADR for every accepted semantic change, with migration and operator impact.
7. **Fault matrix:** cancellation and failure before/during/after prompt, validation, commit, publication, Git, archive, and export.
8. **Restart matrix:** restart at every durable boundary, including unknown external outcomes.
9. **Idempotency proof:** repeated command/effect/recovery yields one semantic result.
10. **Compatibility proof:** import, conflict, corruption, unsupported schema, and retirement fixture results.
11. **Explainability proof:** status and diagnostics identify authority, evidence, conflict, uncertainty, and action.
12. **Reachability proof:** supported entrypoints reach only the target owner; tests no longer require the old owner.
13. **Removal proof:** build, full tests, publish, command smoke matrix, migration matrix, and architecture checks pass after deletion.

### 13.3 Proof levels

| Level | Evidence | Permitted action |
|---:|---|---|
| P0 | Desired behavior and authority unspecified | No migration or deletion |
| P1 | Contract and target owner ratified | Implement behind target boundary |
| P2 | Success and validation parity certified | Route opt-in/new work; keep old recovery path isolated |
| P3 | Failure, restart, effects, and compatibility certified | Make target production default |
| P4 | Production reachability and observation certify target-only behavior | Stop maintaining old implementation; prepare deletion |
| P5 | Legacy removed and full post-removal certification passes | Mark behavior Legacy-Free |

Every subsystem must reach P5 for a 5/5 convergence score.

### 13.4 Example retirement gates

**Plan publication and parent gitlink** are converged only when promoted Plan products enqueue ordered publication and parent-gitlink effects, restart between them reconciles correctly, duplicate invocation is idempotent, failures remain non-success outcomes, status explains pending work, and no Plan legacy test is needed to assert the behavior.

**Storage import/export/sync** are converged only when every supported fixture is detected read-only, conflicts block, import creates canonical product/history identity, export is a versioned projection, sync has a defined one-way or merge contract, interruption is recoverable, and post-import runtime never reads the legacy source.

**Execute continuity** is converged only when restart after implementation can resume, fork, or reconcile the handoff from durable identities; cancellation preserves evidence; unknown provider outcomes cannot duplicate implementation; and the same outcome vocabulary reaches CLI/status.

## 14. Architectural Risk Assessment

| Risk | Failure mode | End-state control | Leading indicator |
|---|---|---|---|
| Hidden authority duplication | A fix lands in a tested class production never calls | Machine-readable authority registry and production reachability tests | Behaviors with multiple owners |
| Split persistence | Status, completion, and resume observe different histories | One state store/product identity and no fallback readers | Conflicting hashes/“latest” records |
| Split workflow | A workflow invents private sequencing or retry | Definition validation and kernel-only progression | New runner/pipeline/state-machine types |
| Split completion | Certification, archive, and closure disagree | One completion decision and effect plan | Multiple completion outcome mappings |
| Unjournaled effects | Publication/Git succeeds but state says otherwise | Effect ledger, idempotency, reconciliation | Direct adapter calls from handlers |
| Unknown-outcome replay | Restart repeats external implementation | Intent/result journal and provider/repository reconciliation | Attempts retried without prior identity |
| Compatibility permanence | Legacy branches become unremovable | Support registry, telemetry, not-before and semantic gates | Adapters with no expiry/usage |
| Policy drift | Different agents receive inconsistent model, effort, prompt, or permission rules | Resolved versioned session policy | Literals/defaults outside Policy Authority |
| Prompt drift | Generated prompt exists without owner or policy identity | Catalog registration and source-hash validation | Generated types with no catalog consumer |
| Product ambiguity | Files, rows, and archives all claim “latest” | Immutable product version and explicit materialization | Unqualified file/row fallback |
| No-op dependency | Required effect/policy silently disappears through null adapter | Composition validation and required capability checks | Null implementation selected in production |
| Interaction loss | Operator action is required but not durably represented | Interaction Broker | Status text with no request identity |
| Provider lock-in | Workflow behavior depends on transport-specific frames | Normalized runtime contract and negotiated capabilities | Provider types referenced by workflow module |
| Over-centralized god object | One composition/kernel class accumulates domain handlers and adapters | Module boundaries, handler registry, dependency rules | File size/dependency fan-out growth |
| False convergence | Test count rises while target recovery/effects remain unproved | P0-P5 proof bundles and weighted scorecard | Legacy-only or handler-only tests |
| Premature deletion | Unique behavior disappears with old project | P4 gate before removal and post-removal certification | Deletion proposed without differential bundle |
| Architecture ossification | Future workflow/provider requires kernel edits | Extension contracts and compatibility-neutral registration | Feature PR modifies kernel switch statements |

## 15. Future-Proofing Contracts

### 15.1 Adding a workflow

A new workflow supplies versioned products, stages, transitions, gates, policies, effects, and recovery metadata plus registered handlers. It passes catalog validation and architecture tests. It may not add a runner, executable, persistence schema outside the store, or workflow-selection branch outside the kernel.

### 15.2 Adding an agent provider

A provider advertises capabilities for one-shot, persistent, resume, read, fork, structured output, cancellation, usage, model/effort, permission, and workspace isolation. The Runtime Gateway chooses only providers satisfying the resolved policy. Unsupported capabilities produce a typed policy outcome; workflows do not branch on provider name.

### 15.3 Adding storage

A storage implementation must satisfy the full transaction, concurrency, schema, journal, object-integrity, backup, and migration contract. Exactly one implementation is selected per workspace. Multi-store mirroring is an explicit effect/export, not shared authority.

### 15.4 Adding prompt or policy modes

New policy profiles are versioned data selected by the Policy Authority and referenced by workflow definitions. They contribute to causal identity and evidence. A prompt may declare extension points; workflows and providers may not append hidden instructions.

### 15.5 Adding compatibility modes

New adapters register a bounded source version, translation contract, fixtures, telemetry, and retirement gate. They cannot modify canonical runtime semantics or remain as “just in case” fallback providers.

### 15.6 Adding interactive control

Live inspection, pause, approval, correction, and guided decision features operate through commands, read models, and durable Interaction Broker objects. The UI can request an action but cannot mutate workflow tables, resolve gates, or execute effects directly.

### 15.7 Adding concurrency

Parallel transitions require declared independence, immutable input snapshots, optimistic concurrency, deterministic join/merge contracts, and effect ordering. The kernel may evolve its scheduler without changing workflow ownership or product identity.

## 16. Long-Term Architecture Contracts and Governance

### 16.1 Required enduring artifacts

The audit should ultimately be materialized as maintained repository contracts:

- `docs/architecture/canonical-architecture.md` — normative version of Sections 3 through 8 and 12 through 16.
- `docs/architecture/authority-registry.yaml` — one owner per behavior, with contract and consumers.
- `docs/architecture/compatibility-register.yaml` — every active obligation and retirement gate.
- `docs/architecture/convergence-scorecard.md` — dated scores, blockers, proof links, and confidence.
- `docs/architecture/decisions/` — ADRs for intentional behavioral changes and authority changes.
- Architecture tests — dependency direction, catalog validity, composition uniqueness, prohibited direct effects, and registry completeness.

### 16.2 Change rules

Any change that introduces or moves a behavior must update the authority registry. Any change to a workflow/product/effect contract creates a versioned change and migration decision. Any compatibility addition requires a removal condition in the same change. Any exception to an invariant must be time-bounded, owned, observable, and represented in the convergence score as a regression.

Architecture review asks:

1. Which behavior and authority are affected?
2. Does another component already own the decision?
3. What canonical identities, evidence, and outcomes are produced?
4. How does restart or unknown external outcome behave?
5. Are effects journaled and idempotent?
6. Is compatibility one-way and expiring?
7. Can status explain the result?
8. What proof level does the change achieve or regress?

### 16.3 CI architecture gates

CI must fail when:

- The production catalog or authority registry is invalid or incomplete.
- More than one supported executable or orchestration kernel appears.
- A workflow module references CLI, provider protocol, concrete persistence, or effect adapters.
- A handler directly performs a declared external effect.
- A required production dependency resolves to null/no-op.
- Public runtime code or generated prompt assets lack a registered owner/consumer.
- A compatibility adapter lacks version bounds, fixtures, usage evidence, or retirement gate.
- Target behavior is tested only by constructing a legacy owner.

## 17. Convergence Roadmap Inputs

This is an ordering model, not an implementation estimate. Each phase is entered only when the prior exit gate is met.

### Phase 0 — Target Ratified

**Work:** Ratify architecture version 1.0, the authority registry, product/state topology, outcome vocabulary, compatibility policy, and proof rubric. Classify behavioral differences as preserve, intentionally change, or retire.

**Exit gate:** Every production behavior and compatibility obligation has one target owner; unresolved product decisions are explicit blockers rather than implicit legacy preservation.

### Phase 1 — Durable Authority Foundation

**Work:** Establish stable workspace/run/workflow/transition/attempt/session/turn/product/effect identities; one Workspace State Store and Evidence Ledger; immutable product storage; canonical `.agents` materialization; effect journal; schema and import framework.

**Why first:** Recovery, completion, storage, telemetry, and behavioral proof all depend on the same causal identity. Building feature bridges before this foundation would create more temporary stores and correlation schemes.

**Exit gate:** New transitions, products, sessions, and effects cannot bypass canonical persistence; current read models resolve one identity.

### Phase 2 — Universal Kernel, Runtime, and Recovery

**Work:** Complete the universal lifecycle, resolved policy/configuration, normalized runtime capabilities, attempt journal, session resume/read/fork handling, unknown-outcome reconciliation, durable interaction, and typed outcome propagation.

**Exit gate:** Restart and cancellation matrices pass without relying on in-memory-only state or provider-string domain classification.

### Phase 3 — Behavioral Convergence

**Work:** Converge missing storage operations; Roadmap validation/policy; Plan publication/gitlink; Execute continuity, review, salvage, and completion semantics; operational telemetry/quota/input-wait behavior; and explainability into target owners. Use target production composition in tests.

**Exit gate:** Each behavior is P3 or higher; no new production behavior lands in legacy owners.

### Phase 4 — Compatibility Quarantine

**Work:** Move all legacy readers into the Compatibility Gateway; build import receipts and semantic fixtures; define support windows and usage evidence; remove runtime fallbacks and dual-write behavior.

**Exit gate:** Supported legacy workspaces migrate through explicit commands and then run canonical-only; unsupported/ambiguous states block with actionable reports.

### Phase 5 — Fully Canonical

**Work:** Route every supported command, workflow, recovery, completion, effect, and migration through target authorities. Complete P4 reachability and operational observation.

**Exit gate:** Alternate implementations are unnecessary for production or certification; weighted score is at least 4/5 in every subsystem.

### Phase 6 — Legacy-Free

**Work:** Remove old Execute, Plan, and Roadmap implementations and tests; remove duplicate catalogs/wrappers/contracts; remove expired compatibility; consolidate projects and public surfaces; enable permanent architecture gates.

**Exit gate:** P5 post-removal build, full tests, publish, command matrix, migration matrix, restart/fault matrix, and architecture checks pass. Every subsystem scores 5/5.

### 17.1 Global optimization rules

- Strict historical debris may be removed in parallel only when it carries no unresolved product intent and no proof dependency.
- Reconnect behavior only through its target authority. Do not temporarily restore an old composition root.
- Build shared identity, store, effect, and recovery foundations before migrating workflows that depend on them.
- Prefer one complete vertical convergence slice over many half-wired abstractions, but do not let a slice invent local persistence or recovery.
- Delete per behavior at P5; delete whole legacy projects when all contained behaviors reach P4 and post-removal certification can establish P5.
- The Roadmap body remains last among large legacy removals because it contains the broadest storage/migration and semantic-validation specification, not because its architecture is the target.

## 18. Current Repository Evidence Summary

The current assessment is grounded in these facts at the recorded revision:

- `LoopRelay.Cli` is the only supported production host and constructs the canonical resolver/runtime/controller/chain path.
- `CanonicalWorkflowDefinitionSketches` defines TraditionalRoadmap, EvalRoadmap, Plan, Execute, and their product convergence.
- Retired Roadmap and Plan projects still compile extensive implementations and tests behind non-functional entrypoints.
- `LoopRunner` and `ExecutionStep` remain compiled/tested but are not constructed by the unified host.
- Canonical workflow persistence is active, while loop history, completion resolution, files, SQLite compatibility, archives, and exports do not share one selected authority.
- Some declared effects, notably Plan publication/gitlink behavior, are not executed by the active Plan path.
- Execution implementation-to-handoff continuity depends on one live in-memory session.
- Storage commands and active verification do not certify the full pre-unification import/export/sync and conflict behavior.
- Completion, policy, telemetry/quota/input-wait, human interaction, and runtime configuration retain bypassed or distributed behavior.
- Audit 1 measured at least 21,000 lines across more than 300 source/prompt assets outside production reachability, including 17,366 lines in retired Roadmap/Plan bodies and about 381 lines/assets in the old Execute loop.

These facts explain the current 42% estimate. They do not constrain the target names, module boundaries, data model, or authority decisions defined above.

## 19. Required Decisions Before Implementation Planning

The target architecture resolves the shape of the end state. The following product/release inputs still need explicit values before a milestone plan can be finalized:

1. The first architecture version and versioning policy for workflows/products.
2. Supported legacy format/version inventory and real consumer/workspace inventory.
3. Not-before dates/releases and usage thresholds for each compatibility obligation.
4. Policy values for telemetry retention/export, usage-limit waiting, input-wait reporting, workspace isolation, and approval/HITL behavior.
5. Supported provider capability matrix for persistent resume/read/fork and structured output.
6. Whether product bodies reside in SQLite BLOBs or a `.LoopRelay/objects` content store; the logical Workspace State Store remains the sole authority either way.
7. The exact assembly consolidation map within the seven-project budget.

These choices tune implementations and support windows. They do not reopen the core contracts: one authority, one kernel, one state/history owner, one effect protocol, one recovery protocol, one completion authority, and one expiring compatibility boundary.

## 20. Audit Progression and Verdict

| Audit | Primary question | Primary output |
|---|---|---|
| 1 | What is disconnected from production? | Reachability inventory |
| 2 | How should disconnected implementation be retired while preserving behavior? | Behavioral retirement strategy |
| 3 | What is the optimal canonical architecture, what invariants define it, how far is the repository from it, and what evidence proves convergence? | Architectural convergence model and enduring design contracts |

**Verdict:** LoopRelay has a credible canonical orchestration nucleus, but it has not yet converged on the target architecture. Deletion safety must be subordinate to authority convergence. Future changes should be accepted only when they move a behavior toward its registered target owner, preserve the canonical invariants, improve the scorecard with evidence, and reduce rather than create compatibility or ownership ambiguity.
