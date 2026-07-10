# Workflow Orchestrator Requirements Completeness Audit

This document audits whether the current Workflow Orchestrator requirements are sufficient for architecture work without forcing the architect to invent behavior.

Authoritative inputs:

- `workflow-runtime-audit.md`
- `workflow-orchestrator-requirements.md`
- `workflow-orchestrator-decision-requirements.md`

Evidence convention:

- **Evidence**: A statement grounded in the authoritative input documents.
- **Inference**: A conclusion drawn from the authoritative input documents.
- **Uncertainty**: A missing, conflicting, or unresolved requirement that would force architectural assumption.

This audit does not design architecture, recommend implementation, define APIs, propose object models, or resolve any of the gaps it identifies.

## 1. Executive Summary

**Overall completeness**

- **Evidence**: The requirements documents provide a detailed inventory of current workflow surfaces, artifacts, state stores, authority signals, decision points, invariants, and known unknowns. `workflow-orchestrator-requirements.md` records Plan, Execution, Roadmap, Completion, Storage, discovery, stage completion, authority, and CLI surfaces. `workflow-orchestrator-decision-requirements.md` records 32 orchestrator decisions and their evidence, inputs, outputs, confidence requirements, ambiguities, and unknowns.
- **Inference**: The requirements are strong as a discovery baseline and as a constraint catalog.
- **Uncertainty**: The requirements are not sufficient to architect a full Workflow Orchestrator without inventing behavior in workflow identity, workflow selection, cross-workflow handoff, storage precedence, recovery, completion ownership, human interaction, concurrency, and extensibility.

**Major strengths**

- **Evidence**: Roadmap requirements are heavily specified through persisted state, transition journals, lifecycle metadata, prompt contracts, projections, blocker evidence, and storage verification.
- **Evidence**: Completion requirements clearly reject raw milestone checkbox completion as final closure; completion certification, policy routing, archive synthesis, and roadmap completion context update are all required.
- **Evidence**: The decision requirements explicitly distinguish evidence, inference, uncertainty, authority, confidence, traceability, and known issue risk.
- **Inference**: The documents give an architect enough evidence to understand current behavior and avoid several unsafe interpretations.

**Largest remaining risks**

- **Evidence**: The requirements repeatedly state that no unified command or cross-workflow status surface exists, that `looprelay` product behavior is undefined, and that Roadmap -> Plan -> Execution chaining is not encoded.
- **Evidence**: Plan and Main Execution lack durable state documents and status commands; partial Plan outputs and partial Execution state remain ambiguous.
- **Evidence**: SQLite is described as canonical by README-level requirements while current code can fall back to filesystem stores when SQLite is missing or not imported/canonical.
- **Evidence**: Completion certification appears in both Main CLI and Roadmap contexts, and no single global owner is specified.
- **Inference**: These risks affect core architecture boundaries rather than only edge behavior.

**Readiness for architecture**

- **Inference**: Readiness is **Partially Ready**. Architecture can rely on the evidence inventory and negative constraints, but cannot fully define orchestrator behavior without resolving missing requirements.
- **Uncertainty**: An unqualified "yes" is not supported. The architect would still have to invent behavior for multiple decisions listed as unknown in `workflow-orchestrator-decision-requirements.md`.

## 2. Requirements Coverage Assessment

| Concern | Assessment | Evidence | Inference | Uncertainty |
| --- | --- | --- | --- | --- |
| Workflow discovery | Partially specified | Discovery domains include CLI args, `.agents`, lifecycle, state, journal, ledger, projections, SQLite, Git, and environment variables. | Current observable facts are well inventoried. | No unified discovery result semantics or global workflow status are specified. |
| Workflow identity | Partially specified | Identity signals include CLI binary, Roadmap command, persisted Roadmap state, prompt contract key, lifecycle, artifact paths, Git state, and decision resume state. | Identity cannot be artifact-only. | No canonical identity registry exists; Traditional Roadmap and Eval-Driven Roadmap are semantic labels only. |
| Workflow selection | Insufficiently specified | No current command answers which workflow should run next. | Requirements constrain selection but do not define precedence when multiple workflows are eligible or blocked. | The architect would invent selection priority and user-facing behavior. |
| Stage discovery | Partially specified | Roadmap stages are enumerated; Plan and Execution stage evidence is artifact-based. | Roadmap stage discovery is much stronger than Plan and Execution. | Plan and Execution lack durable stage state, making partial states ambiguous. |
| Stage selection | Insufficiently specified | Stage eligibility tables list may-execute and must-not-execute conditions. | Some stage decisions are specified inside current CLIs. | No cross-workflow stage selection rule exists when several stages appear relevant. |
| Workflow eligibility | Partially specified | Plan preflight, Roadmap preflight, Storage eligibility, and inferred Execution eligibility are described. | Eligibility is clearest for Roadmap and Plan. | Execution eligibility is inferred; current code does not preflight all required operational inputs. |
| Stage eligibility | Partially specified | `workflow-orchestrator-decision-requirements.md` section 11 lists stage eligibility. | Eligibility constraints exist for many stages. | Missing behavior remains for unsupported Roadmap unblock intents, Plan resume, and Execution missing-input states. |
| Progression | Partially specified | Positive, negative, ambiguous, and completion signals are cataloged. | The requirements identify what cannot count as progress. | No single progression order or next-stage algorithm is specified. |
| Recovery | Insufficiently specified | Roadmap has blockers, intents, and `unblock`; Execution has decision resume and artifact rotation; Plan has cleanup-oriented preflight and rollback in scoped operations. | Recovery coverage is uneven. | Resume, restart, rerun, repair, replay, and rollback semantics are not globally specified. |
| Invalidation | Partially specified | Project Context, projections, active epic, milestone specs, storage markers, and archive movement invalidation are documented. | Roadmap invalidation is strong; Plan invalidation is weak. | What remains valid after invalidation and downstream/upstream propagation are incomplete. |
| Authority | Partially specified | Roadmap, Plan, Execution, Persistence, and decision authority layers are documented. | Authority must be explicit and layered. | Global precedence between SQLite and filesystem, Main and Roadmap completion, HITL scope, and fallback stores is unresolved. |
| Persistence | Partially specified | SQLite tables, filesystem exports, compatibility artifacts, sync markers, and storage statuses are listed. | Persistence facts are observable and authority-sensitive. | Mandatory storage verification, automatic sync, transaction marker scope, and fallback precedence remain unspecified. |
| Observability | Partially specified | Observable facts and events are cataloged. | The requirements identify several unobservable or weakly traceable decisions. | Plan/Main execution cannot be fully reconstructed from a single canonical ledger. |
| Human interaction | Insufficiently specified | HITL, non-implementation decisions, Roadmap terminal states, blockers, and elevation reasons are listed. | Human authority is first-class in several flows. | There is no unified human-decision queue, lifecycle, scope, or blocking rule across workflows. |
| Storage | Partially specified | Storage commands, statuses, sync conflicts, verification findings, and force flags are recorded. | Storage is a workflow and authority domain. | Whether storage verification or sync participates automatically in orchestration decisions is unspecified. |
| Completion | Partially specified | Milestone gate, non-implementation review, completion certification, route mapping, archive synthesis, and roadmap context update are documented. | Raw checked boxes are not closure. | Completion ownership between Main CLI and Roadmap transition, durable completed markers, archive retry, and rerun safety remain unresolved. |
| Concurrency | Insufficiently specified | Current Plan and Main run serially; `status` and `storage-verify` are read-oriented; concurrent mutation is unsupported by evidence. | The requirements identify concurrency as a gap. | No cross-process lock, concurrent status semantics, human edit semantics, or storage sync concurrency contract is specified. |
| Extensibility | Insufficiently specified | The documents name workflows, transitions, prompt contracts, storage domains, and output vocabularies. | Some extensibility needs are implied. | Requirements do not specify how new workflows, stages, transitions, completion types, storage backends, or prompt categories are added without changing requirements. |

