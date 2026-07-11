# Comprehensive Evaluation Fixture Ecosystem Audit

## Audit scope and method

This audit is the planning input for a future roadmap. It identifies what a fixture ecosystem must account for; it does not define an implementation, prescribe code, sequence milestones, or estimate effort.

The audit is based on the active unified CLI, the retained pre-unification workflow services, shared orchestration primitives, prompt assets, persistence schemas, completion and recovery services, repository documentation, known-risk records, and the full test suite. The executable baseline observed on 2026-07-10 was 1,576 passing tests, five skipped tests, and no failures. The five skipped tests are all live Codex approval/protocol checks.

Repository paths in this document are repository-relative. “Fixture” has two independent parts: a **Fixture Repository**, which is the intentionally tiny codebase and its stable repository-owned inputs, and a **Fixture Scenario**, which applies workflow, persistence, Git, provider, configuration, interruption, and oracle conditions to that repository. A repository snapshot alone cannot deterministically create every provider, timing, process, Git-remote, or quota failure. The two dimensions must remain orthogonal so one repository can support many scenarios without becoming many nearly identical repository copies.

## 1. Executive summary

Loop Relay now has a strong canonical orchestration model and unusually broad component-level tests. The active `LoopRelay.Cli` composes four workflow identities—`TraditionalRoadmap`, `EvalRoadmap`, `Plan`, and `Execute`—into two declared chains. It resolves repository state, runs one canonical transition at a time, re-observes the repository, evaluates gates, persists transition evidence to SQLite, and advances across workflow boundaries. Production execution uses real Codex app-server sessions through one-shot, persistent, warm, decision, read-only, and permission-scoped postures.

The current validation suite proves many individual contracts but does not prove the assembled system against real Codex on realistic repository and Git topology. Most workflow tests inject fake or scripted agent runtimes. The only checked-in real-Codex fixture certifies a narrow Codex 0.142.5 protocol identity and promotes exact-ID resume/read support; it does not execute a Loop Relay workflow or certify the skipped approval behavior. Consequently, the largest confidence gap is not missing unit coverage. It is the absence of cheap, repeatable, end-to-end evidence that production composition, real prompts, real sessions, real filesystem effects, real SQLite state, real Git effects, interruption boundaries, and completion behavior agree.

The highest-value fixture ecosystem is therefore not a collection of miniature application test suites. It is a compositional matrix of tiny base repositories and orthogonal scenarios chosen to expose orchestration behavior. The suite needs:

- a small number of full-chain smoke repositories;
- many stage-targeted repositories that begin immediately before a high-risk transition;
- persistence and recovery snapshots for every authority and interruption classification;
- live Codex protocol/capability certification separated from semantic workflow validation;
- deterministic structural and state oracles, with semantic tolerance only where model-authored prose makes exact comparison inappropriate;
- Git and publication topologies that prove `.agents` and parent-repository behavior without relying on an uncontrolled network;
- explicit normalization, privacy, version, cost, and flake policies.

The unit of coverage should be a composed repository/scenario case, but the unit of maintenance should remain the smallest independently meaningful component. A base repository should be reusable with persistence, workflow-state, Git, interruption, provider, permission/configuration, and oracle overlays. Coverage should be measured against architectural dimensions—transitions, recovery boundaries, persistence domains, failure modes, provider capabilities, workflow chains, oracles, and authority interactions—not by counting fixture directories or executed cases.

Several production seams deserve fixture attention because unit tests can pass while assembled behavior remains incomplete or ambiguous:

1. `UnifiedCliComposition` activates definitions from `CanonicalWorkflowDefinitionSketches`; the “sketches” are production authority despite their name.
2. `WriteExecutablePlan` and `RevisePlan` share an in-memory planning session. A process restart between them leaves the revised-plan transition without its warm session.
3. `ExecuteImplementationSlice` and `GenerateHandoff` likewise share an in-memory execution session. A restart between them cannot recreate the held-open session from repository evidence alone.
4. Decision sessions have extensive SQLite-backed continuity and recovery machinery, but production compatibility currently certifies only resume/read. Reconstruction and native fork remain gated because conversation write, context capacity, and lost-response reconciliation are not certified.
5. Workflow-boundary evidence is held by an in-memory `WorkflowBoundaryEvidenceWriter`; a SQLite table and store API for chain runs exist, but the active chain runner does not connect to them.
6. The unified CLI’s active `FileSystemStorageVerifier` checks database readability, schema support, and partial workflow transactions, but the richer stale-export, conflict, reference, round-trip, and domain verification services live in the retained Roadmap persistence layer. Active `storage import/export/sync` commands mostly ensure schema and metadata rather than exercising those richer domain transformations.
7. `EvalRoadmap` plans from evaluation specifications; it is not an evaluation runner. There is no general production architecture that executes fixture evals, orders them by the generated DAG, records hypothesis verdicts, retries evals, aggregates results, or turns them into certification readiness.
8. Completion certification performs multiple prompts, archive mutations, and roadmap-context updates inside one canonical transition. The repository’s known-risk inventory already records partial archive, rerun, archive-index, and partial completion-context hazards.
9. Production Plan authoring and Execute sessions use `danger-full-access`, network access, and no approval. Fixture isolation is therefore a primary safety boundary, not merely test hygiene.
10. The active unified completion composition constructs `CompletedEpicArchiveService` without the available SQLite archive materializer, and constructs certification without an execution-evidence store. Fixtures must distinguish supported archive components from components actually wired into the public path.
11. Repository observation reports Git repository/branch facts but currently records `HasWorkingTreeChanges` as false rather than querying the working tree. Dirty-tree fixture behavior must therefore be validated at the public resolution boundary.

The future roadmap should treat these seams as validation obligations, not assume that the existence of component tests proves active end-to-end wiring.

## 2. Current architectural observations

### 2.1 Project and authority structure

| Area | Current repository authority | Fixture relevance |
|---|---|---|
| Agent process and Codex protocol | `src/LoopRelay.Agents` | Real process launch, JSON-RPC framing, streaming, approval requests, thread identity, resume/read/fork gates, cancellation, and teardown |
| Canonical workflow model/runtime | `src/LoopRelay.Orchestration.Primitives` | Workflow identity, products, stages, gates, transition lifecycle, chaining, resolution, recovery, and canonical persistence |
| Unified public CLI | `src/LoopRelay.Cli` | Production composition, command routing, transition executors, re-observation, exit codes, status, unblock, storage commands, and session lifetime |
| Traditional roadmap domain | `src/LoopRelay.Roadmap.Cli` | Rich roadmap artifacts, selection, split lineage, lifecycle, provenance, transition journal, storage import/export/sync/verify, and retained migration readers |
| Plan domain | `src/LoopRelay.Plan.Cli` | Warm authoring, adversarial review, scoped operations, preflight, publication, and retained pipeline behavior |
| Completion | `src/LoopRelay.Completion` | Evaluation parsing, policy, routing, blocker evidence, archive materialization/recovery, synthesis, and context update |
| Projection system | `src/LoopRelay.Projections` | Nine-file Project Context contract, prompt projections, provenance, validation, freshness, and manifest migration |
| Permissions | `src/LoopRelay.Permissions` | Approval parsing, path scoping, command policy, hard denies, configuration, and provider request adaptation |
| Shared filesystem/Git support | `src/LoopRelay.Core`, `src/LoopRelay.Infrastructure` | Artifact containment, SQLite schema, execution evidence, Git publication, diagnostics, and trust evidence |

The public executable is `LoopRelay.Cli`. The Plan and Roadmap project files remain in the solution and retain services/tests, but their program entry points are retired. Fixtures must test the unified composition rather than directly invoking a retained state machine and assuming the public path behaves identically.

### 2.2 Canonical workflow topology

The active definitions are created by `CanonicalWorkflowDefinitionSketches.CreateAll()` and wrapped by the workflow definition classes in the CLI projects.

| Workflow | Stages | Important transitions | Exit products |
|---|---|---|---|
| TraditionalRoadmap | Roadmap Context; Strategic Initiative Selection; Epic Preparation; Milestone Specification; Workflow Completion | bootstrap/update context, select initiative, audit/create/split/realign/reimagine/retire epic, generate deep dives, verify Plan entry | `PreparedEpic`, `MilestoneSpecificationSet` |
| EvalRoadmap | Evaluation Selection; Dependency Inventory; Hypothesis Inventory; Architectural Catalog; Eval DAG; Next Epic Roadmap; Active Epic Preparation; Milestone Specification; Workflow Completion | select eval intent, create/refresh inventories, create catalog/DAG/roadmap, create active epic, generate deep dives, verify Plan entry | the same `PreparedEpic` and `MilestoneSpecificationSet` |
| Plan | Planning; Plan Validation; Execution Preparation; Workflow Completion | write plan, generate adversarial projection, review, revise, generate operational context, collect/refine details, generate milestones, verify Execute entry | `ExecutablePlan`, `OperationalContext`, `ExecutionDetails`, `ExecutionMilestoneSet`, `ExecutionReadiness` |
| Execute | Execution Readiness; Implementation Planning; Implementation; Execution Continuity; Completion; Workflow Completion | verify readiness, generate/transfer/continue decisions, execute slice, handoff, context update, publish, commit evaluation, milestone evaluation, non-implementation review, completion certification, route interpretation, verify exit | `CertifiedCompletion` |

The declared chains are TraditionalRoadmap → Plan → Execute and EvalRoadmap → Plan → Execute. Default selection is based on whether `.agents/evals` contains evaluation intent files; flags and bounded subcommands override selection.

### 2.3 Canonical transition lifecycle

`TransitionRuntime` owns the lifecycle:

1. resolve the definition and input products;
2. evaluate the input gate;
3. build prompt context and an input snapshot hash;
4. render the prompt;
5. persist transition start;
6. execute the prompt posture;
7. capture raw output;
8. interpret output;
9. evaluate the output gate;
10. validate products;
11. execute ordered effects;
12. resolve eligible successors;
13. persist completion.

Durable states distinguish start, prompt completion, interpretation, output validation, effects applied, partial effects, completion, block, failure, cancellation, and stall. Blockers, gate evaluations, effect records, raw output, transition evidence, and recovery markers have separate SQLite representations. Fixtures can therefore validate much more than process exit: they can verify the entire durable transition story.

The runtime catches persistence failures for supporting evidence in several places so the returned transition result remains available. This makes fault scenarios particularly important: a fixture must determine which record is authoritative when primary state, evidence, blocker, or recovery-marker writes diverge.

### 2.4 Resolution and chaining

`RepositoryObserver` combines filesystem artifacts and SQLite canonical rows into workflow, product, lifecycle, transition, Git, human-interaction, and storage observations. `WorkflowResolver` selects a workflow and stage and derives eligible transitions from product usability and prior completed transition runs. Artifact existence alone is not intended to prove completion.

`WorkflowController` chooses the first eligible transition. `WorkflowChainRunner` evaluates exit, product-transfer, and downstream entry gates before crossing a workflow boundary. `UnifiedCliRunner` executes at most one transition per chain-run call, re-observes after each completed transition, and repeats up to 32 transitions for an unbounded invocation. Bounded invocations stop after one workflow result.

