# Command Center Evolution Implementation Plan

## Purpose

Evolve Command Center from its current repository inspection and workflow orchestration system into a persistent, repository-centered engineering runtime. The end state is a product where Repository Knowledge is the durable body of repository information, Repository Understanding is its current living view, and planning, execution, decisions, history, health, and intelligence all contribute to it.

The product experience must be a projection of the information architecture. Runtime creates the durable conditions for Repository Knowledge to evolve; the UI exposes that knowledge through coherent repository workflows; intelligence remains observational and evidence-backed.

The plan is intentionally incremental. Every phase must leave the solution buildable, regression-protected, and architecturally coherent. Runtime composition may change; semantic authority must not move to the runtime, UI, transport, generated artifacts, or compatibility layers.

## Current Baseline

The solution is a multi-project .NET backend with a React UI and a thin Tauri shell:

- `src/CommandCenter.Core`: repositories, artifacts, configuration, planning projections, low-level repository-owned artifact access, and the authored `.prompt` files generated into `CommandCenter.Core.Prompts`.
- `src/CommandCenter.Execution`: operational execution sessions, context resolution, operational prompt input shaping, generated prompt adapters, provider/process launch, Git, handoffs, monitoring, execution metadata.
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
- Replace all literal prompt construction with generated renderers from `CommandCenter.Core.Prompts`.
- Preserve the authored prompt catalog as the prompt source of truth instead of recreating prompt text inside runtime, execution, decision, planning, or continuity services.
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
10. Execution owns operational semantics, Git, code mutation, handoffs, execution evidence, and operational prompt input shaping. `CommandCenter.Core.Prompts` owns canonical prompt text.
11. Decision Runtime owns live decision conversation behavior, but Decisions domain owns decision validity and durable decision semantics.
12. Humans own intent, approval, ratification, edits, and the decision to start or continue execution.
13. Continuity owns durable repository understanding and context evolution.
14. Reasoning owns knowledge relationships derived from reasoning evidence.
15. Intelligence is observational. Intelligence may identify opportunities, synthesize knowledge, assess trends, and recommend improvements, but it must never mutate repositories, plans, decisions, execution, operational context, governance state, or authority boundaries autonomously.
16. Contracts describe externally observable backend-owned shapes and are never redefined by UI, Rust, mocks, tests, or generated TypeScript.
17. Live process state is disposable. Durable state, journals, operational context, decisions, plans, history, and knowledge are recoverable.
18. Prompt templates are authored repository source under `src/CommandCenter.Core/Prompts` and generated at build time into `CommandCenter.Core.Prompts` by `Lib.Prompts`.
19. Generated prompt renderers are the only authority for prompt text. Runtime, Execution, DecisionSessions, Planning, Continuity, Workflow, UI, shell, tests, and compatibility adapters must not duplicate canonical prompt strings.
20. Session role and workflow phase select the generated prompt. Agent Runtime receives rendered prompt text and prompt metadata; it never selects, edits, or semantically interprets prompts.
21. Every agent turn that uses a generated prompt records prompt name, generated type, `SourceHash`, role, workflow phase, and the durable input artifacts used to render it.
22. Prompt template changes are architecture-affecting communication changes. They require generated prompt build validation, prompt provenance impact review, and compatibility evidence for existing sessions, journals, artifacts, and tests.

## Prompt Architecture

`CommandCenter.Core.Prompts` is the canonical prompt API. The authored source files live in `src/CommandCenter.Core/Prompts/*.prompt`; `Lib.Prompts` consumes them as MSBuild `AdditionalFiles` and generates static prompt classes at compile time. Each generated class exposes `Template`, `SourceHash`, and `Render(...)`; malformed placeholder syntax fails the build through `PROMPT001`-`PROMPT004`.

The generator is an analyzer-only dependency. It introduces no runtime parser, no runtime file lookup, and no embedded-resource prompt authority. Runtime services render prompts through generated methods and pass already-rendered text plus prompt provenance into Agent Runtime.

The initial canonical prompt catalog is:

- Planning: `WritePlanAgainstCodebase`, `WritePlanForNewCodebase`, `RevisePlan`, `ExtractMilestones`.
- Operational execution: `StartExecution`, `ContinueExecution`.
- Decision sessions: `StartDecisionSession`, `StartDecisionSessionFromTransfer`, `GetNextDecisions`.
- Continuity: `ProduceOperationalDelta`, `UpdateOperationalContext`.

