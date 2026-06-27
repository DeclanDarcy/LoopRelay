# Command Center Evolution Implementation Plan

## Purpose

Evolve Command Center from its current repository inspection and workflow orchestration system into a persistent, repository-centered engineering runtime. The end state is a product where Repository Knowledge is the durable body of repository information, Repository Understanding is its current living view, and planning, execution, decisions, history, health, and intelligence all contribute to it.

The product experience must be a projection of the information architecture. Runtime creates the durable conditions for Repository Knowledge to evolve; the UI exposes that knowledge through coherent repository workflows; intelligence remains observational and evidence-backed.

The plan is intentionally incremental. Every phase must leave the solution buildable, regression-protected, and architecturally coherent. Runtime composition may change; semantic authority must not move to the runtime, UI, transport, generated artifacts, or compatibility layers.

## Current Baseline

The solution is a multi-project .NET backend with a React UI and a thin Tauri shell:

- `src/CommandCenter.Core`: repositories, artifacts, configuration, planning projections, low-level repository-owned artifact access.
- `src/CommandCenter.Execution`: operational execution sessions, context resolution, prompts, provider/process launch, Git, handoffs, monitoring, execution metadata.
- `src/CommandCenter.DecisionSessions`: decision session records, registry, metrics, economics, coherence, lifecycle policy, transfer eligibility, continuity artifacts, observability, certification, and recovery.
- `src/CommandCenter.Decisions`: structured decision domain, generation, governance, certification, quality, lifecycle, evidence, proposals, refinement, and persistence.
- `src/CommandCenter.Continuity`: operational context, context proposals, compression, understanding diffs, decision assimilation, diagnostics, reports, and context lifecycle.
- `src/CommandCenter.Workflow`: workflow state machine, preparation, execution projection, handoff, decision coordination, Git projection, continuation, recovery, health, gates, certification, and reporting.
- `src/CommandCenter.Reasoning`: reasoning capture, graph, materialization review, reconstruction, certification, and repository-backed reasoning records.
- `src/CommandCenter.Middle`: cross-context projections and operational context generation that depends on Execution.
- `src/CommandCenter.Backend`: ASP.NET endpoint composition and DI.
- `src/CommandCenter.UI`: React presentation, resource hooks, feature workspaces, characterization tests, generated contract pilot artifacts, and Tauri API wrappers.
- `tests/CommandCenter.Backend.Tests`: backend unit, endpoint, architecture, contract oracle, freshness, generated artifact, and certification tests.

Important existing capabilities to preserve:

- Repository artifacts remain authoritative in each repository under `.agents`.
- Existing domain services own semantic meaning.
- Existing contract oracle, generated TypeScript pilot, artifact freshness, request-boundary, consumer-verification, and architecture governance tests remain part of certification.
- Decision sessions remain separate from operational execution sessions.
- Execution owns Git, code changes, handoff generation, and operational provider interaction.
- Decisions domain owns decision structure, validation, lifecycle, relationships, persistence, and deterministic fallback behavior.
- Continuity owns operational context, compression, understanding evolution, and assimilation semantics.
- Reasoning owns reasoning records, graph relationships, and reasoning materialization policy.
- React presents backend-owned facts and must not infer lifecycle legality, authority, health, eligibility, certification, recovery, or recommendation semantics from weak strings.

Known debt to burn down during the early phases:

- Extract role-agnostic process/runtime infrastructure from `CommandCenter.Execution` into `CommandCenter.Agents`.
- Replace truncate-then-write persistence with atomic temp-write plus replace semantics for repository and local JSON stores.
- Centralize state transitions for execution, continuity, workflow, repository runtime, repository runs, decision runtime, and conversation turns.
- Move long-lived runtime ownership out of stateless endpoint handlers.
- Reduce large endpoint and orchestration files as their responsibilities become runtime commands.
- Replace deterministic token estimates with observed runtime accounting once persistent sessions exist.
- Keep generated contracts and compatibility wrappers synchronized with backend-owned contract identities.

## Architectural Invariants