The boundary writer is process-local. The database schema includes `canonical_workflow_chain_runs` and the canonical persistence store can write it, but active production composition does not use that store for chain boundaries. A fixture should make this difference visible when process restarts occur at a workflow boundary.

Git observation is also deliberately shallow in the current observer: it recognizes `.git` and reads `HEAD`, but its working-tree-change flag is not populated from `git status`. Commit/change detection later uses separate process-driven services. Fixtures should verify which layer owns each Git fact and prevent resolver output from being mistaken for commit-gate evidence.

### 2.5 Prompt and execution posture architecture

Prompt assets under `src/LoopRelay.Core/Prompts` are compiled by the prompt source generator and expose source hashes. The unified renderer combines those templates with resolved product and context sections. Eval prompts have a dedicated catalog; core prompts have a canonical prompt catalog. Local verification and local artifact transitions deliberately avoid a model call.

Production execution postures are materially different and must not be collapsed into one “Codex works” fixture:

| Posture | Current use | Production authority |
|---|---|---|
| One-shot process | Eval and TraditionalRoadmap authoring, milestone deep dives, projection generation, completion prompts and archive synthesis | Usually workspace-write; model output returned at process end |
| Persistent read-only session | adversarial review | app-server thread, no writes/network/approval |
| Warm planning session | initial plan and revision | `danger-full-access`, network allowed, approval never; in-memory continuity |
| Permission-scoped artifact session | projection/details/milestone operations | read-only app-server posture with approval requests constrained by an operation profile and rollback transaction |
| Decision session | execution decisions and recommendation | read-only thread plus extensive SQLite continuity/recovery accounting |
| Warm execution session | implementation slice and handoff | `danger-full-access`, network allowed, approval never; in-memory continuity |
| Local transition | verification, operational-context seed, commit/milestone evaluation, route interpretation | deterministic process/filesystem/Git behavior without a prompt turn |

Real fixtures must certify both the prompt outcome and the posture boundary. A semantically correct artifact produced through an unauthorized write path is not a successful result.

### 2.6 Session lifecycle and continuity

`AgentRuntime` launches Codex, registers held-open sessions by repository/session identity, and makes teardown single-sited through the registry. `CodexAppServerSession` initializes JSON-RPC, starts or resumes a thread, streams public deltas and tool events, answers approval requests through the permission gateway, tracks transport progress, and exposes provider thread/turn IDs.

Decision continuity has a substantially deeper model:

- stable scope identity derived from workspace, prepared epic, executable plan, role, and contract version;
- lineage and active pointers;
- occupancy and cost accounting;
- per-turn write/submission/acceptance/terminal/commit/materialization states;
- exact profile capture;
- recovery attempt journal and immutable recovery plans;
- thread-read, rollout, and repository recovery sources;
- textual reconstruction and native-fork mechanisms;
- fail-closed treatment of unknown provider side effects;
- status rendering without session content.

The embedded Codex manifest currently certifies version 0.142.5 plus one app-server schema digest. Resume, `excludeTurns`, and read are supported. Write, fork, and maximum recoverable context remain unknown, so textual reconstruction and native fork are implemented but normally ineligible. Provider reconciliation is explicitly not implemented for the active profile.

### 2.7 Operational context and execution preparation

The Project Context source contract is exactly nine numbered files under `.agents/ctx`. Projection freshness depends on prompt identity/source hash, Project Context hash, and causal inputs. Projections reject runtime-state headings and require a fixed structural contract.

Operational context has two active meanings:

- Plan’s canonical `GenerateOperationalContext` copies the validated plan to `.agents/operational_context.md` as a deterministic local artifact.
- Decision transfer later produces an operational delta, evolves operational context through scoped operations, optimizes present operational documents, and archives the consumed delta.

The retained Roadmap services also model richer execution-preparation provenance over active epic, milestone specs, decision ledger, operational context, execution prompt, compatibility plan, and milestones. The unified observer retains migration awareness of these artifacts, but canonical Plan/Execute use `.agents/plan.md`, details, milestones, operational context, decisions, recommendation, and readiness products. Fixtures must prevent legacy `execution-prompt.md` or compatibility artifacts from falsely advancing the active workflow.

### 2.8 Persistence and storage authority

The shared database is `.LoopRelay/persistence/looprelay.sqlite3`, schema version 3. Its domains include:

- schema/workspace metadata and sync markers;
- roadmap state, decision ledger, lifecycle, split families, projection and preparation manifests, and transition journal;
- loop history, execution evidence, completed-epic archives, and workflow transactions;
- canonical workflow/stage/transition/product/gate/effect/blocker/recovery/chain rows;
- decision resume and telemetry;
- continuity profiles, scopes, lineage, active state, recovery plans/attempts/sources, decision turns, correlations, and legacy imports.

Authority is distributed across four locations:

1. SQLite is canonical for structured runtime, decision continuity, execution history, and telemetry.
2. `.agents` is repository-owned workflow material and compatibility/export surface.
3. `.LoopRelay/evidence` contains local verification and stall evidence used by the unified path.
4. Codex rollout/session storage is external provider evidence and is not repository-owned.

The full Roadmap persistence layer can import/export/sync many structured domains, detect stale exports and conflicts, validate references and archive metadata, and run non-mutating round-trip verification. The active unified verifier is narrower. Fixture assertions must be tied to the public composition, not merely to the existence of the richer services.

There are also several “ledger” concepts with different authorities: the roadmap decision ledger, live/rotated execution decisions plus their recommendation, the non-implementation review ledger, and canonical transition evidence. Fixtures and expected-artifact inventories must name these explicitly; treating them as one generic decision ledger would hide loss or cross-domain contamination.

### 2.9 Artifact generation, publication, and Git effects

Loop Relay treats `.agents` publication and parent Git recording as workflow effects. The publisher assumes `.agents` can be operated as its own Git repository with a branch and upstream, then commits the parent gitlink. It also distinguishes `.agents` bookkeeping from substantive repository progress. The audit workspace itself tracks `.agents` as ordinary files rather than as a gitlink, illustrating why fixture topology must be explicit instead of inferred from folder name.

Execute publication can commit and push both `.agents` state and implementation changes. Completion archives move or copy live planning/execution artifacts and may materialize SQLite histories into the archive. Fixture repositories must be isolated from production remotes and must verify the Git graph, not just the working tree.

SQLite archive materialization exists as a component, but active unified completion construction does not pass it to `CompletedEpicArchiveService`. The same active construction does not pass a SQLite execution-evidence store into certification. A fixture must report the behavior of this active composition rather than assuming all completion components participate.

### 2.10 Telemetry and replay

Per-turn telemetry records prompt/output/cached tokens, effective cost, quota snapshots, transport timing, first protocol/output timing, model, estimates, provider thread/turn IDs, and continuity fields. SQLite is canonical; rotating JSONL is compatibility output. Telemetry failures are best-effort and must not fail a turn, while caller cancellation propagates.

Replay facilities are narrower than the term may suggest:

- Codex rollout parsing reconstructs a bounded public record projection;
- thread-read and rollout sources feed recovery envelopes;
- canonical transition evidence preserves raw output and state;
- compatibility fixtures preserve scrubbed capability facts and digests.

There is no general workflow replay engine that deterministically re-executes a recorded run without Codex. The roadmap must decide whether “replay” means parser replay, recovery-context reconstruction, state-machine re-observation, or full workflow simulation.

### 2.11 Evaluation and certification are distinct

`EvalRoadmap` consumes `.agents/evals` and creates dependency, hypothesis, catalog, DAG, next-roadmap, epic, and milestone planning artifacts. It does not execute those eval specifications. Completion certification is a different path: it evaluates epic completion and drift with a model, parses a strict Markdown decision, applies a deterministic policy/router, archives the epic, synthesizes archive evidence, and updates roadmap completion context.

The future fixture ecosystem therefore needs two distinct evaluation concepts:

- evaluation of Loop Relay behavior by the fixture harness;
- Loop Relay’s own EvalRoadmap and completion-evaluation products.

Conflating them would allow the system under test to author its own sole oracle.

## 3. Existing validation capabilities

### 3.1 Executable baseline

| Test assembly | Passed | Skipped | Primary coverage |
|---|---:|---:|---|
| Permissions | 81 | 0 | parsing, policy, gateway, adapters, hard denies, configuration |
| Agents compatibility | 4 | 0 | scrubbed Codex identity fixture and manifest promotion |
| Agents | 132 | 5 | process/session/protocol/streaming/permissions/continuity parsers; five live tests skipped |
| Projections | 9 | 0 | context contract, layering, freshness, generation |
| Infrastructure | 8 | 0 | artifact sequence/store, prerequisites, trust |
| Core | 27 | 0 | filesystem artifacts, logical resolution, SQLite schema/evidence |
| Orchestration primitives | 321 | 0 | contracts, runtime, resolver, chain, persistence, recovery, non-implementation review |
| Plan CLI | 108 | 0 | pipeline, warm session, scoped artifacts, publication, preflight |
| Completion | 14 | 0 | certification, policy, routing, archive and failure outcomes |
| Roadmap CLI | 496 | 0 | roadmap state, persistence, provenance, splits, storage, failures, unblock |
| Unified CLI | 376 | 0 | public parsing/composition, canonical workflows, decisions, execution, completion, telemetry |

These tests provide a strong regression net for deterministic components and failure classification.

### 3.2 Particularly strong existing capabilities

- Canonical workflow definitions have invariant validation.
- Transition runtime tests cover missing/stale inputs, malformed/missing output, product invalidity, partial effects, cancellation, blockers, and persistence failures.
- Resolution tests distinguish fresh, active, resumable, blocked, waiting, cancelled, failed, completed, ambiguous, corrupt, and unsupported states.
- Chain tests prove boundary gates and bounded/unbounded selection with fake definitions.
- Roadmap tests cover selection provenance, split families, lifecycle, prompt contracts, deep dives, promotion, unblock, cancellation, and failure preservation.
- SQLite tests cover schema creation, import/export equivalence, stale files, conflicts, corrupt rows, unsupported schema, partial transactions, concurrency smoke, orphaned references, legacy migration, deterministic serialization, and read-only verification.
- Decision continuity tests cover exact resume classification, persisted accounting, committed-output rehydration, recovery planning, recovery journals, reconstruction/fork mechanisms, and unknown-outcome behavior.
- Completion tests cover semantic policy vocabulary, routes, blocker evidence, archive materialization/recovery, and known partial-archive risks.
- Permission tests cover many command and tool-call forms, while checked-in issue files preserve known bypass and false-decline risks.
- Telemetry tests cover SQLite/JSONL sinks, rotation, quota parsing, retry waits, token accounting, and best-effort failure.

### 3.3 Existing fixture-like assets

The only formal checked-in fixture directory is `tests/LoopRelay.Agents.Compatibility.Tests/Fixtures`. Its Codex 0.142.5 record contains booleans and digests only and intentionally excludes thread IDs, prompts, responses, credentials, and rollout bodies. A release run can point at an explicit Codex binary and disposable `CODEX_HOME`; ordinary runs validate the checked-in evidence.