Prompt placeholders accept string payloads. Domain services own the typed source information, serialize or format their authority-owned inputs into the prompt payloads, call the generated renderer, and record the input artifact identities. Prompt templates may instruct agents, but they do not become semantic authority for plans, executions, decisions, continuity, reasoning, Git, workflow, contracts, or UI behavior.

## Delivery Rules

- Ship in vertical slices that include model, service, persistence, projection, contract, UI/resource changes where applicable, and tests.
- Break phase work into five architectural slices when creating implementation tickets or certification evidence: Runtime, Information, Product, Communication and Contracts, and Certification. Not every phase needs equal work in every slice.
- Prefer compatibility adapters over breaking existing endpoints during migration.
- Every phase must pass the backend suite before being accepted.
- Any externally observable contract change must update or add contract identity evidence, fixtures or generated artifacts, consumer verification, freshness manifests, and request-boundary checks where applicable.
- Any prompt behavior change must update the authored `.prompt` file, preserve generated prompt provenance, and add or update prompt rendering, prompt selection, and no-literal-prompt governance coverage.
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

(See ./milestones/m0-runtime-foundation.md)

## Phase 1 - Persistent Agent Runtime

(See ./milestones/m1-persistent-agent-runtime.md)

## Phase 2 - Repository Runtime

(See ./milestones/m2-repository-runtime.md)

## Phase 3 - Planning Runtime

(See ./milestones/m3-planning-runtime.md)

## Phase 4 - Repository Run Boundary and Operational Execution Runtime

(See ./milestones/m4-repository-run-operational-execution-runtime.md)

## Phase 5 - Decision Runtime

(See ./milestones/m5-decision-runtime.md)

## Phase 6 - Continuous Repository Conversation Loop

(See ./milestones/m6-continuous-repository-conversation-loop.md)

## Phase 7 - Long-Horizon Continuity and Intelligent Routing

(See ./milestones/m7-long-horizon-continuity-intelligent-routing.md)

## Phase 8 - Repository Knowledge and Information Intelligence

(See ./milestones/m8-repository-knowledge-information-intelligence.md)

## Phase 9 - Unified Product Experience

(See ./milestones/m9-unified-product-experience.md)

## Phase 10 - Production Readiness and Platform Certification

(See ./milestones/m10-production-readiness-platform-certification.md)

## Phase 11 - Continuous Architectural Evolution

(See ./milestones/m11-continuous-architectural-evolution.md)

## Phase 12 - Adaptive Engineering Intelligence

(See ./milestones/m12-adaptive-engineering-intelligence.md)

## Cross-Cutting Workstreams

### Repository Communication Architecture

Repository communication is the umbrella for every durable or observable way the system carries intent, understanding, instructions, progress, evidence, and results across boundaries. It includes prompts, streams, journals, contracts, artifacts, events, diagnostics, and error envelopes.

Rules:

- Communication transports authority-owned facts; it does not become semantic authority.
- Prompts are generated, named, source-hashed, versioned where needed, and tied to session role, workflow phase, and authority-owned information inputs.
- Prompt text lives only in authored `.prompt` files and generated `CommandCenter.Core.Prompts` classes. Compatibility layers may adapt old call sites, but they must call generated renderers rather than own prompt literals.
- Prompt invocation records must survive recovery: prompt name, generated type, `SourceHash`, session role, workflow phase, rendered input artifact identities, and output artifact references.
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
- React never builds prompt text, infers prompt selection, or repairs prompt output. It sends user intent and edits to backend-owned prompt workflows.

### Documentation and Governance

- Update `docs/architecture.md` when authority, ownership, or lifecycle changes.
- Update `docs/contracts.md` and contract endpoint catalogs when a contract identity or compatibility path changes.
- Update `docs/architectural-mechanisms.md` when a verifier is added, strengthened, weakened, quarantined, replaced, or retired.
- Update prompt architecture documentation when prompt ownership, prompt catalog, prompt selection, or prompt provenance changes.
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
- Generated prompt compilation, prompt rendering, prompt selection, prompt provenance, and no-literal-prompt governance tests pass for touched prompt workflows.
- New runtime or lifecycle states have centralized transition tests.
- Recovery behavior is tested from durable state.
- Product behavior is characterized when user-facing behavior changes.
- Documentation and evidence match implemented behavior.
- Compatibility debt has an owner, consumer list, replacement path, retirement condition, and guard.
- Rollback path is explicit for every architecture-affecting change.