1. Runtime coordinates; domains decide.
2. Repository Knowledge is the central durable information body. Repository Understanding is its current living view. Plans, executions, decisions, handoffs, history, evidence, reasoning, continuity, and intelligence all contribute to it.
3. Information endures; runtime is transient. Repository Runtime, Agent Runtime, Repository Runs, sessions, streams, and live processes may disappear and be reconstructed or replaced. Repository Understanding, Repository Knowledge, plans, decisions, evidence, history, lineage, and governed artifacts must endure.
4. One repository has at most one active Repository Runtime.
5. Repository Run is an explicit coordination object only while it earns an independent lifecycle. If implementation proves that progress, iteration, journal, and turn sequencing belong directly inside Repository Runtime without loss of authority or recoverability, Repository Run may collapse into Repository Runtime through governed architectural decision and migration evidence.
6. Agent Runtime is role-agnostic and owns process/session lifecycle only.
7. Session role selects prompt, sandbox, effort, tools, permissions, and policy.
8. Operational Session and Decision Session are different roles and different domain concepts, even when both use Agent Runtime.
9. Repository Runtime must not compute execution, decision, continuity, reasoning, Git, workflow, contract, information, intelligence, or UI semantics.
10. Execution owns operational semantics, Git, code mutation, handoffs, execution evidence, and operational prompts.
11. Decision Runtime owns live decision conversation behavior, but Decisions domain owns decision validity and durable decision semantics.
12. Humans own intent, approval, ratification, edits, and the decision to start or continue execution.
13. Continuity owns durable repository understanding and context evolution.
14. Reasoning owns knowledge relationships derived from reasoning evidence.
15. Intelligence is observational. Intelligence may identify opportunities, synthesize knowledge, assess trends, and recommend improvements, but it must never mutate repositories, plans, decisions, execution, operational context, governance state, or authority boundaries autonomously.
16. Contracts describe externally observable backend-owned shapes and are never redefined by UI, Rust, mocks, tests, or generated TypeScript.
17. Live process state is disposable. Durable state, journals, operational context, decisions, plans, history, and knowledge are recoverable.

## Delivery Rules

- Ship in vertical slices that include model, service, persistence, projection, contract, UI/resource changes where applicable, and tests.
- Break phase work into five architectural slices when creating implementation tickets or certification evidence: Runtime, Information, Product, Communication and Contracts, and Certification. Not every phase needs equal work in every slice.
- Prefer compatibility adapters over breaking existing endpoints during migration.
- Every phase must pass the backend suite before being accepted.
- Any externally observable contract change must update or add contract identity evidence, fixtures or generated artifacts, consumer verification, freshness manifests, and request-boundary checks where applicable.
- Any new architectural authority, invariant, compatibility exception, generated artifact exception, transport exception, or regression weakening must have decision evidence and documentation updates.
- Prefer additive endpoint paths and UI surfaces until the replacement is certified.
- Keep user-facing terminology repository-centered: Repository, Plan, Execution, Decision, Understanding, History, Health, Knowledge. Reserve runtime/session/registry/provider/transfer terms for diagnostics.

## Verification Baseline

Use these commands as the common acceptance baseline. Run narrower filters during slice development, but every phase certification must include the relevant full sets.

```powershell
dotnet build CommandCenter.slnx
dotnet test tests\CommandCenter.Backend.Tests\CommandCenter.Backend.Tests.csproj
```

```powershell
Push-Location src\CommandCenter.UI
npm run build
npm run lint
npm run test
npm run test:e2e
Pop-Location
```

If shell work is touched:

```powershell
Push-Location src\CommandCenter.Shell
cargo build
cargo test
Pop-Location
```

Contract-focused changes must also run the contract oracle, consumer verification, generated artifact freshness, generated pipeline, and request-boundary tests that match the touched contract families.

## Phase 0 - Runtime Foundation

Goal: establish permanent runtime boundaries without changing observable product behavior.

Implementation:

- Add `src/CommandCenter.Agents` to the solution.
- Move role-agnostic process abstractions and implementations from `CommandCenter.Execution` into `CommandCenter.Agents`:
  - `IProcessRunner`
  - `ProcessRunner`
  - process run/start result models
  - Codex executable resolution if it has no operational semantics
  - provider/process lifecycle primitives
  - stream/event primitives that are not execution-specific
- Keep operational concepts in `CommandCenter.Execution`: Git, execution context, handoff, operational prompts, execution session state, commit, push, and operational evidence.
- Introduce shared runtime primitives:
  - `SessionIdentity`
  - `SessionRole`
  - `AgentSessionSpec`
  - `SandboxProfile`
  - `EffortProfile`
  - `AgentProcessState`
  - `AgentTurnState`
- Add generated prompt infrastructure under `CommandCenter.Core.Prompts` with named prompt builders for planning, execution, decisions, transfer, operational deltas, and context updates. Existing literal prompt composition in Execution must become a compatibility layer over generated prompt output.
- Add initial repository lifecycle models in Core or a new runtime-neutral model package:
  - `Idle`
  - `PlanAuthoring`
  - `PlanReady`
  - `ExecutingPlan`
  - `Completed`