Roadmap tests also contain in-code samples and filesystem snapshots, but these are component fixtures rather than tiny repositories executed by the public CLI with real Codex.

### 3.4 Limits of current validation

- Canonical workflow tests instantiate fake/scripted `IAgentRuntime` implementations.
- Git behavior is generally tested with scripted process runners rather than real repositories, branches, upstreams, and remotes.
- Most SQLite tests call stores directly rather than enter through the public CLI and then inspect all resulting domains.
- Prompt tests validate templates and contracts, not whether real Codex consistently emits usable products from a tiny repository.
- Live approval tests are skipped.
- The compatibility fixture validates provider methods, not Loop Relay semantic workflows.
- There is no cost, latency, token, flake, or repeated-run distribution baseline for real workflow execution.
- There is no cross-platform real-session matrix.

## 4. Validation gaps

### 4.1 Assembly and production-wiring gaps

1. No fixture proves a default public invocation selects the intended chain, runs real Codex transitions, crosses both workflow boundaries, executes repository changes, certifies completion, and remains closed on rerun.
2. No fixture proves every bounded command through the published executable and production settings loader.
3. No fixture proves that the active lightweight storage verifier exposes the same safety conditions promised by the richer retained verifier.
4. No fixture proves the public storage commands’ observable import/export/sync semantics across filesystem and SQLite authorities.
5. No fixture proves chain evidence survives a process boundary; active boundary evidence is in memory.
6. No fixture proves status and unblock after real partial runs, including continuity ancestry and operator action.
7. No fixture proves dirty-tree resolution facts; the repository observer and commit/change services currently derive Git facts through different paths.
8. No fixture proves that SQLite loop history/execution evidence is included in archives through the active unified completion composition.

### 4.2 Real prompt/product gaps

1. No real-session corpus establishes whether tiny TraditionalRoadmap inputs reliably yield each valid authoring disposition.
2. No real-session corpus establishes whether EvalRoadmap outputs satisfy structural, traceability, DAG, and convergence contracts.
3. No real-session test proves the warm Plan session writes and then revises the same plan without losing required files or violating authority.
4. No real-session test proves scoped operations request precise approvals, mutate only declared outputs, and roll back invalid writes.
5. No real-session test proves decision output and recommendation remain a valid bound pair.
6. No real-session test proves an execution slice changes only the fixture workspace, checks the intended milestone, and creates a valid handoff.
7. No real-session test proves completion prompts produce parseable, policy-coherent decisions and archive synthesis.

### 4.3 Cross-process lifecycle gaps

The canonical runtime persists transition state but not every live session dependency:

- Restart after `WriteExecutablePlan` but before `RevisePlan` re-observes the plan product, yet the new process has no `planAuthoringSession`; the executor explicitly requires that in-memory session.
- Restart after `ExecuteImplementationSlice` but before `GenerateHandoff` re-observes implementation evidence, yet the new process has no `executionSession`; handoff generation explicitly requires it.
- In-memory fields also carry changed paths, pre/post milestone counts, repository-slice baseline, completion result, and recovery evidence across Execute transitions.

Fixtures must cover every process boundary where canonical state says a transition completed but the next executor expects memory from the prior transition.

### 4.4 Recovery gaps

- Real exact-ID resume has protocol evidence but not a full interrupted Execute fixture.
- Failed resume classification has fake coverage, not provider-backed unavailable/corrupt session scenarios.
- Reconstruction is inactive for the certified profile because write and context-limit evidence are unknown.
- Native fork is inactive because lost-response reconciliation is unknown; `ReconcileAsync` is not implemented.
- Provider-turn unknown outcomes, write-started-but-not-accepted turns, and committed-but-not-materialized decisions need public-CLI recovery evidence.
- Planning, handoff, publication, archive, and context-update recovery are not covered by a unified real-session harness.
- A repository snapshot cannot cause deterministic interruption timing by itself; the fixture architecture needs a separately controlled fault boundary.

### 4.5 Evaluation architecture gaps

There is no general eval executor, dependency scheduler, retry model, hypothesis-verdict store, result aggregator, certification-readiness calculation, or fixture-result schema. EvalRoadmap’s DAG is a planning artifact, not an executable graph. A future roadmap must decide whether these are Loop Relay product responsibilities, fixture-harness responsibilities, or separate authorities with a defined handoff.

### 4.6 Certification gaps

- Model-authored certification is parsed and policy-checked, but repository truth is not independently re-evaluated by the fixture harness.
- Partial, continue, reopen, and gather-evidence routes become blocked outcomes once milestones are exhausted; there is no demonstrated automatic path from those routes back into execution through the unified CLI.
- Archive plus synthesis plus context update spans multiple side effects under one canonical transition.
- Known risks remain for partial archive materialization, rerun after archival, archive index collision, and partial context update.
- No real-session fixture proves evidence completeness, rejection, approval/close, already-certified discovery, or recovery from an interrupted certification.

### 4.7 Git, permissions, and isolation gaps

- Real `.agents` repository/upstream and parent gitlink behavior is not exercised end to end.
- The audit workspace shows `.agents` can also be a normal tracked directory, while production publisher semantics assume a nested repository/submodule.
- Production authoring and execution use unrestricted postures; no fixture proves confinement to a disposable workspace.
- Five live approval tests remain skipped.
- Checked-in issues record force-push configuration bypass, wrapper-command bypass, redirection/mutating-safe-command bypass, path-like content false declines, and incomplete scoped-operation protocol certification. Even if individual code paths evolve, these remain fixture scenarios until active end-to-end evidence closes them.
- Network and remote Git behavior can introduce nondeterminism, external side effects, credentials, and cost.

### 4.8 Determinism and oracle gaps

- There is no canonical normalization contract for timestamps, IDs, paths, hashes derived from dynamic inputs, Git SHAs, token counts, provider versions, or model prose.
- There is no declared acceptable-variation policy for real Codex output.
- There is no rule for when a semantic oracle may be model-assisted, nor an independent deterministic backstop.
- There is no repeated-run flake threshold or quarantine policy.
- There is no versioned relationship between fixture input, expected artifacts, Codex compatibility profile, prompt hashes, database schema, and oracle version.

### 4.9 Fixture-dimensionality and coverage-accounting gaps

- Repository content and runtime scenario are not yet formalized as separate fixture axes.
- Without that distinction, SQLite snapshots, workflow stages, interruptions, provider profiles, Git topologies, and permission settings can become duplicated repository variants.
- There is no composability model for applying independent state/topology/fault/oracle conditions to one base repository.
- There is no declared compatibility or precedence model for overlays that affect the same authority.
- Coverage is described extensively by tests and scenarios, but there is no canonical architectural coverage ledger or denominator.
- Raw fixture count, test count, or run count can therefore rise without increasing transition, recovery, authority-interaction, provider, or failure-mode confidence.

## 5. Complete fixture opportunity inventory

The following inventory identifies distinct confidence opportunities, not a list of repository directories. Each row is a candidate **Fixture Scenario** that may reuse an existing **Fixture Repository**. For example, the same one-file executable repository can be Execute-ready, no-progress, interrupted-after-work, SQLite-only, Git-divergent, or provider-incompatible depending on the overlays applied to it.

| Opportunity | Minimal starting condition | Loop Relay behavior exposed | Primary oracle |
|---|---|---|---|
| Fresh empty repository | Git repository, no `.agents`, no SQLite | default Traditional selection, missing context handling, no false progress | CLI/status/state invariants |
| Minimal Project Context | exact nine `.agents/ctx` files | projection generation and context contract | exact source set plus structural projection |
| Extra/missing context file | one source absent or unexpected numbered file present | fail-closed projection preflight | exact blocker and no mutation |
| Traditional roadmap seed | minimal roadmap completion context and selection evidence | selection, audit, epic creation | transition route plus valid product structure |
| Existing valid epic | active epic with valid lifecycle/provenance | audit/reuse and deep-dive path | no duplicate authoring; producer/freshness invariants |
| Split-required epic | oversized or explicitly separable epic evidence | split interpretation, lineage, child promotion | graph/reference/lifecycle oracle |
| Realign/reimagine/retire states | targeted active/selected evidence | alternative preparation routes and selection supersession | route table and durable decision ledger |
| Eval intent repository | one tiny `.agents/evals/*.md` | default Eval selection and serial analysis chain | workflow identity, artifact graph, structural/traceability oracle |
| Multiple eval intents | two distinct tiny specs | selection determinism/ambiguity | allowed selection contract and recorded provenance |
| Eval dependency blocker | source with deliberate missing prerequisite | dependency/hypothesis blocked status | source traceability and non-pass invariants |
| Eval DAG edge case | tiny cycle, missing edge, or conflicting direction | DAG rejection/condensation behavior | graph oracle |
| Roadmap convergence pair | Traditional and Eval seeds describing the same tiny capability | producer-agnostic Plan entry | equivalent product contract, not prose equality |
| Plan-ready seed | valid prepared epic and milestone specs | Plan entry and first authoring turn | entry gates and plan structure |
| Warm Plan run | clean Plan input | same-thread write/review/revise behavior | thread identity, plan mutation, transition sequence |
| Interrupted Plan warm session | stop after initial plan transition | cross-process revision recovery | durable state and safe next action |
| Invalid plan output | missing/empty plan or forbidden product shape | prompt success is not product success | failure/blocker and rollback |
| Scoped details operation | plan requiring one tiny details artifact | approval scoping and declared output | exact write set and approval transcript |
| Scoped milestone operation | one milestone only | strict checkbox contract, mutation transaction | file-set, checkbox, rollback invariants |
| Execute-ready tiny project | one source file, one deterministic acceptance signal, one milestone | entry verification and first execution | repo diff, milestone, decision, handoff |
| Pending decisions | valid live decisions/recommendation | skip/continue path and recommendation binding | no redundant decision turn; pair hash |
| Fresh decision session | no decisions, valid handoff/context | new scope/thread/decision and accounting | SQLite scope/lineage/turn plus artifacts |
| Decision resume | active canonical scope and reachable provider thread | exact resume and context non-resend | same provider thread, zero replacement |
| Failed resume | active scope with unavailable provider thread | classification and fail-closed/replacement policy | attempt journal and no duplicate turn |
| Committed decision not materialized | SQLite output committed, artifact absent | output rehydration without resubmission | no provider turn; artifact hash matches stored output |
| Unresolved decision turn | submitted/accepted/unknown nonterminal state | duplicate-submission prevention | fail-closed status and correlation evidence |
| Decision transfer pressure | seeded accounting at transfer threshold | delta, context evolution, planned successor | lineage, accounting, delta history, no old active pointer |
| Implementation with change | one trivial required edit | work turn, change detection, handoff | bounded diff and handoff existence |
| Implementation without change | already-satisfied tiny task | no-change handoff and stall accounting | no false substantive progress |
| Interrupted implementation/handoff | stop after work before handoff | cross-process execution continuity | preserved diff and safe recovery action |
| Publication topology | nested `.agents` repository plus isolated upstreams | submodule commit/push and parent pointer | Git object/branch/upstream graph |
| Publication failure | unavailable/non-fast-forward/stranded upstream condition | surfaced failure and salvage/retry behavior | Git state plus workflow failure evidence |
| Pure milestone progress | only `.agents/milestones` checkbox changes | substantive-progress exception | no stall; exact checkbox delta |
| No-progress sequence | repeated no-op slices | durable stall threshold across invocations | persisted count and exit code 3 |
| Non-implementation candidate | execution creates a tiny prose/policy-like file | classification, semantic confirmation, ledger/HITL | file disposition and ledger identity |
| Explicit HITL request | decision output contains marker | capture and review ownership | provenance and no unauthorized implicit approval |
| Completion-ready | all strict checkboxes checked | non-implementation completion review and certification | evaluation structure and deterministic policy route |
| Certification rejection | coherent incomplete/negative evidence | block/reopen/gather route | parser/policy/router plus no archive close |
| Partial archive | seeded/preserved intermediate archive evidence | recovery and collision behavior | live/archive file and metadata invariants |
| Already certified | completed archive and canonical closed product | idempotent rerun and continuity retirement | no Codex session; exit success; closed state unchanged |
| Filesystem-only legacy state | `.agents` structured exports, no DB | observation/migration behavior | non-mutating classification |
| Canonical SQLite only | DB rows with selected exports absent | canonical resolution and logical artifact reads | normalized row/state oracle |
| Mixed fresh state | DB plus matching exports | authority classification and safe mutation | hash/reference consistency |
| Stale/conflicting mixed state | both authorities changed | conflict block | no mutation and exact conflict domains |
| Corrupt/unsupported DB | invalid file, row hash, or schema version | fail-closed verification | exit code 4 and byte-for-byte preservation |
| Partial workflow transaction | non-completed marker | mutation block and recovery reporting | transaction/status evidence |
| Legacy resume conflict | legacy file plus canonical continuity rows | conflict/quarantine/import rules | canonical rows and legacy-file disposition |
| Telemetry enabled/disabled | one tiny real turn | SQLite canonical event and JSONL compatibility | normalized event fields and kill switch |
| Usage-limit failure | controlled provider diagnostic | wait/retry/cap without duplicate semantic progress | attempt count, timing class, telemetry |
| Malformed provider output | completed turn with invalid product | interpretation/validation block | raw evidence retained, no product advancement |
| Process death/cancellation | controlled boundary before/during/after provider turn | cancellation, unknown outcome, cleanup | process/session registry and durable state |
| Path/permission adversary | allowed target plus content containing paths; disallowed target; wrapper/redirection commands | precise approval and hard-deny behavior | exact requested path/command decision |
| Cross-platform clone | same fixture on Windows and Unix-like environment | newline/path/shell/Git behavior | normalized equivalence |
| Provider/profile drift | unsupported Codex version/schema | compatibility fail-closed behavior | zero gated protocol calls and diagnostic |