## 3. Missing Requirements

1. **Orchestrator product boundary**
   - **Evidence**: `workflow-orchestrator-decision-requirements.md` says the future meaning of `looprelay` is undefined and no single current command chains Roadmap -> Plan -> Execution.
   - **Inference**: The orchestrator's allowed responsibilities are not bounded.
   - **Uncertainty**: An architect would have to invent whether the orchestrator is status-only, selector, sequencer, executor, recovery coordinator, storage coordinator, or some combination.

2. **Canonical workflow identity**
   - **Evidence**: Traditional Roadmap and Eval-Driven Roadmap are not command names, enum values, or persisted workflow identities.
   - **Inference**: Existing labels require semantic mapping.
   - **Uncertainty**: There is no requirement that defines the complete workflow identity set or how identities are created, named, persisted, or deprecated.

3. **Workflow selection precedence**
   - **Evidence**: Requirements state multiple identities can be relevant in one workspace, such as Roadmap paused at `MilestoneSpecsReady` plus Plan eligibility.
   - **Inference**: Multiple valid next actions can coexist.
   - **Uncertainty**: No requirement states which workflow wins, whether the orchestrator presents alternatives, or when it must decline to choose.

4. **Stage selection precedence**
   - **Evidence**: Roadmap has persisted states; Plan and Execution infer stages from artifacts and loop state.
   - **Inference**: Stage selection has heterogeneous authority.
   - **Uncertainty**: No cross-workflow rule defines how to choose a current stage when persisted state, artifacts, archive state, and Git state disagree.

5. **Roadmap -> Plan adapter**
   - **Evidence**: Roadmap produces `.agents/epic.md` and specs; Plan requires `.agents/specs/epic.md`. The runtime audit says the exact adapter is not established.
   - **Inference**: The Roadmap-to-Plan boundary is semantic, not merely file presence.
   - **Uncertainty**: Requirements do not specify what artifact is authoritative, whether transformation is required, or how compatibility is validated.

6. **Plan -> Execution handoff authority**
   - **Evidence**: Plan writes plan, details, operational context, and milestones; Execution consumes them. No single handoff command is encoded.
   - **Inference**: Completion of Plan and start eligibility of Execution are separate decisions.
   - **Uncertainty**: Requirements do not specify a durable Plan completion marker, ownership transfer, or validation proof that execution artifacts belong together.

7. **Automatic chaining policy**
   - **Evidence**: Requirements state Roadmap -> Plan -> Execution automation is not observed and whether it should happen by default is unknown.
   - **Inference**: Chain behavior materially affects orchestration scope and failure handling.
   - **Uncertainty**: Requirements do not specify whether chaining is manual, automatic, opt-in, or prohibited.

8. **Global storage precedence**
   - **Evidence**: SQLite can be imported/canonical or invalid; README-level requirements describe SQLite as canonical while code can fall back to filesystem stores.
   - **Inference**: Store selection affects all downstream facts.
   - **Uncertainty**: No global precedence rule defines SQLite versus filesystem authority across all workflows and legacy exports.

9. **Mandatory storage verification**
   - **Evidence**: Storage verification can detect corrupt, stale, conflicting, orphaned, duplicate, or unresolved runtime state.
   - **Inference**: Verification can materially affect decision confidence.
   - **Uncertainty**: Requirements do not specify whether verification is mandatory before workflow discovery, workflow selection, recovery, or completion.

10. **Plan partial-output recovery**
    - **Evidence**: Plan has no durable state, status, resume, repair, or force command; existing outputs block a fresh Plan run.
    - **Inference**: Partial Plan outputs are ambiguous.
    - **Uncertainty**: Requirements do not specify whether partial Plan outputs are resumable, reusable, invalid, user-cleanup-only, or recoverable by a specific rule.

11. **Execution preflight**
    - **Evidence**: Execution consumes plan/details/operational context/milestones but current code does not expose a complete preflight equivalent to Plan.
    - **Inference**: Execution eligibility is partly inferred.
    - **Uncertainty**: Requirements do not specify required pre-execution checks for missing plan, missing details, missing milestones, invalid operational context, stale decisions, or stale handoffs.

12. **Execution partial-state recovery**
    - **Evidence**: Execution resumes implicitly through handoffs, decisions, operational context, history, SQLite decision resume, and Git state.
    - **Inference**: There is no explicit persisted execution state.
    - **Uncertainty**: Requirements do not specify recovery after interrupted handoff rotation, failed post-execution review, failed publish, failed commit, failed push, or stale live decisions.

13. **Completion ownership**
    - **Evidence**: Completion certification appears in Main CLI and Roadmap completion transition.
    - **Inference**: Both paths can produce closure-related evidence.
    - **Uncertainty**: Requirements do not specify which path is authoritative when evidence disagrees or when one path partially succeeds.

14. **Durable closed-state marker**
    - **Evidence**: Archive movement can remove live plan/details/context/milestones; issue records document rerun ambiguity after archive movement.
    - **Inference**: Live milestone evidence may disappear after closure.
    - **Uncertainty**: Requirements do not specify the durable fact that proves an epic is already closed after archive cleanup.

15. **Archive retry semantics**
    - **Evidence**: Requirements cite non-transactional archive movement, archive retry risk, and archive index collision.
    - **Inference**: Completion recovery can be unsafe.
    - **Uncertainty**: Requirements do not specify what state is valid after archive movement succeeds but synthesis or context update fails.