- Add first-class information records for Planning Intent, Plan, Plan Revision, and Repository Run Identity without changing existing markdown persistence.
- Add architecture tests that prevent:
  - `CommandCenter.Agents` referencing Execution, Decisions, Workflow, Continuity, Reasoning, Middle, Backend, or UI.
  - DecisionSessions referencing operational Execution orchestration.
  - Runtime objects owning domain semantic decisions.
  - UI-local semantic inference for runtime lifecycle, health, eligibility, recovery, and certification.
- Harden persistence with a shared atomic JSON file writer used by application configuration, artifact store write paths where appropriate, execution session store, decision session repository, workflow repository, and continuity proposal store.
- Centralize existing execution and continuity state transition validation before new runtime state is added.

Certification:

- Existing backend and UI behavior remains unchanged.
- Execution still works through the existing public APIs.
- DecisionSessions still compile without referencing operational Execution orchestration.
- Architecture governance tests cover Agent Runtime boundaries, prompt authority, repository lifecycle ownership, and information authority.
- Contract fixtures and generated artifact freshness remain current.

## Phase 1 - Persistent Agent Runtime

Goal: make Codex-backed sessions interactive and reusable across multiple turns.

Implementation:

- Add `IAgentProcess` with operations for open, submit turn, stream output, observe turn completion, cancel, interrupt, dispose, and query health.
- Add `IAgentRuntime` and an implementation that owns live process creation, stream subscription, prompt queueing, cancellation, interruption, disposal, and shutdown.
- Add an `AgentSessionRegistry` keyed by `SessionIdentity`, with ownership, lookup, enumeration, removal, health, and disposal.
- Add session stream support:
  - turn start
  - output
  - diagnostics
  - completion
  - failure
  - cancellation
  - reconnect/replay metadata
- Replace Execution's one-shot provider implementation with a compatibility adapter:
  - open operational session
  - submit one prompt
  - stream output through existing monitoring
  - dispose after completion
  - preserve current API behavior
- Improve process supervision:
  - retain reader/exit tasks
  - observe task failures
  - replace fixed process-start delay with deterministic exit probing
  - detect hung sessions, protocol violations, unexpected exits, cancellation, and timeout.
- Add role-aware sandbox and effort handling to `AgentSessionSpec`.
- Add runtime diagnostics and metrics:
  - session count
  - prompt count
  - turn count
  - lifetime
  - current state
  - failures
  - cancellation/disposal reason
- Add generated contracts and UI types for runtime status, session diagnostics, stream event payloads, and turn completion where they cross backend boundaries.

Certification:

- Multiple prompts execute in the same live agent process.
- Streaming continues across turns.
- Existing one-shot execution remains behaviorally equivalent.
- Session cleanup occurs on cancellation, failure, disposal, and application shutdown.
- Concurrent sessions are isolated.
- Runtime tests cover long output, rapid prompts, cancellation, disposal, failure recovery, and registry ownership.

## Phase 2 - Repository Runtime

Goal: make each registered repository a living runtime with explicit lifecycle, memory, reconstruction, and coordination ownership.

Implementation:

- Add `src/CommandCenter.RepositoryRuntime` to the solution.
- Add `RepositoryRuntime`, `IRepositoryRuntimeRegistry`, `IRepositoryRuntimeSupervisor`, and runtime command/event models.
- The registry maps repository id to exactly one runtime and owns creation, lookup, disposal, reconstruction, enumeration, and health.
- Runtime lifecycle states:
  - `Uninitialized`
  - `Loading`
  - `Ready`
  - `Running`
  - `Stopping`
  - `Disposed`
- Runtime memory is explicit and reconstructable:
  - active planning session id
  - active run id
  - active decision session id
  - current streams
  - current lifecycle
  - transient metadata
- Runtime reconstruction reads repository registration, repository artifacts, execution/decision/workflow records, operational context, run journals, and lifecycle records. It never reconstructs live processes.
- Repository Runtime composes, but does not interpret, Agents, Execution, DecisionSessions, Decisions, Workflow, Continuity, Reasoning, and Middle projections.
- Move orchestration entry points for planning, execution, decision, continuation, streaming, and recovery behind runtime commands while retaining existing endpoint compatibility.
- Add runtime projections for lifecycle, readiness, activity, health, ownership, and diagnostics without exposing process handles or registry internals.
- Extend generated contracts for repository runtime lifecycle, readiness, health, state, and command responses.

Certification:

- Duplicate runtimes for a repository are impossible.
- Runtime lifecycle transitions are centralized and tested.
- Runtime disposal releases live sessions and streams.
- Runtime reconstruction produces the same durable projection after application restart.
- Existing endpoints continue to work through compatibility paths.

## Phase 3 - Planning Runtime

Goal: make planning a first-class, persistent, repository-scoped runtime conversation.

Implementation:

- Introduce Planning Session models and persistence:
  - session identity
  - lifecycle
  - conversation history
  - current plan revision
  - planning metadata
  - artifacts consumed and produced