## 6. Fixture repository taxonomy and scenario composition

### 6.1 Orthogonal fixture model

The fixture ecosystem has two primary axes that must be independently identifiable and reusable:

| Axis | Owns | Does not own | Examples |
|---|---|---|---|
| **Fixture Repository** | stable tiny codebase, deterministic acceptance signal, minimal dependency/project metadata, repository-owned source inputs | transient workflow stage, SQLite runtime rows, provider thread, interruption timing, remote failure, run-specific oracle | empty repo, text transformation, tiny library, tiny CLI, already-satisfied capability |
| **Fixture Scenario** | initial orchestration state and all non-content conditions needed to expose one behavior | arbitrary application complexity or a copied repository for every state combination | Plan interrupted after write, corrupt SQLite, detached `.agents`, unsupported Codex profile, scoped-write denial, completion rerun |

A case is the composition of one repository and one scenario. Repository identity answers “what tiny capability is Codex operating on?” Scenario identity answers “under what Loop Relay and external state is it operating?” Neither should silently absorb the other.

This separation is especially important for state that is expensive or impossible to express as ordinary tracked files: canonical SQLite rows, Codex thread identity, Git upstream/ref topology, process interruption point, quota/provider response, environment settings, and expected normalization/oracles.

### 6.2 Taxonomy by repository content

1. **Null repositories**: empty or nearly empty, used to prove selection and missing-input behavior.
2. **Text-only capability repositories**: a tiny source/expected-output pair with no package restore, used for low-cost implementation and diff behavior.
3. **Tiny executable repositories**: one command with deterministic success/failure, used when execution and evaluation must run rather than only inspect files.
4. **Tiny library repositories**: one pure function and one local test, used to distinguish implementation change from artifact-only progress.
5. **CLI repositories**: one argument/output contract, used for command execution and negative controls.
6. **Intentionally failing repositories**: a deterministic failing check with one obvious correction.
7. **Malformed repositories**: missing project metadata, invalid artifact contracts, unsafe paths, or ambiguous state.
8. **Already-satisfied repositories**: implementation complete but orchestration state behind, used for no-change and certification behavior.

The suite should resist multiplying languages and frameworks unless a platform/provider boundary requires it. Application complexity does not increase orchestration confidence proportionally.

### 6.3 Taxonomy by orchestration maturity

- fresh/no Loop Relay state;
- Project Context only;
- roadmap context and selection;
- prepared epic only;
- epic plus milestone specifications;
- partial Plan products;
- Execute-ready products;
- active decision scope;
- partial implementation slice;
- completion-ready live state;
- partial archive;
- certified/archived closed state;
- migrated legacy state.

### 6.4 Taxonomy by authority shape

- filesystem only;
- SQLite only;
- matching mixed authority;
- stale export;
- conflicting dual change;
- corrupt database or corrupt row;
- unsupported schema;
- missing storage;
- incomplete transaction;
- external provider state present, missing, or ambiguous.

### 6.5 Taxonomy by expected outcome

- completes and advances;
- completes but remains bounded;
- waits;
- blocks with recoverable action;
- fails;
- cancels;
- stalls;
- reports ambiguity;
- closes and remains idempotently closed.

### 6.6 Taxonomy by external topology

- no Git repository;
- local Git repository with clean/dirty/detached state;
- `.agents` as ordinary directory;
- `.agents` as nested repository/submodule with upstream;
- isolated parent and `.agents` remotes;
- unavailable or divergent upstream;
- supported/unsupported Codex binary and profile;
- authenticated/unauthenticated disposable `CODEX_HOME`;
- network denied/allowed posture.

### 6.7 Fixture composability

A composable case can be reasoned about as a base repository plus independent scenario overlays. “Overlay” here is an architectural responsibility, not a prescribed file format or merge mechanism.

| Composable element | Responsibility | Representative variation |
|---|---|---|
| Base repository | tiny capability, deterministic acceptance signal, minimal project surface | empty, one-change, already-satisfied, intentionally failing, malformed |
| Workflow/artifact overlay | `.agents` products, lifecycle, projection, selected workflow/stage, partial products | PreparedEpic ready, partial Plan, Execute-ready, completion-ready |
| Persistence overlay | canonical/legacy SQLite and filesystem authority | empty DB, SQLite-only, stale export, corrupt row, unresolved turn, partial transaction |
| Git overlay | repository, `.agents`, refs, branches, upstreams, remotes, working-tree state | clean, dirty, detached, submodule, divergent upstream, lost push response |
| Interruption overlay | controlled stop/fault boundary and expected retry/reconcile classification | before submission, after acceptance, after validation, during effects, before handoff |
| Provider overlay | Codex identity, capability profile, authentication/session availability, quota/response condition | certified resume, unsupported schema, missing thread, unknown fork outcome, usage limit |
| Configuration/permission overlay | settings, environment switches, operation profile, trust posture | resume disabled, recovery policy, allowed target, denied command, network posture |
| Oracle overlay | normalization and assertions relevant to the scenario | structural Markdown, SQLite snapshot, Git graph, protocol transcript, semantic invariant |

Composability must preserve several constraints:

- each overlay declares which authority it changes and which assumptions it requires;
- incompatible overlays are rejected or explicitly ordered rather than merged accidentally;
- overlay application cannot mutate the reusable base repository;
- scenario identity changes when any behavior-bearing overlay changes;
- reset removes materialized runtime/provider/Git state without modifying the base;
- an oracle overlay cannot alter the system-under-test inputs it evaluates;
- coverage attribution identifies the repository, every overlay, and the architectural obligations exercised;
- common overlays remain reusable across repositories, workflows, providers, and platforms when their contracts allow it.

For example, one “single deterministic change” repository can be combined with Execute-ready workflow state, then independently with a clean or divergent Git topology, a normal or interrupted handoff boundary, a supported or unsupported provider profile, and state/Git/protocol oracle sets. The roadmap should avoid materializing every combination as a permanent repository variant.

### 6.8 Variant-control implications

The roadmap must decide which dimensions genuinely change repository semantics and which are scenario overlays. A new repository is justified when the codebase or acceptance signal must differ. A new scenario is justified when the same codebase is observed under different orchestration, persistence, Git, provider, permission, fault, or oracle conditions. This distinction prevents Cartesian repository growth, shared-state leakage, and contradictory copies of the same tiny project.

## 7. Workflow coverage inventory

### 7.1 Cross-workflow and CLI coverage

Every workflow fixture matrix must include:

- workflow chaining, transition ownership, product/output propagation, and automatic progression;
- default, forced-chain, and bounded invocation modes;
- status before, during, blocked, waiting, failed, cancelled, stalled, and completed states;
- exit codes 0, 1, 2, 3, 4, and 130 where applicable;
- selection evidence and ignored/conflicting evidence;
- entry gate, output gate, exit gate, product transfer, and downstream entry gate;
- automatic re-observation after each completed transition;
- the 32-transition continuation guard;
- process restart before and after workflow boundaries;
- unknown/missing definition or transition references;
- no eligible transition and multiple/ambiguous state evidence;
- `unblock` with recoverable, non-recoverable, and stage-less blockers;
- status rendering of storage authority, gates, blockers, next transition, and continuity ancestry;
- storage verification before mutation;
- bounded commands proving that no downstream workflow starts;
- chained commands proving that downstream workflows do start only from validated transferred products.

### 7.2 TraditionalRoadmap coverage

| Coverage area | Required fixture outcomes |
|---|---|
| Completion-context bootstrap/update | absent, valid, stale, malformed, archived-evidence input, prompt failure, product missing after prompt |
| Selection | one valid selection, malformed selection, stale provenance, superseded selection, completion-context drift, roadmap drift, retired initiative exclusion, explicit HITL marker |
| Existing epic audit | ready, insufficient evidence, blocked evidence, prompt failure, audit output persisted without false blocker |
| Epic creation | valid promotion, blocked output, ambiguous output, structurally invalid output, prompt completion without artifact, lifecycle persistence |
| Rewrite paths | realign and reimagine success, invalid replacement, preserved prior active epic, selection fallback rules |
| Split | no files, spec-only output, path traversal, duplicate paths, direct active-epic target, mixed valid/invalid children, valid ordered children, family persistence and selected-child promotion |
| Retirement | stable identity, deduplication, completion-context update, selection supersession, next selection after restart |
| Milestone deep dives | valid specs, zero specs, spec ownership mismatch, partial bundle write, malformed checklist, prompt/context failure |
| Plan entry | exact `PreparedEpic`/`MilestoneSpecificationSet` convergence, stale provenance, legacy artifacts ignored, bounded completion |