16. **HITL scope and lifecycle**
    - **Evidence**: HITL appears in planning, roadmap, completion, blockers, non-implementation review, permission/elevation, and strategic terminal states.
    - **Inference**: Human decisions can block multiple workflow domains.
    - **Uncertainty**: Requirements do not specify a global human-decision lifecycle, queue, precedence, or downstream blocking scope.

17. **Permission and elevation authority**
    - **Evidence**: Roadmap has elevation flags; Plan/Main have permission policy; issue records document trust gaps.
    - **Inference**: Permission behavior can allow or block execution.
    - **Uncertainty**: Requirements do not specify cross-workflow permission precedence, hard-deny invariants, or how permission trust gaps affect orchestrator confidence.

18. **Concurrency model**
    - **Evidence**: Concurrent workflow mutation against `.agents`, SQLite, or Git is not shown as supported; no cross-process lock is observed.
    - **Inference**: Concurrent operations can corrupt or confuse authority signals.
    - **Uncertainty**: Requirements do not specify behavior for simultaneous status, storage verification, storage sync, Plan, Roadmap, Execution, or human edits.

19. **Invalidation propagation**
    - **Evidence**: Roadmap projections and active-epic-derived specs have freshness/provenance; Plan outputs do not have an equivalent durable manifest.
    - **Inference**: Invalidation coverage is uneven.
    - **Uncertainty**: Requirements do not fully specify what remains valid, what becomes invalid, and which upstream/downstream workflows are affected after input drift.

20. **Recovery vocabulary**
    - **Evidence**: Roadmap has `unblock`; Execution has decision resume; Plan has no durable recovery command.
    - **Inference**: Recovery concepts exist but are not uniform.
    - **Uncertainty**: Requirements do not define the distinction and allowed behavior for resume, restart, rerun, repair, replay, rollback, unblock, cancel, and fail across all workflows.

21. **Legacy compatibility semantics**
    - **Evidence**: Requirements mention legacy `.agents/state.md`, legacy decision ledger markdown, compatibility exports, and legacy Roadmap execution-preparation states.
    - **Inference**: Legacy files can be observable but may not be authoritative.
    - **Uncertainty**: Requirements do not fully specify when legacy artifacts are authoritative, ignored, migrated, or treated as stale exports.

22. **Extensibility contract**
    - **Evidence**: Current requirements name existing workflows and contracts but do not define a shared registry for Plan/Main prompt contracts or workflow identities.
    - **Inference**: Adding new behavior would require architecture decisions.
    - **Uncertainty**: Requirements do not specify what requirements must exist for a new workflow, stage, transition, completion route, storage backend, or prompt category to be orchestratable.

## 4. Hidden Assumptions

| Assumption | Evidence | Inference | Architectural impact |
| --- | --- | --- | --- |
| Single repository workspace | All CLI invocations take one `<REPO_DIR>` and artifacts live under that repository. | Orchestration is scoped to one repo root. | Multi-repo or cross-repo orchestration behavior is undefined. |
| `.agents` is durable and central | Requirements call `.agents` the behavior-bearing artifact root. | Runtime meaning depends on durable workspace files. | Behavior under missing, corrupted, readonly, or concurrently edited `.agents` is underdefined. |
| Serial mutation | Plan and Main run serially inside their processes; concurrent mutation is unsupported by evidence. | Requirements assume one mutating workflow at a time. | Cross-process locking and concurrent user edits are not specified. |
| Single active Plan pipeline | Plan is a clean-start fixed pipeline and blocks on existing outputs. | Only one Plan output set is expected in live workspace. | Parallel or versioned planning is undefined. |
| Single active epic lifecycle | Invariants reject duplicate Ready/Executing active epics. | Roadmap and completion assume one active epic. | Multi-epic execution or overlapping epics are undefined. |
| Durable Git availability | Execution progress and commits depend on Git status excluding `.agents`. | Git is an authority source. | Behavior when Git fails, is unavailable, or has unusual state is incomplete. |
| Optional but authority-sensitive SQLite | Requirements allow SQLite-backed or filesystem-backed facts depending on validation. | Persistence authority is not universally mandatory. | Store selection affects nearly every decision and lacks global precedence. |
| Prompt output parseability | Requirements rely on parsers, output gates, and policy validators after prompts. | Prompt text alone is not authority. | Malformed, partial, or unexpected output behavior is covered unevenly across workflows. |
| Human interaction is synchronous enough for CLI workflows | HITL decisions and elevation reasons are represented as files, flags, or manual action. | Human authority is assumed to be externally resolvable. | No global model for asynchronous, queued, or multi-user decisions exists. |
| Archive side effects are recoverable enough to inspect | Requirements require completed archive state checks and cite known archive issues. | Archive artifacts are expected to aid recovery. | Partial archive behavior is not specified enough for safe architecture. |
| Telemetry is supporting, not decisive | Telemetry absence is explicitly unknown. | Current requirements do not make telemetry a hard dependency. | Confidence and observability behavior without telemetry is unspecified. |

## 5. Requirement Ambiguities

1. **Traditional Roadmap versus Eval-Driven Roadmap**
   - **Evidence**: Both labels are semantic, not encoded workflow identities.
   - **Possible interpretations**: They are separate workflows; they are Roadmap variants; Eval-Driven Roadmap is a completion phase; Eval-Driven Roadmap is prompt behavior only.
   - **Architectural consequence**: Workflow identity, selection, state, and user-facing status differ materially.

2. **Meaning of `looprelay`**
   - **Evidence**: No current CLI command named `looprelay` was observed; future product behavior is unknown.
   - **Possible interpretations**: Status command, unified runner, workflow selector, recovery coordinator, storage-aware supervisor, or CLI facade.
   - **Architectural consequence**: Scope, persistence, command authority, and failure handling cannot be derived.

3. **SQLite canonicality versus fallback**
   - **Evidence**: README-level requirements say SQLite structured runtime state is canonical; code can fall back to filesystem stores unless SQLite is valid imported/canonical.
   - **Possible interpretations**: SQLite is mandatory, SQLite is preferred, SQLite is per-domain optional, or filesystem remains coequal.
   - **Architectural consequence**: Every discovery and authority decision depends on this precedence.

4. **Roadmap -> Plan handoff**
   - **Evidence**: Roadmap outputs `.agents/epic.md` and specs; Plan requires `.agents/specs/epic.md`.
   - **Possible interpretations**: Roadmap already produces Plan's epic input; a copy is required; a transform is required; the user must author the adapter.
   - **Architectural consequence**: Workflow chaining and Plan eligibility cannot be determined safely.