- Add Planning Session coordination to Repository Runtime using an operational sandbox and high reasoning effort profile.
- Promote planning inputs into first-class information:
  - Planning Intent
  - Requirements Artifact
  - Plan
  - Plan Revision
  - Planning Session
  - Planning State
- Preserve existing markdown artifacts and make them durable serialization, not UI-only documents.
- Implement planning protocols:
  - write initial plan from planning intent and requirements artifacts
  - revise plan from human feedback
  - preserve session context across revisions
  - complete planning only when the user chooses
- Add planning streams with turn boundaries, replay, cancellation, reconnect, and completion.
- Add backend planning endpoints through Repository Runtime commands.
- Add UI planning workspace:
  - requirements artifact editor
  - plan viewer
  - feedback editor
  - live stream
  - revision history
  - readiness and Execute Plan action
- Add generated contracts for planning lifecycle, session, commands, artifacts, stream events, and plan revisions.

Certification:

- A repository without an accepted plan enters Plan Authoring.
- Planning can run indefinitely without triggering execution.
- Plan revisions occur in the same persistent planning session.
- Current plan and revision history are durable.
- UI supports authoring, revision, cancellation, reload, and execution readiness.

## Phase 4 - Repository Run Boundary and Operational Execution Runtime

Goal: stop treating execution as a disconnected provider invocation and introduce the narrowest durable coordination object that can own execution progress, iteration, journal, and lifecycle. The initial implementation should model this as Repository Run, while preserving the option to collapse it into Repository Runtime if an independent run lifecycle proves unnecessary.

Implementation:

- Add an explicit architecture decision checkpoint for Repository Run before broad adoption:
  - why it needs a distinct identity from Repository Runtime
  - what lifecycle it owns independently
  - which durable records it owns
  - what would collapse into Repository Runtime if the boundary is not justified
  - which contracts and migration path would preserve external behavior if it collapses
- Add Repository Run models under `CommandCenter.RepositoryRuntime`:
  - run id
  - repository id
  - lifecycle
  - current phase
  - iteration
  - current owner
  - current operational session id
  - current handoff
  - execution metadata
  - run journal
- Run lifecycle states:
  - `Created`
  - `Preparing`
  - `Executing`
  - `Waiting`
  - `Completed`
  - `Cancelled`
  - `Failed`
- Add append-only run journal persistence with plan reference, handoff references, execution metadata, lifecycle events, turn events, and milestone/progress records.
- Route Start Execution through:
  - endpoint
  - Repository Runtime
  - Repository Run
  - Execution
  - Agent Runtime operational session
- Execution remains the authority for context building, Git, operational prompts, provider interaction, handoffs, commit, and push.
- Repository Run owns sequencing, iteration, lifecycle, current phase, and durable progress only if those responsibilities remain distinct from Repository Runtime coordination.
- Align execution streams with Repository Run events while preserving existing SSE/resource behavior.
- Connect repository lifecycle transitions:
  - `PlanReady` -> `ExecutingPlan`
  - `ExecutingPlan` -> `Completed`
  - failure and cancellation states remain explicit and recoverable.
- Add run recovery from journal and execution records. Live processes are never recovered.
- Add generated contracts for repository run, run lifecycle, run journal, execution readiness, execution metadata, and stream events.

Certification:

- One active run or active runtime conversation per repository is enforced, depending on the accepted Repository Run boundary.
- Execution starts and advances through Repository Runtime.
- Existing execution semantics remain intact.
- Handoffs become run artifacts with provenance.
- Run recovery restores durable run state and projections.
- Repository Run has explicit justification, or the implementation records a governed collapse plan into Repository Runtime before downstream phases depend on it.

## Phase 5 - Decision Runtime

Goal: make Decision Sessions persistent Codex-backed runtime participants while preserving separation from operational execution.

Implementation:

- Add `IDecisionRuntime` and `DecisionRuntimeService` to `CommandCenter.DecisionSessions`.
- Decision Runtime uses `CommandCenter.Agents` with:
  - Decision role
  - read-only or zero-permission sandbox
  - high reasoning effort
  - repository-scoped context
- Decision session lifecycle:
  - `Created`
  - `Active`
  - `Waiting`
  - `Transferred`
  - `Retired`
  - `Failed`
- Implement decision conversation protocol:
  - consume operational context and current handoff
  - request next decisions
  - stream decision output
  - preserve warm conversation state
  - allow multiple turns
- Replace free-form decision output in runtime paths with structured `DecisionProposal` JSON.
- Parse structured output into existing Decisions domain services for validation, fallback, lifecycle, relationships, evidence, quality, governance, and persistence.
- Add human review flow:
  - proposal display
  - evidence and tradeoffs
  - editable decision content
  - submit/ratify
  - preserve human edits and revision history