Roadmap evolution also needs longitudinal fixtures: an initial roadmap decision, repository change, completion-context update, revised selection, and next epic must retain causal history without reusing stale projections or selections.

The real-session oracle should not insist that Codex chooses a particular discretionary preparation route unless the fixture makes that route contractually unique. Otherwise the oracle should accept a defined route set and validate the chosen route’s state/evidence invariants.

### 7.3 EvalRoadmap coverage

| Coverage area | Required fixture outcomes |
|---|---|
| Intent discovery/selection | no eval files, one file, multiple files, empty/malformed intent, forced eval with no usable intent |
| Dependency inventory | exhaustive source traceability, safe-failure dependency, forbidden non-implementation dependency, missing/invalid output, refresh against repository state |
| Hypothesis inventory | one-to-many dependency translation, confirmation/falsification/inconclusive definitions, missing dependency coverage, refresh status |
| Architectural catalog | every eligible item indexed once, same-plane grouping, dependency/falsifiability separation, unresolved ambiguity |
| Eval DAG | node and edge traceability, acyclicity, topological layers, cycle/conflict handling, negative controls, machine-gate obligations |
| Next-epic roadmap | earliest unresolved frontier, bounded slice, dependency order, hypothesis acceptance, no invented implementation detail |
| Active epic | canonical `.agents/epic.md` path, required headings/table, evaluation traceability, no false implementation/certification claims |
| Deep dives | reuse of universal transition and valid Plan-entry products |
| Refresh/evolution | dependency, hypothesis, and roadmap status changes after repository mutation; causal invalidation |
| Convergence | Plan accepts Eval products without branching on producer identity |

Because current EvalRoadmap transitions are serial even though definitions can represent multiple eligible successors, the fixture roadmap must preserve graph/topology coverage for future parallel or branching evaluation execution.

### 7.4 Plan coverage

| Coverage area | Required fixture outcomes |
|---|---|
| Entry | valid products from each roadmap producer, stale/invalid/ambiguous products, partial input set |
| Write plan | real file creation, empty/missing product, unrelated writes, prompt failure, cancellation |
| Adversarial projection | generated versus reused, stale prompt hash, Project Context drift, invalid projection, manifest migration |
| Read-only review | real output, empty output, attempted mutation, session teardown, source-hash evidence |
| Revision | same-thread warm session, revised product validation, session death, restart between write and revision |
| Operational context | deterministic plan seed, missing plan, idempotent regeneration, invalidation after plan change |
| Details collection/refinement | declared input guards, precise write approvals, path-like content, rollback on invalid output, optional details behavior |
| Milestone extraction | one/multiple files, strict checkboxes, zero files, no checkboxes, extra writes, rollback, reduced milestone count superseding old artifacts |
| Publication | `.agents` commit, push, stranded commit recovery, parent gitlink, no-change publication, failure visibility |
| Exit | exact five-product entry contract for Execute, producer/freshness/causal identity, bounded stop, partial-stage resume |

### 7.5 Execute coverage

| Coverage area | Required fixture outcomes |
|---|---|
| Readiness | missing plan/details/context/milestones/readiness, malformed checkboxes, outstanding blocker, valid entry |
| Decision planning | fresh scope, resume, continuation, transfer, recommendation validation, projection injection once, accounting restoration, stale scope rejection |
| Implementation | first execution, decision-driven continuation, declared model/effort, real change, milestone-only change, no change, failed command, cancellation, unrelated change |
| Handoff | changes/no-changes prompt selection, required live file, held-open same thread, restart after work, failed handoff turn |
| Continuity artifacts | handoff rotation, decision retirement, operational delta creation/archive, context evolution, history sequence |
| Non-implementation review | no candidates, semantic candidate, cached disposition, HITL request, allowed auxiliary file, forbidden file, review failure |
| Publication and commit | `.agents` publication, source commit/push, bookkeeping exclusion, detached/divergent branches, salvage on abnormal exit |
| Stall | progress resets count, repeated no-op persists count across invocations, exact threshold, explicit recovery requirement |
| Milestone completion | some unchecked, all checked, zero trackable checkboxes, completion after only checkbox progress |
| Certification | completed, blocked, failed, reject/reopen/gather evidence, archive/context update, partial side effects |
| Exit and rerun | `CertifiedCompletion`, continuity scope retirement, chain completion, no new session on rerun |

## 8. Session lifecycle coverage inventory

### 8.1 Process and transport

- executable discovery from default and `CODEX_EXECUTABLE`;
- missing, wrong, and incompatible executable;
- one-shot argument construction, stdin completion, zero/nonzero exit, stderr tail, long output, and tool events;
- app-server initialize/start/resume/read/fork frames and exact profile gates;
- stdout/stderr flood without deadlock;
- non-JSON noise, malformed frames, missing IDs, duplicate events, and stream truncation;
- token usage present/absent, cached input, estimator fallback, and quota probes;
- request write-started, submitted, accepted, provider-turn identified, terminal, and unknown transport boundaries;
- process death before request, after write, after acceptance, during output, and after terminal response;
- cancellation at all transport boundaries;
- registry ownership, close, double-close protection, repository isolation, and process-tree teardown.

### 8.2 Session roles and authority

Each role needs a live assertion of model, effort, sandbox, network, approval policy, working directory, and allowed effects:

- Planning authoring;
- Planning review;
- Operational one-shot;
- Scoped artifact operation;
- Decision;
- Execution.

The fixture oracle must verify actual provider frames and observed side effects. Checking the constructed `AgentSessionSpec` alone is insufficient.

### 8.3 New, warm, resumed, replaced, and transferred sessions

- lazy fresh thread creation and eager continuity creation;
- multiple turns in one process with stable thread identity;
- plan warm-session reuse and teardown;
- execution work/handoff reuse and teardown;
- exact decision resume after CLI restart;
- resume disabled with and without active continuity state;
- deterministic protocol failure without silent replacement;
- retryable resume failure and bounded retry;
- unavailable/corrupt session eligibility for replacement;
- planned transfer successor and parent/root lineage;
- provider reconstruction with full, selective, summary, and repository-only completeness;
- native fork only under a certified profile;
- lost-response reconciliation and multiple-child ambiguity;
- scope invalidation when prepared epic or executable plan changes;
- active scope retirement on certified completion.

### 8.4 Context restoration and privacy

- decision projection sent on fresh session and omitted on warm reuse;
- operational context and handoff restoration;
- repository recovery source ordering and budgets;
- exact-thread rollout selection;
- truncated-tail and malformed-middle recovery behavior;
- secret, environment dump, base64, external path, encrypted reasoning, and hidden-reasoning sanitization;
- duplicate recovery items and explicit omissions;
- budget overflow and mandatory repository content that cannot fit;
- marker/digest validation for injected context;
- checked-in evidence containing no credentials or session content.

## 9. Evaluation coverage inventory

### 9.1 Fixture harness evaluation obligations

The future harness needs independent answers to:

- Did the intended workflow/stage/transition execute?
- Were the correct prompt asset and source hash used?
- Did real Codex run under the intended posture?
- Were required products created and validated?
- Did forbidden artifacts or side effects occur?
- Did canonical and exported state agree?
- Did recovery resume rather than duplicate work?
- Did the repository acceptance signal change as intended?
- Did the run produce sufficient evidence to explain pass, fail, block, wait, cancel, stall, or ambiguity?
- Did a second run remain idempotent?

These answers must not depend solely on the prose written by the same Codex session being evaluated.

### 9.2 Loop Relay EvalRoadmap obligations

The roadmap must account for:

- executable interpretation of eval specifications versus planning-only analysis;
- dependency ordering and invalid dependency graphs;
- hypothesis status: confirmed, falsified, blocked, inconclusive, and not run;
- negative controls and false-success tests;
- retry eligibility and retry exhaustion;
- evidence production, retention, and provenance;
- result aggregation without erasing individual eval outcomes;
- mapping eval evidence to roadmap evolution;
- readiness for completion certification;
- distinction between a missing evaluator capability and a failing product capability.

### 9.3 Eval failure and retry classes

- command/test exits nonzero;
- evaluator cannot start;
- evaluator times out or is cancelled;
- expected evidence missing or malformed;
- dependency not satisfied;
- hypothesis inconclusive;
- negative control unexpectedly passes;
- result conflicts with prior result;
- flaky/non-repeatable result;
- provider or quota failure unrelated to repository capability;
- retry repeats an already-applied side effect;
- partial result survives process restart.

No production store currently represents this complete vocabulary, so the fixture roadmap must establish where these outcomes live and who owns them.

## 10. Certification coverage inventory

### 10.1 Inputs and evidence

- active epic exists and is structurally valid;
- one or more strict milestone files exist;
- completion claim records the trigger and milestone set;
- Project Context projection is fresh;
- execution, non-implementation, blocker, and evaluation evidence is discoverable;
- archived SQLite histories are associated with the correct epic;
- completion decision references repository reality rather than only checked boxes;
- evidence paths remain resolvable before and after archive.

### 10.2 Decision vocabulary and routes

Fixtures must cover every policy combination that is valid or deliberately invalid:

- completion: fully complete, functionally complete, partially complete, not complete, inconclusive;
- drift: none, positive, negative, mixed, unknown;
- recommendation: close, close with follow-up, continue, reopen, gather more evidence;
- coherent close and coherent non-close routes;
- contradictory recommendation/status/drift combinations;
- unknown vocabulary and malformed Markdown tables;
- approval/close, rejection, partial certification, and evidence-gathering behavior.

### 10.3 Archive and closure

- clean archive index selection;
- gaps, nonnumeric entries, existing synthesis files, and collisions;
- copy/move set for epic, plan, details, context, milestones, decisions, deltas, handoffs, reviews, and evidence;
- SQLite materialization hashes and metadata;
- archive synthesis success/failure;
- roadmap completion context bootstrap/update success/failure;
- live input preservation or explicit recoverability on partial failure;
- final canonical closed product and route evidence;
- continuity retirement;
- second invocation with no Codex work;
- archive recovery with and without metadata.

## 11. Persistence coverage inventory

### 11.1 SQLite domains

Every schema domain needs canonical-row, corruption, missing-reference, export/import, and evolution consideration where applicable:

- `schema_metadata`, `workspace_metadata`, `sync_markers`;
- roadmap state, decision ledger, lifecycle, split families and children/order;
- execution-preparation, selection-provenance, and projection manifests;
- transition journal;
- loop history and execution evidence;
- completed-epic archive metadata/records;
- workflow transactions;
- canonical workflow, stage, transition, evidence, product, gate, effect, blocker, recovery, and chain state;
- legacy decision resume and telemetry;
- continuity profiles, scopes, lineage, active pointers, recovery plans/attempts/sources, decision turns, correlations, and legacy imports.

### 11.2 Authority and synchronization

Fixtures must distinguish:

- missing storage versus valid empty storage;
- filesystem observation without mutation;
- import into a new database;
- canonical database with deleted exports;
- export regeneration;
- stable export-import-export round trip;
- scoped sync dependencies;
- stale export after database change;
- database/export conflict after both change;
- optional missing manifests versus required missing domains;
- legacy Markdown migration without silently mutating source during verification;
- duplicate identities, case-variant paths, invalid sequence names, orphaned evidence, bad archive metadata, and unresolved transition outputs;
- unsupported earlier/future schema versions;
- concurrent writers and optimistic row-version conflicts;
- incomplete workflow transaction classification;
- verification byte-for-byte non-mutation.

### 11.3 Filesystem and logical artifacts

- path containment and traversal rejection;
- case sensitivity differences;
- newline and encoding preservation;
- numbered history allocation and gaps;
- logical resolution from SQLite when exports are absent;
- retained filesystem versus migrated-domain authority;
- live/rotated handoff, decisions, recommendation, and delta semantics;
- `.LoopRelay/.gitignore` create-only behavior;
- telemetry JSONL compatibility rotation;
- cleanup/reset without touching user-owned files outside the fixture root.

### 11.4 Observable active-wiring questions

The public fixture suite must explicitly compare claimed command semantics with active wiring:

- whether unified `storage import` imports domain data or only establishes a schema marker;
- whether unified `storage export` materializes exports or deliberately performs no filesystem mutation;
- whether unified `storage sync` reconciles domains or only ensures schema usability;
- whether the active verifier detects stale exports, conflicts, corrupt rows, bad references, and nondeterministic round trips;
- whether chain-run rows are written during public workflow execution.

These are audit findings requiring roadmap decisions, not assumptions about intended behavior.

## 12. Failure recovery coverage inventory

### 12.1 Failure boundary matrix

Every prompt-bearing transition class should be interruptible at these conceptual boundaries:

1. before definition/input resolution;
2. after input resolution but before input gate;
3. after prompt context but before prompt render;
4. after render but before start persistence;
5. after start persistence but before process launch;
6. after request write begins;
7. after request submission/acceptance;
8. after partial output;
9. after provider terminal result but before raw-output persistence;
10. after raw output but before interpretation;
11. after interpretation but before output gate;
12. after product validation but before effects;
13. during each ordered effect;
14. after effects but before completion persistence;
15. after completion persistence but before repository re-observation;
16. after re-observation but before workflow-boundary transfer;
17. after boundary transfer but before the next workflow transition.

The expected recovery classification differs by boundary. Before submission, retry may be safe. After an uncertain provider side effect, duplicate submission must be prevented until reconciliation. After validated products, recovery may need to apply or verify effects without rerunning the prompt. Fixtures need an oracle for all three cases.

### 12.2 Recoverable repository conditions

- missing or repaired Project Context;
- malformed then corrected execution disposition/certification;
- missing artifact restored from canonical SQLite;
- stale projection regenerated;
- partial scoped operation rolled back;
- incomplete split family or promotion;
- publication commit present but push response lost;
- parent pointer not recorded;
- implementation changed but handoff missing;
- handoff present but publication missing;
- decision output committed but artifact missing;
- active continuity pointer conflict;
- partial archive with live inputs, copied inputs, or metadata;
- completion context update absent after archive;
- cancelled transition with durable output;
- stalled Execute after operator correction;
- usage limit after a failed turn.

### 12.3 Non-recoverable or fail-closed conditions

- unsupported schema/profile;
- corrupt canonical authority without a trusted source;
- ambiguous provider side effect;
- multiple candidate fork children;
- mismatched scope/product causal identity;
- untrusted recovery envelope or marker mismatch;
- invalid hard-deny/permission boundary;
- conflicting dual authorities with no chosen source;
- completed certification evidence contradicted by repository state.

### 12.4 Reproducibility boundary for failures

Artifact corruption, partial rows, stale exports, and pre/post-transition state can be encoded in repository fixtures. Exact process death, cancellation timing, provider response loss, quota exhaustion, push response loss, and network interruption require controlled runtime conditions. The roadmap must define a scenario authority that can reproduce those boundaries without making the fixture repository itself complex or nondeterministic.

## 13. Oracle strategy recommendations

### 13.1 Exact comparison

Appropriate for:

- fixture seed files and reset hashes;
- prompt asset identity and source hash;
- rendered deterministic prompts after path/newline normalization;
- CLI exit code and stable status fields;
- expected file presence/absence and allowed write set;
- structured JSON schemas and enum values;
- deterministic serialization with normalized dynamic fields;
- permission decisions for exact command/path requests;
- SQLite schema, table set, foreign-key/reference invariants, and selected normalized rows;
- transition/effect ordering;
- recovery plan digest when all inputs are canonicalized;
- Git branch/upstream relationships and commit-parent topology.

Exact comparison is generally inappropriate for full real-Codex Markdown prose or raw SQLite database bytes.

### 13.2 Structural comparison

Appropriate for:

- roadmap, epic, plan, milestone, projection, evaluation, handoff, blocker, and certification Markdown;
- required headings and table columns;
- checkbox counts and IDs;
- JSON documents with dynamic fields;
- telemetry records;
- archive directory shape;
- workflow status explanations.

Structural checks should validate required/forbidden sections, cardinality, references, types, and path ownership without coupling to incidental wording.

### 13.3 Semantic comparison

Appropriate for:

- traceability from eval specification → dependency → hypothesis → catalog → DAG → roadmap → epic → milestone;
- whether a plan actually addresses the tiny requested capability;
- whether a handoff describes the observed slice rather than inventing work;
- whether completion evidence agrees with the repository acceptance signal;
- whether non-implementation artifacts were correctly classified;
- whether a recovery envelope preserves enough public context.

Semantic comparison should be constrained by deterministic facts and invariants. A second model may provide additional signal, but it should not be the sole pass/fail authority for a real-model fixture.

### 13.4 Invariant verification

High-value invariants include:

- prompt success never advances without valid products;
- no downstream workflow starts before its entry gate;
- one transition run has one durable terminal classification;
- effect order matches the definition;
- no undeclared filesystem write escapes the fixture root;
- no duplicate provider turn occurs for the same transition/input snapshot;
- active decision scope matches current epic/plan causal identities;
- canonical hashes match stored bodies;
- completed/archived artifacts remain discoverable;
- certification close requires coherent policy and independent repository evidence;
- verification is non-mutating;
- rerun after closure performs no model turn.

### 13.5 State and SQLite verification

State oracles should inspect a normalized logical snapshot rather than database file bytes. They should cover workflow/stage state, transition durable state, products, gate evaluations, effects, blockers, recovery markers, chain boundaries, decision scope/lineage/turns, history, evidence, archives, telemetry, and unresolved transactions. Foreign keys and logical references matter as much as row values.

### 13.6 Workflow and graph verification

The oracle should reconstruct the observed workflow graph and compare:

- selected chain/workflow/stage;
- eligible/completed transitions;
- input/output products and causal identities;
- boundary transfers;
- allowed successor set;
- terminal outcome;
- Eval DAG acyclicity and traceability;
- split-family and recovery-lineage parent/child relationships.

### 13.7 Golden output policy

Good golden candidates are deterministic prompts, normalized status output, schemas, fixture inputs, structured examples, and scrubbed provider capability evidence. Real Codex artifacts should normally use structural and semantic contracts, with exemplar goldens retained for diagnostic comparison rather than brittle byte equality.

### 13.8 Replay policy

Scrubbed provider transcripts and rollout projections can reproduce parsers, normalization, recovery-source selection, and unknown-frame handling at low cost. Persisted workflow snapshots can reproduce resolver and unblock behavior. Neither should be counted as proof that the current Codex binary, model, permission protocol, or full workflow still works live. Live and replay evidence need separate labels.

## 14. Expected artifact inventory

### 14.1 Planning and roadmap artifacts

| Artifact | Expected comparison |
|---|---|
| `.agents/ctx/01..09-*.md` | exact fixture input and exact source-set contract |
| projections and projection manifest | structural content; exact identity/source/causal hashes after normalization |
| roadmap completion context | structural/semantic plus freshness and archive references |
| selection and selection provenance | structural plus exact selected identity and causal inputs |
| active epic | structural/semantic; required headings/table; one active identity |
| milestone specifications | structural/semantic; ownership and dependency order |
| split family and children | exact graph/reference/state invariants |
| roadmap state/lifecycle/decision ledger | normalized structured equality and append ordering |
| prompt contracts and transition journal | exact identities/hashes/order with dynamic-field normalization |

### 14.2 EvalRoadmap artifacts

| Artifact | Expected comparison |
|---|---|
| selected evaluation | exact source identity; structural selection rationale |
| dependency inventory | structural/semantic traceability and exhaustive source coverage |
| hypothesis inventory | structural/semantic dependency coverage and falsifiability |
| architectural catalog | graph grouping invariants and item uniqueness |
| eval DAG | graph oracle, acyclicity, edge/node traceability |
| next-epic roadmap | structural/semantic frontier and boundedness |
| eval evidence | exact transition/prompt/source metadata; structural model output |

### 14.3 Plan and execution artifacts

| Artifact | Expected comparison |
|---|---|
| plan | structural/semantic capability coverage |
| adversarial projection/review | structural plus source/freshness identity |
| operational context | exact initial seed where deterministic; semantic after transfer evolution |
| details | structural/semantic and allowed-path set |
| execution milestones | exact checkbox/ID counts plus semantic acceptance |
| execution readiness | exact gate/product evidence |
| decisions and recommendation | semantic decision plus exact recommendation binding/hash/model/effort |
| decision history | exact sequence and correlation; structural body |
| handoff history | exact sequence; structural/semantic body |
| operational deltas | exact sequence; semantic context changes |
| implementation evidence | exact run/lineage/thread correlations; structural output |
| repository diff | exact allowed files and semantic required change |

### 14.4 Review, completion, and archive artifacts

| Artifact | Expected comparison |
|---|---|
| repository slice baseline/post snapshot | exact normalized file facts |
| non-implementation review/ledger/decisions/synthesis | structural plus exact identity/disposition state |
| completion claim | exact trigger/input paths with timestamp normalization |
| completion evaluation | strict structural parser contract and semantic repository agreement |
| blocker evidence | exact category/route references; structural prose |
| archive synthesis | structural/semantic summary and exact archive linkage |
| archive metadata | exact logical/export path and content hash set |
| roadmap completion update | semantic closure plus exact archive/evidence references |
| `CertifiedCompletion` | exact canonical product/state invariants |

### 14.5 Runtime and operational artifacts

- base Fixture Repository identity and reset hash;
- Fixture Scenario identity, including every applied overlay and version;
- composition evidence showing authority ownership, compatibility checks, and overlay application order where order is behavior-bearing;
- architectural coverage obligations claimed by the case and the evidence level actually achieved;
- normalized canonical SQLite snapshot;
- filesystem export snapshot;
- `.LoopRelay/.gitignore`;
- local verification/stall evidence;
- transition input snapshots and raw-output evidence;
- workflow boundary and chain evidence;
- session telemetry SQLite rows and JSONL compatibility rows;
- Codex compatibility profile and evidence digest;
- recovery plan, attempts, sources, lineage, turns, and correlations;
- Git refs, commits, tree paths, submodule/gitlink state, and upstream reachability;
- process/session cleanup evidence;
- fixture run result and cost/latency summary, whose authority the roadmap must define.