5. **Plan partial outputs**
   - **Evidence**: Existing Plan outputs block preflight but can also look like progress.
   - **Possible interpretations**: Partial outputs are invalid leftovers, completed Plan evidence, resumable state, or user-confirmation-required state.
   - **Architectural consequence**: The orchestrator cannot choose fresh Plan, resume, cleanup, or Execution eligibility without inventing behavior.

6. **Execution preflight**
   - **Evidence**: Execution consumes several artifacts but does not expose a full preflight/status command.
   - **Possible interpretations**: Missing inputs are runtime failures, weak prompts, implicit recovery conditions, or preflight blockers.
   - **Architectural consequence**: Execution eligibility and error classification are underdefined.

7. **Completion owner**
   - **Evidence**: Main CLI and Roadmap transition both perform or reference completion certification behavior.
   - **Possible interpretations**: Main owns closure; Roadmap owns closure; shared services own closure; first successful path wins.
   - **Architectural consequence**: Conflicting completion evidence and rerun behavior cannot be resolved.

8. **HITL blocking scope**
   - **Evidence**: HITL requests appear in roadmap, plan, decisions, non-implementation review, blockers, and permissions.
   - **Possible interpretations**: HITL blocks only originating workflow, all downstream workflows, all orchestrator activity, or only mutation.
   - **Architectural consequence**: Workflow selection and safe progression differ.

9. **Storage sync automation**
   - **Evidence**: Storage sync is a command and force flags are scoped; whether storage sync should run automatically is unknown.
   - **Possible interpretations**: Never automatic, automatic before discovery, automatic before mutation, or automatic only with user approval.
   - **Architectural consequence**: Discovery mutability and authority repair behavior are undefined.

10. **Legacy Roadmap execution-preparation states**
    - **Evidence**: Roadmap state contains execution-preparation states not advanced by current Roadmap CLI.
    - **Possible interpretations**: Historical compatibility only, future active states, report-only states, or migration states.
    - **Architectural consequence**: Resume and status behavior for persisted legacy states is unclear.

## 6. Requirement Contradictions

1. **SQLite canonicality versus filesystem fallback**
   - **Evidence**: Requirements say SQLite structured runtime state is canonical, but also state Roadmap/Main choose SQLite only when imported/canonical and otherwise fall back.
   - **Inference**: This creates conflicting authority expectations.
   - **Uncertainty**: A global rule resolving canonicality, fallback validity, and stale export handling is missing.

2. **Plan artifacts as progress versus blocker**
   - **Evidence**: Plan required outputs are positive progress signals, but Plan preflight blocks when those outputs already exist.
   - **Inference**: Existing Plan artifacts can mean successful Plan, partial failed Plan, or fresh-run blocker.
   - **Uncertainty**: Requirements do not define when existing Plan artifacts prove completion versus block further planning.

3. **Milestone completion versus archive cleanup**
   - **Evidence**: Milestone gate requires live strict checkboxes, but completed archive movement can remove live plan/details/context/milestones.
   - **Inference**: The evidence needed to detect completion can be moved by completion itself.
   - **Uncertainty**: Durable already-closed detection is not specified.

4. **Completion certification ownership split**
   - **Evidence**: Completion behavior appears in Main CLI completion service and Roadmap completion transition.
   - **Inference**: Two surfaces can claim or process completion.
   - **Uncertainty**: Requirements do not specify precedence when Main and Roadmap evidence differ.

5. **Roadmap `unblock` surface versus unsupported intents**
   - **Evidence**: Roadmap exposes `unblock`, but some blocker intents are explicitly unsupported.
   - **Inference**: The existence of `unblock` does not imply every blocked condition is recoverable.
   - **Uncertainty**: Requirements do not distinguish intentional non-recoverability from incomplete coverage.

6. **Discovery non-mutation versus storage repair pressure**
   - **Evidence**: Discovery must be non-mutating unless a mutating command is explicitly invoked; storage sync can resolve or change authority surfaces.
   - **Inference**: Some decisions may require storage authority that discovery cannot repair.
   - **Uncertainty**: Requirements do not define whether the orchestrator blocks, reports, or requests explicit storage action.

7. **Roadmap terminal pause versus intended chain**
   - **Evidence**: Roadmap pauses at `MilestoneSpecsReady`; the intended chains converge Roadmap -> Plan -> Execution.
   - **Inference**: Roadmap completion for its current CLI is not the same as whole workflow-chain completion.
   - **Uncertainty**: Requirements do not define who owns advancing from the pause to Plan.

## 7. Requirement Coupling

- **Evidence**: Storage authority affects whether SQLite or filesystem facts can be trusted.
- **Inference**: Workflow discovery, state discovery, lifecycle interpretation, history reads, evidence reads, and recovery classification are coupled to storage authority.
- **Uncertainty**: These concerns cannot change independently until storage precedence is specified.

- **Evidence**: Project Context and projection freshness govern Roadmap prompt readiness and decision-session resume freshness.
- **Inference**: Prompt execution, selection reuse, and invalidation are coupled to projection provenance.
- **Uncertainty**: Plan and Execution do not have equivalent manifest coverage, so projection coupling is uneven.

- **Evidence**: Active epic validity drives milestone spec readiness; milestone specs drive Plan; Plan outputs drive Execution; Execution evidence drives Completion; Completion drives Roadmap context update.
- **Inference**: Cross-workflow contracts are a dependency chain, not independent artifacts.
- **Uncertainty**: The requirements do not specify all boundary ownership and invalidation propagation across that chain.

- **Evidence**: Completion archive movement affects live Plan/Execution artifacts and rerun safety.
- **Inference**: Completion, archive, recovery, and already-closed detection are coupled.
- **Uncertainty**: Archive retry and durable closed-state requirements are missing.

- **Evidence**: HITL decisions appear in non-implementation completion, roadmap strategic states, blockers, and permissions.
- **Inference**: Human authority is coupled to progression, recovery, and completion.
- **Uncertainty**: Scope and lifecycle of human decisions are not globally specified.

- **Evidence**: Plan preflight requires absence of outputs, while Execution requires those outputs.
- **Inference**: Plan completion and Execution eligibility are tightly coupled through artifact ownership and freshness.
- **Uncertainty**: No durable Plan completion marker decouples "outputs exist" from "Plan completed."

## 8. Negative Requirements

1. **Never infer workflow or stage completion from artifact existence alone.**
   - **Evidence**: Requirements state artifact existence can be stale, archived, superseded, blocked, partial, or compatibility export.
   - **Inference**: Artifact existence is supporting evidence, not completion authority.
   - **Uncertainty**: None for this negative requirement.