- Repository Run starts and maintains the active Decision Session, tracks iterations, and coordinates submissions.
- Add decision streams for proposal output, lifecycle, reasoning-safe diagnostics, completion, failure, and review readiness.
- Add recovery for pending structured proposals and review state from durable records.
- Add architecture tests preventing Execution from referencing Decision Runtime and Decision Runtime from referencing operational Execution orchestration.
- Add generated contracts for decision runtime lifecycle, proposal, submission, revisions, streams, and metadata.

Certification:

- Decision Sessions are backed by live Agent Runtime processes.
- Decision Runtime never performs code, Git, commit, push, workflow, or planning operations.
- Every submitted decision passes Decisions domain validation.
- Human review is required before a decision advances the run.
- Deterministic decision services remain available as fallback.

## Phase 6 - Continuous Repository Conversation Loop

Goal: compose operational turns, decision turns, and human review into one continuous governed repository conversation. If Repository Run remains independent, it owns the conversation progression; if Repository Run collapses into Repository Runtime, the same conversation model moves intact under Repository Runtime.

Implementation:

- Add a unified turn model:
  - `OperationalTurn`
  - `DecisionTurn`
  - `HumanReviewTurn`
- Every turn has:
  - identity
  - owner
  - start timestamp
  - stream references
  - completion condition
  - transition result
  - diagnostics
- Add conversation state to the accepted progression owner, initially Repository Run:
  - current turn
  - previous turn
  - iteration
  - current owner
  - conversation history
  - checkpoint references
- Implement deterministic transitions:
  - operational execution produces handoff
  - handoff starts decision turn
  - structured decision proposal starts human review
  - human submission advances continuation
  - continuation starts next operational turn or completes the run
- Use `CommandCenter.Workflow` state machine and continuation services as the semantic workflow/progression authority. Repository Runtime coordinates only.
- Persist conversation progress and checkpoints in the run journal.
- Add continuous conversation stream that merges operational, decision, human review, lifecycle, and health events into one repository-centric timeline.
- Add UI conversation timeline that presents planning, execution, decisions, and continuation as one flow while preserving turn identity and authority.
- Add generated contracts for conversation state, turns, iteration, lifecycle, stream events, and timeline projections.

Certification:

- Runs advance through repeated operational/decision/human turns without hidden transitions.
- Human governance happens only at decision review/submit boundaries.
- Recovery resumes from durable conversation state.
- The UI presents one continuous repository conversation, not fragmented feature streams.

## Phase 7 - Long-Horizon Continuity and Intelligent Routing

Goal: allow repository execution to continue indefinitely while preserving understanding, authority, and governance quality. By the end of this phase, Operational Context has evolved into Repository Understanding: the current, durable, living view of the repository, even before richer knowledge, lineage, and query capabilities are added.

Implementation:

- Replace deterministic token estimates in runtime routing paths with observed token accounting:
  - prompt tokens
  - output tokens
  - turn growth
  - session utilization
  - context pressure
  - transfer thresholds
- Keep estimator services only for preflight, tests, and fallback diagnostics.
- Add repository-aware router decisions:
  - reuse warm Decision Session
  - transfer to new Decision Session
  - fail closed when required understanding cannot be reconstructed
- Implement warm session reuse when context pressure and health allow.
- Implement session transfer:
  - produce operational delta
  - update durable operational context
  - create new decision session
  - preserve identity, lineage, evidence, and authority
  - retire old session without preserving live process state
- Add Operational Delta model:
  - new understanding
  - changed understanding
  - removed understanding
  - compressed understanding
  - evidence references
  - identity/provenance
- Add context evolution flow through Continuity:
  - current operational context plus operational delta becomes updated Repository Understanding
  - Operational Context becomes the implementation artifact and serialization format behind the living Repository Understanding
  - preserve historical versions and compression history
  - remove temporary noise and completed low-value detail
- Make Repository Understanding canonical at the continuity boundary:
  - current understanding is no longer treated as an execution supplement
  - planning, execution, decisions, handoffs, transfers, and reasoning all feed the same durable understanding identity
  - runtime projections refer to Repository Understanding rather than exposing Operational Context as a product concept
- Add repository memory projections:
  - current understanding
  - historical understanding
  - operational history
  - decision history
  - reasoning history
  - transfer history
- Add transfer/reuse/recovery observability without exposing internal routing mechanics as primary product concepts.
- Add generated contracts for router decisions, transfer, operational delta, context versions, repository memory, and runtime evolution.

Certification:

- Long-running conversations transfer sessions without losing repository understanding.
- Repository Run identity, or the accepted runtime conversation identity, survives session replacement.
- Continuity remains semantic authority for context evolution and Repository Understanding.
- Operational Context has become the durable implementation artifact behind Repository Understanding.
- Decision generation resumes from durable understanding after transfer or restart.
- Stress tests cover many transfers, large repositories, context rewrites, and recovery after transfer.

## Phase 8 - Repository Knowledge and Information Intelligence

Goal: make Repository Knowledge first-class. Repository Knowledge is the broader durable body of repository information: Repository Understanding, history, evidence, knowledge graph, lineage, and queries. Repository Understanding remains the current, living view inside that knowledge body.

Implementation:

- Establish Repository Knowledge as the named information layer:
  - Repository Understanding: current living view
  - Repository History: time-ordered evolution
  - Repository Evidence: supporting artifacts and facts
  - Knowledge Graph: relationships among durable information
  - Information Lineage: provenance from intent through understanding
  - Information Queries: governed exploration over authoritative facts
- Preserve the Phase 7 boundary: Repository Understanding is already canonical. This phase enriches the surrounding knowledge layer rather than reintroducing understanding as a new feature.
- Promote durable information objects:
  - Intent
  - Plan
  - Handoff
  - Decision
  - Operational Context
  - Repository Understanding
  - Evidence
  - History Entry
  - Knowledge Relationship
- Treat Operational Context as the implementation artifact behind Repository Understanding.
- Extend Reasoning Graph into a repository Knowledge Graph connecting intent, plans, runs, handoffs, decisions, understanding, artifacts, evidence, and history.
- Add repository history as a continuous, queryable, repository-centric timeline:
  - planning history
  - execution history
  - decision history
  - understanding history
  - transfer history
  - knowledge evolution
- Add end-to-end lineage:
  - intent to plan
  - plan to run
  - run to handoff
  - handoff to decision
  - decision to understanding
  - understanding to future decisions
- Add information queries backed by authoritative information:
  - why a decision was made
  - how understanding changed
  - what evidence supports a claim
  - when a claim became true
  - which assumptions remain
  - which goals remain incomplete
- Add information authority tests:
  - Human owns intent and ratification.
  - Planning runtime generates proposals and revisions but does not approve them.
  - Decisions domain validates decisions.
  - Continuity owns Repository Understanding.
  - Reasoning owns Knowledge Graph semantics.
- Add UI knowledge, history, evidence, lineage, and understanding-evolution explorers.
- Add contracts for Repository Understanding, Knowledge Graph, Repository History, Information Lineage, Information Query, and Evolution projections.

Certification:

- Repository Understanding is the primary representation of repository knowledge.
- Repository Knowledge is the durable information layer that contains understanding, history, evidence, graph relationships, lineage, and queries.
- Knowledge relationships are evidence-backed and queryable.
- Information lineage is complete across planning, execution, decisions, and understanding.
- Repository intelligence explains current state without speculative conclusions.

## Phase 9 - Unified Product Experience

Goal: make the product a coherent projection of the certified Repository Knowledge architecture, so planning, execution, decisions, understanding, history, evidence, lineage, queries, and health feel like one repository experience.

Implementation:

- Promote repository lifecycle into primary navigation and workspace composition, but make Repository Knowledge the product's center and Repository Understanding the default living view.
- Build a unified repository workspace that centers:
  - current understanding
  - repository knowledge
  - current objective
  - current plan
  - current execution turn
  - current decision review
  - knowledge and evidence
  - current health
  - conversation timeline
- Move diagnostics, reasoning internals, governance internals, continuity mechanics, contracts, and raw runtime health into secondary inspection surfaces.
- Unify visible terminology:
  - Repository
  - Understanding
  - Plan
  - Execution
  - Decision
  - Knowledge
  - History
  - Health
- Hide runtime/session/registry/provider/transfer terminology from primary workflows unless the user is inspecting diagnostics.
- Replace fragmented streams with one repository conversation timeline backed by typed backend projections.
- Present operational context only as Repository Understanding in primary UI. Markdown and context mechanics remain implementation details.
- Complete decision-first review surface:
  - proposal
  - evidence
  - tradeoffs
  - editable human revision
  - submit
  - history
- Add knowledge, lineage, evidence, and understanding-evolution surfaces to the repository workspace without making them separate product silos.
- Add navigation and command palette flows that follow repository lifecycle and information needs, not project boundaries.
- Add product contracts for repository lifecycle, conversation timeline, repository dashboard, navigation, repository understanding, knowledge, history, evidence, and decision review.

Certification:

- Users can plan, execute, review decisions, continue, inspect understanding, inspect Repository Knowledge, and view history without leaving the repository workspace.
- Runtime transitions are visible only as activity/health/progress, not implementation machinery.
- Product surfaces render backend-owned information instead of reconstructing it in React.
- UI characterization and E2E tests cover the complete repository lifecycle and the Repository Understanding-centered workspace.
- Product terminology is consistent across components, empty states, buttons, headings, and diagnostics.

## Phase 10 - Production Readiness and Platform Certification

Goal: harden the complete platform for daily use.

Implementation:

- Certify the full repository lifecycle:
  - register/select repository
  - plan
  - execute
  - decide
  - review
  - continue
  - transfer/recover
  - complete
  - inspect historical knowledge
- Harden runtime reliability:
  - process stability
  - resource cleanup
  - per-repository isolation
  - memory pressure behavior
  - cancellation and shutdown
  - long-lived sessions
  - deterministic failure recovery
- Complete ecosystem integration:
  - Git
  - filesystem
  - Codex
  - Tauri
  - IDE-adjacent workflows
  - repository discovery
  - prompt generation
- Add production observability:
  - repository activity
  - conversation health
  - runtime health
  - session health
  - execution progress
  - decision progress
  - understanding evolution
  - failure and recovery state
- Add operational tooling:
  - runtime diagnostics
  - repository diagnostics
  - session diagnostics
  - recovery controls
  - health dashboards
  - export/support bundles
- Complete documentation:
  - architecture guide
  - runtime guide
  - information guide
  - protocol guide
  - user guide
  - operator guide
  - recovery guide
  - extension guide
- Complete release-path certification, including packaging and shell sidecar lifecycle.

Certification:

- Full backend, frontend, E2E, shell, contract, architecture, stress, recovery, scalability, and governance suites pass.
- Failure tests cover restart, session loss, transfer interruption, corrupted/partial context, repository reload, cancellation, and shutdown.
- Runtime failures never corrupt repository understanding.
- Operational documentation matches implemented behavior and recovery paths.

## Phase 11 - Continuous Architectural Evolution

Goal: make the platform safe to extend without foundational redesign.

Implementation:

- Add an explicit evolution framework for:
  - architecture
  - runtime
  - protocol
  - information
  - capability
  - product surface
- Add capability lifecycle:
  - proposal
  - evaluation
  - implementation
  - certification
  - integration
  - retirement
- Add extension boundaries for additional providers, runtimes, repository types, workflows, planning strategies, and reasoning strategies.
- Add architectural drift detection:
  - authority drift
  - runtime drift
  - protocol drift
  - information drift
  - boundary erosion
  - semantic duplication
  - generated artifact bypass
  - compatibility debt without retirement path
- Add self-assessment services that observe, but never mutate:
  - repository quality
  - runtime quality
  - knowledge quality
  - understanding quality
  - architecture quality
  - operational quality
- Strengthen governance so every architecture-affecting change maps to invariant, owner, evidence, mechanism, compatibility impact, rollback path, and baseline updates.

Certification:

- Runtime, protocol, information, knowledge, and product models can evolve independently while preserving contracts.
- New capabilities can be introduced through extension points, not structural rewrites.
- Drift detection is continuously executable.
- Human authority remains the gate for architectural and product evolution decisions.

## Phase 12 - Adaptive Engineering Intelligence

Goal: add governed, evidence-backed intelligence that identifies opportunities, synthesizes knowledge, and recommends improvements without autonomous repository mutation.

Implementation:

- Add `CommandCenter.Intelligence` or an equivalent bounded context once Repository Understanding and Knowledge Graph are certified.
- Add first-class intelligence information:
  - Opportunity
  - Recommendation
  - Trend
  - Assessment
  - Pattern
  - Repository Insight
  - Knowledge Synthesis
- Add opportunity discovery from authoritative information:
  - architectural inconsistencies
  - repeated friction
  - decision churn
  - historical instability
  - repository hotspots
  - knowledge gaps
  - planning deficiencies
  - documentation gaps
  - governance gaps
- Add knowledge synthesis:
  - patterns
  - lessons
  - successful strategies
  - repeated failures
  - architecture evolution
  - engineering practices
- Add recommendation generation:
  - architecture improvements
  - planning improvements
  - execution improvements
  - knowledge improvements
  - repository organization
  - documentation improvements
  - governance improvements
- Add trend analysis:
  - architecture growth
  - knowledge growth
  - decision quality
  - execution quality
  - understanding maturity
  - repository stability
  - engineering velocity
- Add continuous repository assessment:
  - current direction
  - architectural health
  - knowledge quality
  - planning quality
  - decision quality
  - execution quality
  - repository maturity