## 15. Determinism considerations

### 15.1 Stable inputs

Fixtures should minimize nondeterminism from repository complexity: tiny files, explicit acceptance signals, no ambient user configuration, no package download, no wall-clock business logic, no network dependency, no random data, no parallel mutation, and no irrelevant project choices. Repository reset must restore both files and Git/SQLite/provider scenario state.

The reusable base repository and the materialized composed case have different determinism contracts. The base must have a stable content/reset identity. The scenario must have stable overlay identities and declared dynamic fields. The materialized case may contain run-specific IDs and timestamps, but normalization must trace them back to the base and scenario that produced them.

### 15.2 Values requiring normalization

- absolute repository, temp, user-home, Codex-home, and rollout paths;
- Windows/Unix separators and case behavior;
- CRLF/LF and UTF-8 console behavior;
- timestamps and durations;
- GUIDs, workspace IDs, transition run IDs, session IDs, scope/lineage/attempt/turn IDs;
- provider thread/turn/request IDs;
- Git commit SHAs and generated branch names;
- SQLite autoincrement values where relative ordering is the contract;
- token counts, cached-token counts, quota percentages, and retry timestamps;
- archive indices when prior archive topology varies;
- model prose, incidental ordering, and diagnostic wording;
- Codex version/schema/profile digest when the test is not specifically a compatibility test.

Normalization must be versioned. Otherwise an oracle change can silently make old failures disappear.

### 15.3 Acceptable and unacceptable variability

Acceptable variability may include prose wording, explanatory ordering that is not contractually significant, token usage, timing, and generated IDs. Unacceptable variability includes workflow selection, required artifact paths, required headings/table schemas, permission decisions, undeclared writes, product validity, causal relationships, transition/effect ordering, certification policy coherence, and duplicate provider turns.

### 15.4 Model and provider drift

The current default settings identify a model and effort, while provider protocol support is pinned by exact binary/schema evidence. Fixtures need to record both semantic model configuration and transport compatibility identity. A semantic regression, protocol regression, prompt regression, and fixture-oracle regression are different failure classes and should remain distinguishable.

### 15.5 Repeated-run evidence

One successful real run establishes reachability, not determinism. The roadmap must define repetition expectations for high-variance transitions, acceptable route sets, flake classification, retry limits, and when a provider drift requires recertification rather than a fixture update.

## 16. Cost optimization opportunities

### 16.1 Token cost

- Start most fixtures immediately before the transition under audit by seeding already-validated upstream products.
- Reserve full-chain runs for a small smoke set; use stage-targeted runs for branch and failure coverage.
- Keep Project Context, eval specs, roadmap, epic, plan, and source files minimal while retaining required structure.
- Avoid multiplying repositories where the same content can represent multiple authority/failure states.
- Reuse one base repository across orthogonal workflow, persistence, Git, provider, interruption, configuration, and oracle scenarios.
- Select overlay combinations because they cover an authority interaction or failure obligation, not because every Cartesian combination exists.
- Use deterministic local oracles instead of additional model calls wherever possible.
- Use replay for parser and normalization regressions while retaining periodic live certification.
- Separate provider capability certification from semantic workflow runs so protocol facts are not repeatedly rediscovered with tokens.
- Measure prompt size by transition and detect context growth, especially decision transfer and completion prompts.

### 16.2 Execution time

- Prefer dependency-free acceptance signals and local Git remotes.
- Avoid restore/build steps when a text-only capability provides the same orchestration signal.
- Keep each implementation slice singular and obvious.
- Run independent replay/unit tiers broadly and live tiers selectively according to the confidence question.
- Reuse a live session only when session reuse is the behavior being tested; otherwise isolate transitions.

### 16.3 Storage

- Retain normalized logical snapshots and scrubbed evidence rather than complete disposable workspaces where diagnostics do not require them.
- Bound rollout/public transcript evidence and never retain hidden reasoning or credentials.
- Deduplicate fixture bases and record scenario deltas conceptually.
- Retain reusable bases and behavior-bearing overlays separately from disposable materialized cases.
- Define retention for successful telemetry, failed-run artifacts, archives, and provider evidence.

### 16.4 Maintenance and human review

- Prefer invariant and structural oracles over large prose goldens.
- Keep each fixture’s confidence purpose singular and explicit.
- Detect unused fixtures, duplicate coverage, stale prompt hashes, unsupported profiles, and orphaned expected artifacts.
- Surface concise normalized diffs: workflow/state, files, SQLite rows, Git graph, and semantic contract violations.
- Require human review only for genuinely semantic ambiguity or provider drift, not timestamps and IDs.

### 16.5 CI duration and quota

The roadmap should define separate evidence tiers: hermetic deterministic checks, replay/protocol transcript checks, low-cost live transition checks, full-chain live smoke, and compatibility/recovery certification. Tiering is necessary to prevent quota or provider outages from turning every code change into an uninformative failure while still preventing replay-only false confidence.

## 17. Scalability considerations

### 17.1 New workflows and identities

Fixture coverage must derive from workflow contracts—identity, stages, transitions, products, gates, effects, dependencies, blockers, recovery, and completion—so a new workflow can expose untested contract elements. Hard-coded assumptions that every chain is Roadmap → Plan → Execute or that only one transition is eligible will not scale.

### 17.2 New artifacts and schemas

The ecosystem needs discoverability of newly registered products, storage representations, prompt assets, SQLite tables/columns, enum vocabulary, artifact paths, and lifecycle states. A new artifact should not be silently absent from snapshots, archive checks, reset logic, or privacy scanning.

### 17.3 New evals and dependency graphs

Adding an eval must account for source traceability, dependency/hypothesis nodes, negative controls, evidence schema, retry semantics, aggregation, and certification relevance. DAG evolution needs cycle detection, invalidation, and compatibility rules.

### 17.4 New execution agents and providers

Provider-neutral session contracts exist, but production behavior is Codex-specific. Future providers may differ in thread identity, resume, fork, read/write, approval protocol, token reporting, hidden content, rollout access, and reconciliation. Fixture results should separate provider-independent workflow invariants from provider-specific capability certification.

### 17.5 Parallelism and concurrency

Definitions support non-linear topology, while active execution is serial. Future parallel transitions introduce database concurrency, effect conflicts, Git merge behavior, shared `.agents` mutation, provider quota coordination, ordering-independent oracles, and cancellation fan-out. The roadmap must preserve a fixture class for these even before parallel execution exists.

### 17.6 Regression prevention

Scalability requires coverage accounting by workflow transition, execution posture, effect category, product, storage domain, failure boundary, provider capability, platform, and outcome. Raw fixture count is not a useful coverage metric because one duplicated happy-path repository can inflate count without covering a new behavior.

### 17.7 Architectural coverage model

Coverage should be a vector of architectural obligations rather than a single fixture or test count. Each dimension needs an explicit denominator, evidence level, and uncovered set.

| Coverage dimension | Coverage unit / denominator | Counted as covered only when |
|---|---|---|
| Workflow transition coverage | every transition in the active production workflow definitions, including route-specific alternatives | the transition is selected through production resolution, executed under its real posture or an explicitly labeled lower evidence tier, and its input/output/effect invariants are asserted |
| Workflow chain coverage | every declared chain, selection mode, boundary, bounded stop, and terminal route | exit, product transfer, downstream entry, output propagation, and automatic progression/stop are observed through the public composition |
| Recovery boundary coverage | every relevant transition/session effect boundary from pre-submission through post-effect persistence | a controlled interruption reaches the named boundary and the next invocation proves the expected retry, resume, reconcile, block, or no-duplicate behavior |
| Persistence domain coverage | every SQLite/filesystem domain and supported operation/authority shape | canonical rows, logical references, hashes, synchronization/migration behavior, non-mutation, and corruption/conflict outcomes are verified as applicable |
| Failure mode coverage | every classified process, prompt, product, permission, Git, storage, provider, evaluation, certification, and archive failure | the active path produces the correct durable outcome, evidence, operator action, cleanup, and retry eligibility |
| Provider capability coverage | each provider operation/parameter/result/reconciliation contract used by a workflow | exact provider identity is captured and live or scrubbed certification proves the request, side effect, result identity, failure classification, and reconciliation claim |
| Oracle coverage | every behavior-bearing artifact, state domain, side effect, and terminal claim | at least one suitable independent exact, structural, semantic, graph, state, or invariant oracle is declared and exercised |
| Authority interaction coverage | every behavior-bearing interaction between filesystem, SQLite, Git, provider, in-memory session state, and configuration | precedence, synchronization, conflict, invalidation, and recovery behavior are asserted for the interaction rather than each authority only in isolation |
| Execution posture coverage | each one-shot, persistent, warm, scoped, decision, read-only, unrestricted, and local posture | actual provider/process frames, trust boundary, session lifetime, allowed effects, and teardown are observed |
| Product/effect coverage | every canonical product, lifecycle, storage representation, and effect category/order | production creates or consumes the product/effect with validated causal identity, authority, freshness, and failure semantics |
| Platform/topology coverage | every supported OS/path/line-ending/Git topology that changes behavior | the same architectural obligation passes under normalized but genuinely different platform/topology conditions |

Evidence strength should remain visible. A useful conceptual scale is:

1. **Uncovered** — no executable evidence.
2. **Deterministic component evidence** — contract/store/parser behavior only.
3. **Replay evidence** — recorded protocol/output/state exercised without the current live provider.
4. **Live transition evidence** — production transition and real provider on a composed fixture case.
5. **Live chain/recovery certification** — public CLI, real provider, real authorities, and restart/idempotency behavior.

These levels are not interchangeable. Ten component tests do not equal one live-transition obligation, and ten live happy paths do not cover one missing recovery boundary.

Coverage accounting also needs anti-gaming rules:

- denominators come from active production definitions, persistence schemas, provider profiles, effect categories, and maintained failure vocabularies rather than a manually convenient fixture list;
- a composed case may satisfy many obligations, but each obligation records the exact assertion/evidence that satisfied it;
- success-path execution does not imply failure or recovery coverage;
- exercising an artifact does not imply its oracle coverage unless the artifact was independently checked;
- replay and live evidence are reported separately;
- unsupported, deferred, or intentionally excluded obligations remain visible with rationale rather than disappearing from the denominator;
- a fixture duplication does not increase coverage unless it adds a new obligation, evidence level, authority interaction, platform, or meaningful semantic variation;
- changes to workflows, products, schemas, prompts, provider capabilities, or failure vocabulary expand or invalidate coverage automatically in the planning model.

The primary report should therefore show covered and uncovered obligation sets by dimension and evidence level. A single aggregate percentage may summarize but must not conceal a zero in a critical dimension such as recovery, authority interaction, or provider capability.

## 18. Architectural risks