2. **Never treat prompt completion as artifact validity.**
   - **Evidence**: Active epic promotion requires parser, validation, lifecycle, and promotion outcomes.
   - **Inference**: Prompt output can exist before safe downstream use.
   - **Uncertainty**: None for Roadmap; Plan/Main equivalent validation contracts are less uniform.

3. **Never treat milestone checkbox completion as epic closure.**
   - **Evidence**: Completion certification policy and route are required; milestone gate only triggers completion claim.
   - **Inference**: Closure requires certification and roadmap context update.
   - **Uncertainty**: Completion owner remains unresolved.

4. **Never count `.agents` submodule changes as real implementation progress.**
   - **Evidence**: Git progress excludes `.agents`; progress requires non-`.agents` Git changes or reduced unticked milestones.
   - **Inference**: Implementation progress and artifact publication are distinct.
   - **Uncertainty**: None for current Execution behavior.

5. **Never reuse stale Roadmap projections under blocking stale policy.**
   - **Evidence**: Stale projection under policy `Block` is a blocker.
   - **Inference**: Prompt readiness depends on manifest-backed freshness.
   - **Uncertainty**: Projection repair versus regeneration remains unspecified.

6. **Never silently choose between conflicting SQLite and filesystem state.**
   - **Evidence**: Storage conflict, stale export, divergent changes, and unsupported schema are explicit findings.
   - **Inference**: Conflicts must remain visible.
   - **Uncertainty**: Global precedence is missing.

7. **Never invent commands, flags, or overrides.**
   - **Evidence**: Architectural constraints explicitly say not to invent workflow commands, flags, or overrides not present in repository evidence.
   - **Inference**: Existing CLI boundaries constrain requirements.
   - **Uncertainty**: Future `looprelay` behavior is undefined.

8. **Never mutate during discovery unless an existing mutating command is explicitly invoked.**
   - **Evidence**: Architectural constraints require non-mutating discovery.
   - **Inference**: Status and discovery cannot perform hidden repair.
   - **Uncertainty**: Storage sync automation is unspecified.

9. **Never ignore blocker or HITL evidence.**
   - **Evidence**: HITL requests are workflow evidence; blockers carry evidence paths and required next steps.
   - **Inference**: Human authority and blockers affect eligibility.
   - **Uncertainty**: Cross-workflow blocker scope is missing.

10. **Never treat `storage-verify` as repair.**
    - **Evidence**: Requirements state `storage-verify` is read-only and must not be treated as repair.
    - **Inference**: Verification findings qualify decisions but do not change state.
    - **Uncertainty**: Whether verification is mandatory is unknown.

11. **Never treat Plan partial outputs as safe resume evidence without more proof.**
    - **Evidence**: Plan lacks durable state/journal and partial outputs are ambiguous.
    - **Inference**: Existing outputs do not prove resumability.
    - **Uncertainty**: The missing proof requirement is not defined.

12. **Never collapse cancellation, failure, paused, blocked, stalled, completed, and ambiguous into one status.**
    - **Evidence**: Decision constraints require these states remain distinct.
    - **Inference**: Status vocabulary must preserve semantic differences.
    - **Uncertainty**: Shared output enum is missing.

## 9. Edge Case Coverage

| Edge case | Coverage | Evidence | Inference | Gap |
| --- | --- | --- | --- | --- |
| Partial workflows | Partial | Roadmap has transition intent; Plan/Main lack durable state. | Roadmap partial state is better covered than Plan/Main. | Plan/Main partial workflow semantics are missing. |
| Partial artifacts | Partial | Existing Plan outputs and archive artifacts can be ambiguous. | Artifact presence is not enough. | Reuse, invalidation, and cleanup semantics are missing. |
| Partial persistence | Partial | Workflow transaction markers can classify phases where present. | SQLite-backed phases are observable. | Marker scope outside coordinated SQLite phases is unclear. |
| Partial recovery | Insufficient | Roadmap has narrow `unblock`; Plan has no resume; Execution is artifact-driven. | Recovery is uneven. | Global recovery vocabulary and allowed actions are missing. |
| Stale projections | Sufficient for Roadmap | Stale Roadmap projections under `Block` block. | Roadmap freshness is well specified. | Repair/regeneration behavior and non-Roadmap parity are missing. |
| Storage conflicts | Partial | Storage conflict, stale export, unsupported schema, verification failure are categories. | Conflicts are observable. | Global precedence and orchestrator response are missing. |
| Git conflicts/failures | Partial | Git status and commit/push affect progress. | Git is an authority source for Execution. | Git failure, merge conflict, detached head, and dirty `.agents` conflict semantics are not fully specified. |
| Cancelled workflows | Partial | CLIs distinguish cancellation; Roadmap persists cancellation. | Cancellation is not generic failure. | Plan cancellation durability and cross-workflow resume after cancel are missing. |
| Failed workflows | Partial | Outcomes include failed; Roadmap journal/blockers preserve evidence. | Failure handling varies by workflow. | Plan/Main failure recovery semantics are incomplete. |
| Multiple failures | Insufficient | Known issue records qualify recovery confidence. | Repeated failure can change safety. | No requirements define escalation or persistent failure counters. |
| Missing inputs | Partial | Project Context, Plan input, Roadmap inputs, milestones have checks. | Missing inputs can block. | Execution missing-input preflight is incomplete. |
| Unexpected outputs | Partial | Parsers, gates, and validators handle some outputs. | Prompt output schema is enforced unevenly. | Plan/Main unexpected output classification is incomplete. |
| Orphaned artifacts | Partial | Storage verification can detect orphaned/unresolved runtime state. | Orphans matter for authority. | Cross-workflow orphan artifact behavior is not specified. |
| Legacy compatibility | Partial | Legacy files and states are listed. | Legacy facts are not automatically canonical. | Migration, ignore, and authority rules remain incomplete. |
| Unknown workflow state | Partial | Requirements say preserve ambiguity. | Unknown state is a valid outcome. | User-facing and architecture-level handling of unknown state is unspecified. |

## 10. Decision Sufficiency

