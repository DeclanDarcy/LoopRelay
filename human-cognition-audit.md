# Human Cognition Audit

## Scope

Repository audited: `C:\kernritsu\LoopRelay`

Audit date: 2026-07-08

This audit evaluates only human cognition cost. It does not evaluate correctness, architecture sophistication, feature value, or test adequacy except where those surfaces change how much a human must reconstruct.

Representative transition audited in detail: `SelectNextEpic`, centered on `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:578`.

Prior audit artifact found: `transitions/select-next-epic.md`. No prior repository-level Human Cognition Audit was found.

## Verdict

❌ Architectural Debt

The repository imposes high unnecessary cognition. The implementation is not careless, but it is over-layered, over-governed, and too dependent on reconstruction. A new engineer cannot answer "what happens next?" from one obvious location. They must follow state planners, registries, prompt catalogs, string keys, transition helpers, persistence stores, lifecycle stores, journals, evidence writers, and documentation claims before they can trust their understanding of a single transition.

Estimated remaining clarity: 32 / 100.

This is not "understandable at a glance." It is "understandable after a careful excavation."

## Hard Failure Conditions

### Hidden Execution

Triggered.

`SelectNextInitiativeAsync` looks compact at `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:578`, but it hides many execution steps:

- `ProjectionCache.EnsureAsync` can run an agent prompt, validate output, mutate a projection manifest, write generated projections, and write blocker evidence.
- `RunPromptTransitionWithCompletionAsync` resolves input snapshots, writes state, appends transition journals, runs the prompt, writes completion state, and persists failure state.
- `RoadmapPromptRunner.RunRuntimePromptAsync` renders a prompt through `RoadmapPromptCatalog`, appends prompt policy, runs a read-only agent, streams console output, echoes silent output, and throws wrapped failures.
- Post-output selection materialization writes `.agents/selection.md`, captures HITL markers, writes numbered evidence, records provenance, upserts lifecycle, parses the result, and appends a decision.

The apparent method body is not the transition. It is a gateway into hidden work.

### Multiple Execution Authorities

Triggered.

Roadmap execution meaning is distributed across:

- `RoadmapStateMachine`
- `RoadmapStartupPlanner`
- `RoadmapResumePlanner`
- `RoadmapWorkflowStateClassifier`
- `RoadmapState`
- `PromptContractRegistry`
- `ProjectionRegistry`
- `RoadmapPromptCatalog`
- `TransitionInputResolver`
- prompt template files under `src/LoopRelay.Core/Prompts`
- artifact lifecycle, state, journal, provenance, and decision-ledger stores

Each file contains legitimate information, but together they force the reader to reconstruct authority.

### Mixed Concern Execution

Triggered.

`RoadmapStateMachine.cs` is a 1,905-line coordinator with 29 constructor collaborators. It mixes routing, prompt execution, state persistence, journal persistence, artifact mutation, lifecycle updates, decision ledger writes, evidence generation, recovery, console reporting, completion certification, and invariant failure handling.

The key example is `RunPromptTransitionWithCompletionAsync` at `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs:1404`, which combines prompt execution with transition input capture, state mutation, journaling, timing, and failure persistence.

### Excessive Reconstruction

Triggered.

Understanding `SelectNextEpic` requires at least these files:

- `src/LoopRelay.Roadmap.Cli/RoadmapStateMachine.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionCache.cs`
- `src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/ProjectionRegistry.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptContextBuilder.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionInputs.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapPromptRunner.cs`
- `src/LoopRelay.Roadmap.Cli/SelectionParser.cs`
- `src/LoopRelay.Roadmap.Cli/SelectionProvenance.cs`
- `src/LoopRelay.Roadmap.Cli/RoadmapStateStore.cs`
- `src/LoopRelay.Roadmap.Cli/TransitionJournalStore.cs`
- `src/LoopRelay.Roadmap.Cli/DecisionLedgerStore.cs`
- `src/LoopRelay.Roadmap.Cli/ArtifactLifecycleStore.cs`
- `src/LoopRelay.Core/Prompts/Planning/SelectNextEpic.prompt`
- `src/LoopRelay.Core/Prompts/Projections/ProjectionForSelectNextEpic.prompt`

This exceeds the audit threshold of five files by a wide margin.

### Locality Failure

Triggered.

The transition narrative already exists separately in `transitions/select-next-epic.md`, which is itself evidence that the implementation does not communicate the transition locally. The trace is helpful, but a portfolio-grade repository should not require a separate explanatory artifact to make a central transition readable.

### Hidden Branching

Triggered.

Execution branches through string values:

- runtime prompt names such as `"SelectNextEpic"`, `"CreateNewEpic"`, `"SplitEpic"`
- selection outcomes such as `"Select Existing Epic"` and `"Strategic Investigation Required"`
- prompt catalog switches
- prompt contract registry rows
- transition input resolver switches
- artifact writer names stored as strings, such as `"ArtifactPromotionService"`