- Add adaptive planning inputs that improve planning with repository history, decision history, execution outcomes, and engineering knowledge while preserving human planning authority.
- Add repository intelligence workspace:
  - opportunities
  - recommendations
  - trends
  - knowledge synthesis
  - assessment
  - future outlook
- Add platform-wide learning only with explicit repository isolation, privacy boundaries, evidence lineage, and human-governed acceptance.

Certification:

- Intelligence remains observational.
- No intelligence service mutates repositories, decisions, plans, executions, operational context, or governance state autonomously.
- Every opportunity and recommendation has evidence lineage.
- Repository isolation is preserved.
- Recommendation quality, trend accuracy, authority boundaries, contracts, and regression behavior are tested.

## Cross-Cutting Workstreams

### Repository Communication Architecture

Repository communication is the umbrella for every durable or observable way the system carries intent, understanding, instructions, progress, evidence, and results across boundaries. It includes prompts, streams, journals, contracts, artifacts, events, diagnostics, and error envelopes.

Rules:

- Communication transports authority-owned facts; it does not become semantic authority.
- Prompts are generated, named, versioned where needed, and tied to session role and information inputs.
- Streams expose ordered runtime facts, turn boundaries, lifecycle events, replay/reconnect metadata, and terminal states without leaking process handles or registry internals.
- Journals preserve durable progress, lifecycle transitions, decisions, handoffs, context evolution, transfers, and recovery checkpoints.
- Artifacts remain repository-owned durable records unless explicitly classified as local runtime metadata.
- Events and diagnostics must name their owner, source, severity or health semantics when applicable, and recovery meaning.
- Contracts define externally observable communication shape and must be protected by oracle, consumer, freshness, generated artifact, and request-boundary verification where applicable.
- Error envelopes are communication contracts whenever structured failures cross backend, shell, stream, or UI boundaries.
- Every communication surface must state its durability: transient stream event, replayable event, append-only journal entry, repository artifact, local metadata, generated contract, or diagnostic projection.

### Contracts and Generated Artifacts

- Expand contract identities phase by phase.
- Add golden fixtures before consumer migration.
- Generate or verify TypeScript artifacts from backend-owned contract truth.
- Keep manual UI types as compatibility wrappers until generated consumer types are schema-complete and migration evidence exists.
- Add stream lifecycle contracts where ordering, reconnection, retry, terminal, or replay semantics are observable.
- Treat error envelopes as contracts wherever structured errors cross backend, shell, or UI boundaries.

### Persistence and Recovery

- All durable JSON writes use atomic replacement.
- Journals are append-only where history matters.
- Live processes are never durable state.
- Runtime, run, conversation, decision, context, transfer, knowledge, and intelligence records must reconstruct from repository artifacts and local metadata.
- Recovery tests must include crash/restart windows around every multi-write operation.

### State Machines

- Every lifecycle has one named transition home:
  - Agent Session
  - Repository Runtime
  - Planning Session
  - Repository Run
  - Operational Session
  - Decision Session
  - Conversation Turn
  - Operational Context Proposal
  - Workflow
  - Transfer
  - Intelligence Opportunity
- Services call transition APIs; they do not duplicate guard logic with scattered `if` statements.

### UI Architecture

- Resource hooks own loading, refresh, mutation, stale response handling, and failure state for one backend contract family.
- Controllers build feature view models and action sequencing.
- Workspaces compose controllers and local interaction flow.
- Presentation components render facts, labels, colors, icons, layout, and accessibility text only.
- The application root owns repository selection, global shell state, navigation, and workspace composition only.

### Documentation and Governance

- Update `docs/architecture.md` when authority, ownership, or lifecycle changes.
- Update `docs/contracts.md` and contract endpoint catalogs when a contract identity or compatibility path changes.
- Update `docs/architectural-mechanisms.md` when a verifier is added, strengthened, weakened, quarantined, replaced, or retired.
- Add evidence for each phase certification with commands run, files changed, scope, known limits, compatibility impact, and rollback path.
- Keep decisions, evidence, mechanisms, and capability documentation aligned before accepting a phase.

## Phase Acceptance Checklist

Each phase is complete only when:

- Code compiles.
- Backend tests pass.
- Relevant UI build, lint, tests, and E2E checks pass.
- Shell tests pass if shell or transport behavior changed.
- Contract oracle, consumer verification, freshness, generated pipeline, and request-boundary checks pass for touched contract families.
- Architecture governance tests protect new or changed invariants.
- New runtime or lifecycle states have centralized transition tests.
- Recovery behavior is tested from durable state.
- Product behavior is characterized when user-facing behavior changes.
- Documentation and evidence match implemented behavior.
- Compatibility debt has an owner, consumer list, replacement path, retirement condition, and guard.
- Rollback path is explicit for every architecture-affecting change.