| ID | Decision | Sufficiency | Evidence | Reason |
| --- | --- | --- | --- | --- |
| D01 | Current executable surface | Partially | Existing CLIs are known; future `looprelay` is undefined. | Current surfaces are known, future orchestrator surface is not. |
| D02 | Storage authority | Partially | SQLite validation statuses and fallback are documented. | Global precedence is missing. |
| D03 | Roadmap workflow state | Yes for Roadmap, partial globally | Roadmap state and classifier exist. | No global workflow state exists. |
| D04 | Roadmap initialize/resume/report/block | Partially | Roadmap planners exist. | Some states have no safe resume rule. |
| D05 | Project Context validity | Yes for Roadmap | Nine-file contract is explicit. | Global enforcement outside Roadmap is unspecified. |
| D06 | Roadmap prompt/projection readiness | Partially | Prompt contracts and projection freshness are explicit. | Projection repair/regeneration is unspecified. |
| D07 | Reuse or regenerate selection | Partially | Selection freshness and lifecycle are documented. | User-facing handling of stale present selection is not global. |
| D08 | Next roadmap initiative outcome | Partially | Allowed decisions are listed. | Traditional versus eval-driven classification is not explicit. |
| D09 | Existing epic preparation route | Partially | Audit dispositions are listed. | Insufficient Evidence persistence is inconsistent. |
| D10 | Promote active epic output | Partially | Promotion/validation is required. | Automatic repair for blockers is unsupported and unresolved. |
| D11 | Split output validity and child selection | Partially | Split extraction and lineage are listed. | Split blocker recovery is unsupported. |
| D12 | Milestone specs readiness | Partially | Freshness/provenance and terminal pause are documented. | Downstream chaining remains external. |
| D13 | Roadmap evaluates completion claim | Partially | Roadmap maps completion state to evaluation. | Main CLI also certifies completion. |
| D14 | Completion route | Partially | Policy/router outcomes are documented. | Authority on disagreement is missing. |
| D15 | Non-implementation review block | Partially | Human decisions are required for unresolved entries. | HITL scope across workflows is missing. |
| D16 | Plan eligibility | Yes for fresh run | Preflight rules are explicit. | Resume eligibility is absent. |
| D17 | Plan progression/completion | Partially | Artifact gates are listed. | No whole-pipeline durable completion marker. |
| D18 | Execution eligibility | No | Execution inputs are consumed inline. | Complete preflight requirements are not specified. |
| D19 | Execution slice route | Partially | Live decisions and handoff routing are described. | Interrupted rotation formal state is missing. |
| D20 | Decision-session continue/transfer | Yes within decision session | Router behavior is documented. | Not a workflow-wide resume model. |
| D21 | Decision-session resume | Partially | SQLite, env flag, projection freshness, and resume success govern it. | Experimental thread resume confidence is not product-specified. |
| D22 | Handoff prompt type | Partially | Real changes versus no changes are described. | Handoff content quality is not machine-validated beyond existence. |
| D23 | Progress, commit/push, stall | Partially | Git/milestone progress and process-local stall are defined. | Stall counter resets on process restart. |
| D24 | Milestone completion claim | Yes before archive | Strict checkbox rule is explicit. | Archive cleanup creates later ambiguity. |
| D25 | Main CLI reports `EpicCompleted` | Partially | Completion certification is required. | Partial archive/update failure can strand retry state. |
| D26 | Cancellation/failure salvage | Partially | Outcomes are distinct; Execution salvage is best-effort. | Plan durable cancellation is absent. |
| D27 | Storage command outcome | Partially | Storage categories are listed. | Automatic storage sync is unspecified. |
| D28 | Permission/elevation requirement | Partially | Permission settings and Roadmap elevation exist. | Trust gaps and hard-deny behavior are unresolved. |
| D29 | User/HITL required | Partially | Human decision surfaces are listed. | No unified queue or scope. |
| D30 | Output invalidation | Partially | Roadmap and storage invalidation are documented. | Plan invalidation lacks durable manifest. |
| D31 | Concurrency safety | No | Serial behavior and unsafe concurrent mutation are documented. | No general concurrency contract exists. |
| D32 | Complete/obsolete/superseded/already closed | Partially | Lifecycle, archives, and issue records are relevant. | Durable already-closed gate is unresolved. |

## 11. Authority Sufficiency

**Precedence rules**

- **Evidence**: Roadmap authority is layered by storage validity, persisted state, journal/blocker evidence, prompt contracts, projection freshness, lifecycle/artifact validation, and advisory roadmap sources.
- **Evidence**: Execution authority is layered by real Git changes, milestone progress, handoff/decision/context artifacts, non-implementation review, completion certification, archive, and roadmap context update.
- **Evidence**: Persistence authority depends on SQLite validation, sync markers, hashes, and export freshness.
- **Inference**: Local authority relationships are well documented.
- **Uncertainty**: Cross-workflow precedence is not fully defined, especially SQLite versus filesystem, Main versus Roadmap completion, HITL scope, Plan artifacts versus Plan preflight, and legacy exports versus canonical stores.

**Contradictory authority**

- **Evidence**: Requirements identify possible disagreement between SQLite and filesystem exports, Main and Roadmap completion evidence, live artifacts and archive state, and lifecycle entries versus artifacts.
- **Inference**: Contradictory authority can exist.
- **Uncertainty**: Requirements do not define a global conflict resolution rule or a universal "must defer" rule beyond some storage and projection cases.

**Architect invention pressure**

- **Inference**: An architect would have to invent precedence for several high-impact conflicts unless requirements are expanded.
- **Uncertainty**: This is a blocker for an unambiguous architecture of workflow selection, status, and recovery.

## 12. Invalidation Sufficiency

**What becomes invalid**

- **Evidence**: Requirements identify invalidation from Project Context drift, projection prompt/source/hash drift, active epic drift, milestone spec drift, decision ledger drift, storage marker drift, lifecycle states, archive movement, and Git changes.
- **Inference**: Roadmap-derived artifact invalidation is substantially specified.
- **Uncertainty**: Plan outputs are only semantically invalidated by upstream epic/spec changes; no durable Plan invalidation manifest exists.

**What remains valid**

- **Evidence**: Requirements identify some lifecycle states as usable and others as not usable.
- **Inference**: Some artifacts can remain diagnostic evidence even when not usable for progression.
- **Uncertainty**: Requirements do not fully specify which artifacts remain valid as evidence, recovery input, downstream input, or historical record after each invalidation type.

**Downstream effects**

- **Evidence**: Active epic drift invalidates milestone specs; milestone spec drift invalidates operational context, execution prompt, and compatibility artifacts.
- **Inference**: Invalidation propagates downstream.
- **Uncertainty**: Propagation through Plan outputs, Execution decisions/handoffs, completion evidence, and archive state is incomplete.

**Upstream effects**

- **Evidence**: Completion certification updates roadmap completion context, which influences future selection.
- **Inference**: Downstream completion can affect upstream Roadmap selection.
- **Uncertainty**: Requirements do not specify how failed or blocked completion evaluation affects upstream artifacts and selection eligibility.

## 13. Recovery Sufficiency