The strings are repeated across registries, state machine branches, prompt catalog entries, tests, and documentation. That requires search-driven confidence.

### Duplicate Orchestration

Triggered.

The repository has several orchestration surfaces that repeat the conceptual shape "run agent, check artifact, persist side effects, advance":

- `RoadmapStateMachine`
- `LoopRunner`
- `DecisionSession`
- `PlanPipeline`
- `PlanSession`
- `PermissionedArtifactOperationStep`

Some duplication is domain-specific. The cognitive failure is that a reader cannot quickly tell which orchestration style is canonical, legacy, mirrored, or intentionally separate.

## Mandatory Penalty Summary

Minimum direct penalties observed:

| Penalty | Evidence | Points |
|---|---|---:|
| Hidden execution | Projection cache, prompt runner, shared transition runner, lifecycle/provenance side effects | -2 |
| Multiple execution authorities | State machine, startup planner, resume planner, classifier, registries, prompt catalog | -2 |
| Hidden branching | Runtime prompt names and decision outcomes are string-routed | -2 |
| Mixed persistence + execution | Prompt transition runners persist state and journals | -2 |
| Mixed routing + execution | `ContinueAfterSelectionAsync` routes selection and launches downstream transitions | -2 |
| Mixed reporting + execution | Console output is emitted from execution paths and prompt runners | -2 |
| String-driven execution authority | `PromptContractRegistry`, `RoadmapPromptCatalog`, `TransitionInputResolver` | -1 |
| Duplicate orchestration | Roadmap, main loop, plan pipeline, decision session, scoped operations | -3 |
| Transition requires more than five files | `SelectNextEpic` requires more than 15 files for confidence | -3 |
| Coordinator exceeds 1500 LOC | `RoadmapStateMachine.cs` is 1,905 lines | -4 |
| Constructor exceeds 25 collaborators | `RoadmapStateMachine` has 29 collaborators | -6 |
| Requires more than 10 simultaneous concepts | State, projection, prompt, context, snapshot, journal, lifecycle, evidence, provenance, parser, decisions, resume safety | -3 |
| Additional abstraction without immediate readability improvement | Governance/provenance/semantic layers often explain after the fact instead of simplifying the read path | -3 |

Minimum penalty total: -35 before repeated occurrences.

## Category Scores

| Category | Score | Finding |
|---|---:|---|
| Cognitive Reconstruction Cost | 2 / 10 | One transition requires reconstructing hidden prompt, projection, state, journal, provenance, and lifecycle behavior. |
| Locality | 2 / 10 | Required facts live across many unrelated files and prompt templates. |
| Reading Resistance | 3 / 10 | Individual methods are often named clearly, but the main roadmap coordinator is too large and too mixed. |
| Navigation Penalty | 1 / 10 | Search is mandatory. Reading top-down is not enough. |
| Working Memory Tax | 2 / 10 | The reader must hold state, artifact lifecycle, transition status, prompt identity, projection freshness, evidence paths, and recovery intent together. |
| Concern Entanglement | 1 / 10 | Execution paths perform routing, persistence, reporting, recovery, and mutation together. |
| Execution Transparency | 3 / 10 | Entry points are visible, but actual execution is hidden behind planners and helper runners. |
| Transition Transparency | 3 / 10 | `SelectNextEpic` can be traced, but not read sequentially without external reconstruction. |
| Ownership Transparency | 2 / 10 | Ownership of state, route, artifact, recovery, and reporting is distributed. |
| Debugging Resistance | 3 / 10 | Tests exist, but the runtime effects are spread across many stores and journals. |
| Modification Resistance | 2 / 10 | Changing one transition risks prompt contracts, projection freshness, lifecycle, resume safety, and journal compatibility. |
| Semantic Noise | 2 / 10 | Governance, semantic reports, projection terminology, and compatibility terminology add more vocabulary than they remove. |
| Cognitive Compression | 2 / 10 | The same transition could reasonably require half as many concepts and files. |
| Engineer Respect | 3 / 10 | The code is serious and tested, but it makes the engineer pay too much up front. |

## Representative Transition: SelectNextEpic

The readable local shape is:

1. Ensure projection.
2. Build context.
3. Run prompt.
4. Write selection.
5. Record provenance and lifecycle.
6. Parse decision.
7. Append decision.
8. Route downstream.

The actual cognitive shape is:

1. `RunAsync` loads persisted state.
2. `RoadmapStartupPlanner` decides whether preflight is required.
3. `ProjectContextLoader` loads project context and `PromptContractRegistry.EmitSnapshotAsync` writes contracts.
4. `RoadmapResumePlanner` captures artifact snapshots, validates incomplete transitions, validates projection safety, validates lifecycle usability, and maps persisted state to a resume action.
5. `ExecuteResumePlanAsync` dispatches to selection.
6. `RunSelectionAndFollowingAsync` calls `SelectNextInitiativeAsync`.
7. `ProjectionCache.EnsureAsync` may generate a projection through an agent, validate it, mutate the manifest, and block on stale or invalid content.
8. `RoadmapPromptContextBuilder.BuildSelectionContextAsync` reads completion context and roadmap sources, renders source references, and rejects raw project-context markers.
9. `RunPromptTransitionWithCompletionAsync` resolves transition inputs, writes state, appends journal start, runs the agent, appends journal completion, writes state completion, and persists failure if needed.
10. The state machine writes selection output, captures HITL requests, writes selection evidence, records selection provenance, and marks lifecycle ready.
11. `SelectionParser` parses table content out of the generated markdown.
12. `AppendDecisionAsync` allocates a decision id and writes the ledger.
13. `ContinueAfterSelectionAsync` branches by raw string outcomes and may launch audit, create, split, terminal pause, or milestone generation.

That is too much reconstruction for one transition.

## Main Findings

### 1. The State Machine Is The Largest Cognitive Bottleneck

`RoadmapStateMachine` is the place a reader expects to understand roadmap execution, but it contains too many responsibilities to be scanned. Its constructor alone is a map of 29 concepts:

- artifacts
- project context
- contracts
- projection manifest
- projection cache
- context builder
- input resolver
- completion policy
- completion router
- completion archive
- prompt runner
- state store
- startup planner
- resume planner
- unblock planner
- selection provenance
- decision ledger
- journal
- lifecycle
- promotion
- bundle extraction
- split interpretation
- split family state
- execution preparation
- invariants
- console
- HITL capture
- non-implementation review

That is not a transition owner. It is a subsystem container.

### 2. Transition Authority Is Distributed Across Registries And Switches

The prompt name `"SelectNextEpic"` appears as an execution key in:

- `ProjectionRegistry`
- `PromptContractRegistry`
- `RoadmapPromptCatalog`
- `TransitionInputResolver`
- `RoadmapStateMachine`
- tests
- prompt filenames

The engineer has to verify that each string-keyed authority agrees. A strongly typed transition definition could make this relationship visible; the current design makes it searchable.

### 3. Generic Helpers Hide Durable Side Effects

`RunPromptTransitionWithCompletionAsync` sounds like an execution helper, but it also owns durable state and journal writes. `ProjectionCache.EnsureAsync` sounds like a cache helper, but it can execute a prompt and persist blocker evidence. `SaveStateAsync` sounds like state persistence, but it also reloads manifest counts, active artifact rows, decision ids, split-family counts, blockers, transition intent, and next transitions.

These names compress too much. The reader cannot infer side effects from method names.

### 4. Persistence Surfaces Are Too Numerous For At-A-Glance Reasoning

A successful selection touches at least:

- projection file
- projection manifest
- prompt contract snapshot
- transition journal
- roadmap state JSON
- selection artifact
- selection evidence
- selection provenance manifest
- artifact lifecycle
- decision ledger
- optional HITL ledger

This is not necessarily wrong, but it is cognitively expensive. The transition does not present one local "write set" that a reader can inspect before modifying it.

### 5. Resume Logic Is A Separate Execution Authority

`RoadmapResumePlanner` is itself a 622-line decision surface. It interprets persisted state, lifecycle state, projection freshness, selection freshness, active-epic validity, and execution preparation. A reader cannot understand what a persisted state does by reading the enum or the state machine; they must also read resume planning and workflow classification.

This is a major "what happens next?" failure.

### 6. Documentation Adds Cognitive Burden Instead Of Reducing It

The docs are extensive and often careful, but they introduce a second reconstruction task: determine which documentation describes the current solution.

Examples:

- `docs/architecture.md` describes React UI, Tauri shell, backend sidecar, endpoint catalogs, and legacy backend subsystems that are not present in `LoopRelay.slnx`.
- `docs/architecture.md` and `docs/orchestration-loop-governance.md` claim `LoopRelay.Agents` references no other `LoopRelay.*` project, while `src/LoopRelay.Agents/LoopRelay.Agents.csproj` references `LoopRelay.Permissions`.
- `docs/prompt-architecture.md` says there are 10 canonical prompts in the described loop, while the current prompt tree contains many more roadmap planning and projection prompts.
- `.agents/semantic/...` reports add a semantic protocol layer that is not the obvious operational entry point for the checked-out CLIs.

This may be historical drift rather than functional risk. For this audit, the point is simpler: the docs force the engineer to classify relevance before they can use them.

### 7. Tests Prove Behavior, But Setup Reveals Cognitive Cost