| Risk | How it creates false confidence | Required roadmap consideration |
|---|---|---|
| Brittle prose goldens | harmless model wording changes fail; teams loosen checks indiscriminately | structural/semantic oracle boundary |
| Overfitting prompts to fixtures | Codex learns tiny fixture conventions that do not exercise general orchestration | diverse but minimal semantics; contract-focused assertions |
| Prompt coupling | fixture passes only for one prompt phrasing/hash | prompt-version evidence and explicit recertification |
| Implementation coupling | fixtures encode internal classes/stores rather than public behavior | public-CLI and observable-state authority |
| Active/retained authority confusion | direct tests of retired Roadmap/Plan services are mistaken for unified coverage | production-composition labeling |
| Fixture complexity | application bugs dominate orchestration signal | strict minimality and purpose ownership |
| Duplicated fixtures | variants drift and disagree | reusable bases/scenario dimensions |
| Repository/scenario conflation | transient SQLite, Git, provider, or fault state creates copied repositories and contradictory seeds | orthogonal identities and ownership |
| Overlay interaction ambiguity | two overlays silently change the same authority or invalidate each other | compatibility, precedence, and conflict rules |
| Base-state leakage | one scenario mutates the reusable repository or provider/Git state seen by another | immutable base semantics and complete reset boundary |
| Combinatorial explosion | every repository is paired with every scenario even when interactions add no signal | obligation-driven combination strategy |
| Fixture drift | seeds no longer satisfy current product/schema contracts | validation before live execution |
| Provider nondeterminism | semantic variation becomes flake or masks regressions | acceptable-variation and repetition policy |
| Provider drift | binary/model changes invalidate compatibility or behavior | exact profile capture and separate failure class |
| Quota dependence | CI fails for external capacity rather than code | evidence tiers and quota-aware scheduling |
| Network dependence | remote availability, credentials, and data alter results | isolated topology and network posture evidence |
| Unsafe unrestricted sessions | a tiny prompt can affect real files/remotes | disposable workspace and side-effect containment |
| Permission-protocol mismatch | scoped operations hang, over-write, or falsely decline | live approval certification |
| False success from artifacts | polished plans/evidence exist without implementation progress | independent repo acceptance signal |
| False failure from content parsing | path-like prose is treated as file access | exact permission request oracle |
| Storage split-brain | SQLite and exports disagree but fixture reads one side | explicit authority/conflict scenarios |
| Partial transaction masking | final files look correct while durable state is incomplete | row/effect/journal oracle |
| In-memory dependency | restart loses warm session or prior transition facts | cross-process boundary fixtures |
| Replay substitution | recorded protocol passes while current live provider is broken | separate live/replay labels |
| Semantic oracle circularity | model evaluates its own output | deterministic independent invariants |
| Privacy leakage | prompts, rollouts, credentials, or hidden reasoning enter goldens | scrub/retention policy and scanners |
| Cross-platform blind spots | Windows-only paths/shell/Git behavior appears universal | normalized multi-platform evidence |
| Archive/rerun blind spot | first close passes but second invocation restarts work | mandatory idempotent rerun oracle |
| Coverage by fixture count | many happy paths obscure missing failures/branches | contract-dimension coverage accounting |
| Aggregate coverage masking | a high overall percentage hides zero live recovery or provider-capability evidence | per-dimension evidence levels and uncovered sets |
| Schema/oracle migration | expected snapshots are silently rewritten to match regressions | versioned migrations and reviewed deltas |
| Flake quarantine permanence | hard provider failures become ignored forever | ownership and recertification criteria |

The checked-in `issues/` files and `docs/orchestration-known-risks.md` should be treated as mandatory scenario sources. A future issue is not closed from the fixture perspective until the active public path has evidence for it.

## 19. Dependencies that must influence the roadmap

### 19.1 Product and architecture dependencies

- The canonical workflow/product/gate/effect contracts and their evolution.
- The active unified CLI composition and retirement status of old entry points.
- A defined distinction between EvalRoadmap planning and executable eval infrastructure.
- A defined certification authority independent from fixture evaluation authority.
- Persistent representation for all cross-process facts needed by the next transition.
- Observable chain-run/boundary authority.
- Defined semantics for public storage commands and active storage verification.
- Singular completion/closure ownership and resumable archive semantics.
- Stable identities and authority boundaries for Fixture Repositories, Fixture Scenarios, and composed cases.
- Overlay compatibility, precedence, invalidation, and reset semantics across workflow, persistence, Git, interruption, provider, configuration, and oracle state.

### 19.2 Codex and provider dependencies

- `CODEX_EXECUTABLE`, login/authentication, disposable `CODEX_HOME`, analytics policy, and secrets isolation.
- Exact Codex version and app-server schema digest.
- Model/effort availability and configuration authority.
- Thread start/resume/read/write/fork/reconcile capability evidence.
- Approval request shape and exact target-path evidence.
- Token/context limits, quota reset behavior, and provider outages.
- Public rollout availability and privacy boundaries.

### 19.3 Repository and Git dependencies

- Git executable behavior across platforms.
- branch/upstream availability and detached-head behavior;
- `.agents` ordinary-directory versus nested-repository/submodule topology;
- isolated remote authority for `.agents` and parent repositories;
- clean/dirty tree semantics and bookkeeping filtering;
- line-ending, executable-bit, filename-case, and path-length differences;
- no writes or pushes outside disposable fixture authorities.

### 19.4 Persistence dependencies

- SQLite provider/native bundle and schema version;
- foreign-key, hash, transaction, and concurrency behavior;
- filesystem/SQLite authority and migration rules;
- legacy state compatibility window;
- normalization and snapshot schema;
- reset and cleanup of database sidecars, telemetry, provider state, and Git refs.

### 19.5 Test and operational dependencies

- CI platform and credentials model;
- live-test opt-in/required policy;
- cost, token, time, storage, and retention budgets;
- flake classification and rerun policy;
- artifact privacy and redaction review;
- result reporting that distinguishes product regression, provider regression, environment failure, fixture drift, and oracle drift;
- ownership for fixture updates when prompts, workflows, schemas, or providers change.
- an architectural coverage ledger with production-derived denominators, evidence levels, explicit exclusions, and uncovered obligation sets.

## 20. Open architectural questions requiring roadmap decisions

1. Is the fixture harness part of Loop Relay, a separate test executable, or an external certification system?
2. What identities and ownership boundaries distinguish a Fixture Repository, a Fixture Scenario, and one materialized composed case?
3. Which workflows must have a complete live-Codex chain, and which may be certified through stage-targeted live runs plus deterministic chaining tests?
4. What is the minimum full-chain smoke set for TraditionalRoadmap and EvalRoadmap?
5. How are model, effort, Codex binary, schema digest, prompt source hashes, settings, and fixture version bound into one result identity?
6. What provider/model variation is acceptable without updating expected results?
7. Must real-session runs repeat, and what outcome distribution constitutes pass, flake, or regression?
8. May a model-assisted semantic judge affect pass/fail, and what deterministic evidence must backstop it?
9. What normalized logical snapshot is authoritative for SQLite, filesystem, Git, telemetry, and provider correlations?
10. Which dynamic values are normalized, and how is the normalizer version migrated?
11. How are hidden reasoning, credentials, rollouts, prompts, and repository content scrubbed and retained?
12. How are authenticated disposable Codex homes created without copying user session state into artifacts?
13. Which live capability checks are release gates, especially approval scoping, conversation write, context limit, fork, and reconciliation?
14. What is the policy when the installed Codex version/schema has no certified profile?
15. How will an interrupted warm Plan session resume after `WriteExecutablePlan`?
16. How will an interrupted execution session recover after implementation but before handoff?
17. Which other Execute transition inputs currently live only in memory, and what recovery evidence is authoritative after restart?
18. Is workflow-boundary/chain-run persistence required, and which existing SQLite record is its authority?
19. What are the intended public semantics of storage init/import/export/sync/verify?
20. Should the unified CLI use the richer Roadmap storage verification/synchronization services or deliberately expose a narrower contract?
21. Who owns executable eval scheduling, dependency ordering, retries, hypothesis verdicts, aggregation, and certification readiness?
22. How do EvalRoadmap planning artifacts become executable eval inputs without allowing the model-authored DAG to be its own sole oracle?
23. What independent repository acceptance signal proves implementation progress and completion for each tiny fixture?
24. How are discretionary model routes, such as audit/create/split/realign/reimagine/retire, made deterministic enough to test without prompt overfitting?
25. What Git topology is canonical for fixtures when production publication assumes `.agents` is a submodule but repositories may contain an ordinary `.agents` directory?
26. How are pushes confined to isolated remotes, and how are lost push responses or non-fast-forward states reproduced safely?
27. Are unrestricted networked authoring/execution postures acceptable in fixture CI, or must the fixture authority impose an additional containment boundary?
28. How are timing-dependent interruptions injected and proven to occur at the intended transport/transition boundary?
29. What is the expected recovery action for each durable state, especially prompt-completed, output-validated, effects-partial, and effects-applied?
30. How is unknown provider side effect reconciliation surfaced to an operator and fixture oracle?
31. What archive state is authoritative after failure during materialization, synthesis, or completion-context update?
32. How is already-certified closure discovered when live milestone/plan artifacts have been archived?
33. Which artifacts are compared exactly, structurally, semantically, or only by invariants, and who approves changes to that classification?
34. What evidence is retained from successful runs versus failed, flaky, blocked, or provider-incompatible runs?
35. What cost, latency, token, storage, and human-review ceilings define an acceptable fixture?
36. Which test tiers run per change, per platform, on schedule, and for release certification?
37. How are known risks and issue records mapped to fixtures and prevented from silently losing coverage?
38. How does the ecosystem detect new workflow identities, products, effects, persistence domains, prompts, provider capabilities, and execution agents that lack fixture coverage?
39. How will future parallel transitions and concurrent repository mutation change isolation, ordering, and oracle semantics?
40. What is the retirement policy for fixtures tied to legacy state, old schemas, old prompts, or unsupported Codex profiles?
41. Which production-derived registries and vocabularies define the denominator for each architectural coverage dimension?
42. How are workflow/artifact, persistence, Git, interruption, provider, configuration/permission, and oracle overlays represented, ordered, validated for compatibility, and versioned without prescribing one storage mechanism prematurely?
43. When a base repository changes, which scenarios and prior evidence become invalid, and how is that invalidation made visible?
44. Which authority interactions require exhaustive combinations, pairwise combinations, or one representative case so composability controls cost without creating coverage blind spots?
45. Which coverage dimensions and evidence levels are release-blocking, and how are explicit exclusions approved without allowing raw fixture count to mask critical gaps?

## Audit conclusion

Loop Relay has the contracts and deterministic component tests needed to support a high-confidence fixture ecosystem, but current confidence stops at the production assembly boundary. The most valuable future work is to make real Codex, public CLI composition, repository state, SQLite authority, Git effects, cross-process recovery, evaluation evidence, and certification closure observable under tiny, repeatable scenarios.

The roadmap should optimize for behavioral coverage per model turn. It should judge composed repository/scenario cases by the architectural obligations they cover—not by application realism, repository size, fixture-directory count, run count, or quantity of generated artifacts. Confidence will come from reusable base repositories, composable state/topology/fault/oracle overlays, a few complete live chains, many sharply targeted transition and recovery cases, independent deterministic oracles, explicit provider certification, and an idempotent rerun requirement.