| Recovery concern | Sufficiency | Evidence | Gap |
| --- | --- | --- | --- |
| Resume | Partial | Roadmap resume and decision-session resume exist. | Plan resume and general Execution resume are not explicitly specified. |
| Restart | Insufficient | Plan can fresh-start only with clean outputs. | Cleanup/restart semantics for partial outputs are missing. |
| Rerun | Insufficient | Rerun risks after archive movement are documented. | Safe rerun conditions are not specified. |
| Repair | Partial | Roadmap `unblock` covers some categories; storage verify detects problems. | Unsupported intents, projection repair, Plan repair, and archive repair are underdefined. |
| Replay | Insufficient | Roadmap journal can trace transitions. | Plan/Main lack enough journal parity for full replay. |
| Rollback | Partial | Scoped artifact operations can rollback; archive movement is non-transactional by issue evidence. | Cross-workflow rollback semantics are missing. |
| Blockers | Partial | Roadmap blockers and completion blockers carry evidence. | Global blocker scope and unsupported blockers are unresolved. |
| Human intervention | Partial | HITL decisions, elevation reasons, strategic states, and blockers exist. | No global lifecycle, queue, or precedence exists. |

**Inference**: Recovery is not complete enough for architecture without assumptions.

**Uncertainty**: The largest recovery gaps are Plan partial output handling, Execution partial state handling, archive retry, already-closed detection, unsupported Roadmap unblock intents, and cross-workflow human decision scope.

## 14. Observability Sufficiency

**Decisions that cannot be fully made from current facts**

- **Evidence**: Existing Plan outputs can be completed planning, partial planning, or preflight-blocking leftovers.
- **Inference**: Plan completion cannot be confidently reconstructed without additional evidence.
- **Uncertainty**: No whole-pipeline marker exists.

- **Evidence**: Main Execution has no named persisted execution state; route is inferred from handoffs, decisions, milestones, Git state, review evidence, and certification evidence.
- **Inference**: Execution status is observable only by correlation.
- **Uncertainty**: Interrupted or stale combinations can remain ambiguous.

- **Evidence**: Completion archive movement can remove live milestone evidence.
- **Inference**: A closed epic may not be discoverable through live milestone gate after archive.
- **Uncertainty**: Durable closed-state marker is missing.

- **Evidence**: Storage conflicts and stale exports can exist.
- **Inference**: Facts can conflict across stores.
- **Uncertainty**: Global precedence is missing.

**Required facts that may be missing**

- **Evidence**: Projection manifests, lifecycle files, SQLite, `.agents`, Git state, and history may be missing, stale, corrupt, or legacy.
- **Inference**: Requirements support reporting ambiguity.
- **Uncertainty**: User-facing and architecture-level behavior when facts are absent is not fully specified.

**Traceability gaps**

- **Evidence**: Roadmap decisions are traceable through state, journal, lifecycle, ledger, snapshots, outputs, and blockers. Plan and Main are weakly traceable through artifacts, commits, console flow, telemetry, and histories.
- **Inference**: Traceability is uneven.
- **Uncertainty**: The required traceability level for Plan/Main to match Roadmap is unknown.

## 15. Extensibility Sufficiency

| Extension | Sufficiency | Evidence | Gap |
| --- | --- | --- | --- |
| New workflow | Insufficient | Current identities are observed, not registered globally. | No requirements define workflow identity registration, authority, lifecycle, or outputs. |
| New stage | Insufficient | Roadmap has states; Plan/Main have implicit stages. | No cross-workflow stage contract exists. |
| New transition | Partial | Runtime audit identifies transition lifecycle. | Plan/Main do not have uniform transition metadata requirements. |
| New completion type | Insufficient | Completion routes are enumerated. | Extension behavior and ownership are not specified. |
| New storage backend | Insufficient | SQLite/filesystem authority and storage commands are documented. | Backend precedence, sync, migration, and verification requirements are not generalized. |
| New prompt category | Partial | Roadmap prompt contracts are explicit; other workflows are implicit. | No shared prompt contract requirements across all workflows. |

**Inference**: Extensibility is not sufficiently specified for an architect to design for growth without inventing behavior.

**Uncertainty**: The requirements do not define which current concepts are closed sets and which are expected to grow.

## 16. Architectural Decision Pressure

| Missing requirement | Affected decisions | Architectural consequence |
| --- | --- | --- |
| Orchestrator product boundary | D01, D16-D18, D27-D31 | Architect must invent whether the orchestrator selects, runs, reports, chains, repairs, or coordinates storage. |
| Canonical workflow identity | D01, D03, D08, D13, D32 | Architect must invent identity names, state ownership, and status labels. |
| Workflow selection precedence | D01, D03, D16, D18, D27, D29, D32 | Architect must invent next-action ordering when multiple workflows are eligible or blocked. |
| Cross-workflow handoff contracts | D12, D16-D18, D30 | Architect must invent the Roadmap -> Plan adapter and Plan -> Execution proof of readiness. |
| Global storage precedence | D02, D27, D30-D32 | Architect must invent which facts to trust when SQLite and filesystem disagree. |
| Plan partial recovery | D16-D17, D30, D32 | Architect must invent whether partial Plan artifacts can be resumed, reused, ignored, or block. |
| Execution preflight and recovery | D18-D24, D26, D30-D32 | Architect must invent safe start and interrupted-run semantics. |
| Completion ownership | D13-D15, D24-D25, D32 | Architect must invent authority between Main completion and Roadmap completion transition. |
| Durable closed-state marker | D24-D25, D30, D32 | Architect must invent how to recognize an already closed epic after archive cleanup. |
| Archive retry semantics | D25-D26, D30, D32 | Architect must invent safe behavior after partial completion side effects. |
| HITL scope | D15, D28-D29, D31 | Architect must invent whether human blockers stop one workflow, downstream workflows, or all mutation. |
| Concurrency model | D02, D27, D31 | Architect must invent lock, read-only, mutation, and human-edit rules. |
| Extensibility contract | D01, D06, D17-D18, D27, D30 | Architect must invent how new workflows/stages/prompts/storage domains become orchestratable. |

## 17. Foundational Requirements

**Foundational requirements already present**

- **Evidence**: Evidence, inference, and uncertainty must be reported distinctly.
- **Evidence**: Workflow discovery must be non-mutating unless an existing mutating command is invoked.
- **Evidence**: Artifact existence, prompt completion, and workflow completion must be distinct facts.
- **Evidence**: Completion certification is separate from milestone checkbox completion.
- **Evidence**: Storage authority must be resolved before trusting SQLite-backed or filesystem-backed facts.
- **Evidence**: User overrides must be explicit, scoped, and traceable.