`StateMachineFactory.Create` in `tests/LoopRelay.Roadmap.Cli.Tests/RoadmapStateMachineSelectionTests.cs` manually assembles the roadmap state machine with the same broad collaborator graph. That test helper is useful, but it exposes how much wiring is required before a transition can run.

Good tests reduce debugging fear. They do not make the transition easy to understand at a glance.

## Positive Clarity Affordances

These reduce some cognition:

- Naming is generally intentional.
- The solution layout separates Agents, CLI, Plan CLI, Roadmap CLI, Completion, Permissions, Projections, Infrastructure, and Core.
- Prompt templates live in a clear prompt catalog.
- Transition journals, state stores, lifecycle stores, and evidence artifacts make runtime state observable.
- `transitions/select-next-epic.md` gives a useful trace of one transition.
- Tests cover many important workflow paths.

These positives prevent a harsher verdict. They do not overcome the hard failures.

## Delta Analysis

Compared against `transitions/select-next-epic.md`:

Improved:

- No concrete cognitive burden was observed as removed in the reviewed implementation.

Unchanged:

- Hidden steps remain: projection freshness, prompt contract lookup, input snapshot capture, state start/completion persistence, journal writes, prompt rendering, prompt-policy append, evidence writes, provenance writes, lifecycle upsert, and decision ledger append.
- `SelectNextEpic` still requires many navigation jumps.
- Selection materialization still writes artifact/evidence/provenance/lifecycle before parse validation.
- Downstream routing remains visually attached to selection through `ContinueAfterSelectionAsync`.
- The suggested handler boundary from the prior transition audit has not been implemented.

Regressed or newly visible at repository scope:

- Repository-level documentation adds more historical and semantic vocabulary than a new reader can immediately classify.
- Architecture docs and project files conflict on at least one stated layering invariant.
- The repository has several orchestration styles with similar agent-turn lifecycles and no immediate "current canonical flow" map.

Delta verdict: unchanged for the audited transition, broader repository cognition burden confirmed.

## Modification Risk

Changing `SelectNextEpic` safely requires considering:

- prompt contracts
- projection registry and prompt catalog alignment
- projection freshness and manifest behavior
- selection context shape
- transition input hash semantics
- state JSON compatibility
- transition journal compatibility
- selection artifact and evidence paths
- selection provenance freshness
- lifecycle state
- HITL capture
- decision ledger schema
- resume safety
- downstream routing strings

This is too much blast radius for one transition.

## Debugging Risk

When a transition fails, the engineer must determine whether the failure came from:

- project context preflight
- projection generation
- projection validation
- stale projection policy
- context building
- transition input resolution
- prompt execution
- prompt output parsing
- artifact write
- lifecycle upsert
- provenance freshness
- decision ledger append
- invariant validation
- completion certification
- unblock recovery

Many of those are legitimate failure domains. The cognition problem is that the executing path does not present them as one local, inspectable failure map.

## Repository Portfolio Assessment

Could this become a model repository demonstrating world-class code organization?

Not in its current shape.

The repository demonstrates ambition, discipline, and serious testing. But a model repository must make central behavior obvious. This repository asks the reader to reconstruct central behavior from a large coordinator, string-keyed registries, prompt templates, evidence systems, and documents that partly describe historical or parallel architectures.

An experienced engineer might respect the rigor. They would not emulate the cognitive shape.

## Highest-Impact Cognitive Repairs

These are recommendations, not changes made by this audit.

1. Give each roadmap transition one local execution owner.

   Do this only if it collapses the read path. A useful `SelectNextEpic` owner would show projection, context, run, write set, parse, decision, and handoff in one sequential method. Avoid extraction that merely moves the same reconstruction into more files.

2. Replace string-routed prompt authority with typed transition definitions.

   Runtime prompt name, projection prompt, required inputs, outputs, parser, writer, allowed decisions, and transition input roles should be inspectable as one transition definition.

3. Split execution from durable persistence naming.

   If a helper writes state and journals, its name should say so. If a cache can run an agent and write blocker evidence, its name should say so.

4. Collapse state meaning into one obvious authority.

   Today, state meaning is spread across enum, startup planner, resume planner, classifier, `NextTransitions`, and router branches. A single transition table would reduce reconstruction.

5. Add a current-architecture entry point and archive or label historical docs.

   The docs need a short "current executable surfaces" map that says which CLIs are current, which docs are historical, and which governance files are active.

6. Publish transition write sets next to transition code.

   For each transition, list durable writes and failure writes locally. This would immediately reduce debugging and modification resistance.

## Final Answer

This implementation forces high unnecessary cognition onto a human engineer.

The main cost is not lack of care. It is that execution is too indirect, too string-keyed, too mixed with persistence and reporting, and too distributed across planners, registries, prompt catalogs, stores, and governance documentation. The repository is understandable with effort, but the stated excellence standard rejects that as sufficient.

Verdict: ❌ Architectural Debt.