**Derived requirements**

- **Inference**: Stage discovery derives from workflow identity, storage authority, artifact lifecycle, projection freshness, and observable event history.
- **Inference**: Workflow eligibility derives from required inputs, blockers, lifecycle status, storage authority, permissions, and workflow-specific preflight rules.
- **Inference**: Recovery derives from durable evidence, last safe point, blocker intent, archive state, and human decisions.
- **Inference**: Completion derives from milestone claim, review, certification policy, route, archive, and roadmap context update.

**Missing foundational requirements**

- **Uncertainty**: The orchestrator product boundary is not defined.
- **Uncertainty**: The canonical workflow identity model is not defined.
- **Uncertainty**: Cross-workflow authority precedence is not defined.
- **Uncertainty**: Workflow selection and stage selection precedence are not defined.
- **Uncertainty**: Cross-workflow recovery vocabulary is not defined.
- **Uncertainty**: Completion ownership and durable closed-state authority are not defined.
- **Uncertainty**: Concurrency and human-edit behavior are not defined.
- **Uncertainty**: Extensibility requirements are not defined.

## 18. Requirement Quality Assessment

| Quality | Assessment | Evidence | Inference | Uncertainty |
| --- | --- | --- | --- | --- |
| Clarity | Partial | Current behavior and known unknowns are clearly labeled. | Facts are readable and traceable. | Product behavior and cross-workflow behavior remain unclear. |
| Consistency | Partial | Many invariants are repeated consistently. | Negative requirements are strong. | SQLite fallback, completion ownership, Plan artifact interpretation, and archive behavior create unresolved tension. |
| Completeness | Partial | Discovery, authority, and decision inventories are broad. | Requirements cover many current facts. | Missing behavior remains in selection, recovery, handoffs, concurrency, HITL, and extensibility. |
| Traceability | Partial | Roadmap has strong traceability; decision docs cite source concepts. | Requirements can support audit trails for Roadmap. | Plan/Main traceability is weak and lacks canonical ledger parity. |
| Non-overlap | Partial | Domains are separated into Roadmap, Plan, Execution, Completion, Storage. | Some boundaries are clear. | Completion and evaluation overlap across Main/Roadmap; storage authority cuts across all domains. |
| Testability | Partial | Many invariants are concrete: strict checkboxes, `.agents` exclusion, preflight inputs, stale projection blocks. | Some requirements are directly testable. | Unknowns and inferred eligibility lack acceptance criteria. |
| Observability | Partial | Observable facts and events are cataloged. | The system can report many facts. | Plan/Main branch history, stale partial artifacts, and already-closed state remain weakly observable. |
| Authority | Partial | Layered authority is documented by domain. | Local authority is often clear. | Cross-domain precedence and conflicting authority resolution are incomplete. |
| Ambiguity | High | Documents explicitly list ambiguity inventories and unknowns. | The ambiguity is recognized rather than hidden. | Several ambiguities materially affect architecture and remain unresolved. |

## 19. Readiness Assessment

**Outcome: Partially Ready**

- **Evidence**: The requirements define many facts, invariants, constraints, decision inputs, and known unsafe interpretations.
- **Inference**: The current documents are ready as an architecture input for discovery, status reporting, and constraint preservation.
- **Uncertainty**: The current documents are not ready as the sole basis for full Workflow Orchestrator architecture because core behavior would still be invented in identity, selection, chaining, storage precedence, recovery, completion ownership, HITL scope, concurrency, and extensibility.

**Answer to the success question**

- **Inference**: The Workflow Orchestrator cannot be fully architected from the current requirements without forcing the architect to invent behavior.
- **Uncertainty**: The missing information is not cosmetic; it affects state ownership, command scope, persistence authority, safety boundaries, recovery, and user-facing behavior.

## 20. Remaining Unknowns

1. **Uncertainty**: What exact product behavior should `looprelay` expose?
2. **Uncertainty**: Should Traditional Roadmap and Eval-Driven Roadmap become explicit workflow identities or remain inferred Roadmap behavior variants?
3. **Uncertainty**: What is the authoritative adapter from Roadmap active epic/spec outputs to Plan's `.agents/specs/epic.md` input?
4. **Uncertainty**: Should Roadmap -> Plan -> Execution ever be automatically chained?
5. **Uncertainty**: What precedence applies when SQLite canonical state and filesystem exports disagree?
6. **Uncertainty**: Is storage verification mandatory before workflow discovery, selection, recovery, or completion?
7. **Uncertainty**: May storage sync run automatically, or only through explicit user invocation?
8. **Uncertainty**: What are Plan partial-output recovery semantics?
9. **Uncertainty**: What are Execution partial-state recovery semantics beyond handoffs, decisions, and Git state?
10. **Uncertainty**: What complete Execution preflight is required before opening an execution session?
11. **Uncertainty**: Which completion certification path is authoritative when Main CLI and Roadmap evidence differ?
12. **Uncertainty**: What durable completed-state marker survives completion archive cleanup?
13. **Uncertainty**: How should archive retry behave after partial archive materialization?
14. **Uncertainty**: How should completed epic archive indexes be allocated after gaps or non-numeric directories?
15. **Uncertainty**: Which Roadmap unblock intents are intentionally unsupported versus incomplete?
16. **Uncertainty**: How should HITL blockers be scoped across workflows?
17. **Uncertainty**: What global lifecycle applies to human decisions?
18. **Uncertainty**: What concurrency model governs simultaneous status, storage verify, storage sync, Plan, Roadmap, Execution, and human edits?
19. **Uncertainty**: What release evidence is required before scoped artifact app-server operations are trusted without sandbox fallback?
20. **Uncertainty**: How should permission-policy hard-deny invariants be protected from configurable parser bypasses?
21. **Uncertainty**: Should telemetry absence affect workflow discovery or decision confidence?
22. **Uncertainty**: Are legacy Roadmap execution-preparation states permanent compatibility states, future active states, or historical persisted values only?
23. **Uncertainty**: What traceability level is required for Plan and Main Execution to match Roadmap transition journaling?
24. **Uncertainty**: Should Plan adversarial review outputs be part of a durable cross-workflow contract?
25. **Uncertainty**: Should completion review delete decisions be separately discoverable as a stage?
26. **Uncertainty**: What artifact ownership rules apply across Roadmap, Plan, Execution, Completion, Storage, and legacy compatibility exports?
27. **Uncertainty**: What invalidation propagation applies from upstream drift into Plan outputs, Execution decisions, handoffs, completion evidence, and archive state?
28. **Uncertainty**: Which concepts are closed sets and which must be extensible without revising requirements?

End of audit.
