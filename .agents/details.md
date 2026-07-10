# Canonical Orchestration Plan Detail Supplement

## Purpose

This file captures details from `.agents/specs/roadmap.md` and `.agents/specs/s0.md` through `.agents/specs/s9.md` that are meaningful supplements to `.agents/plan.md`.

It is not a replacement for the plan. It is a gap-filling reference for enriching the plan with concrete deliverable shapes, contract metadata, state names, certification questions, and retirement ownership rules.

## Cross-Cutting Details

### Architecture Authority

The existing Roadmap CLI is evidence for behavior, not architectural authority. The canonical workflow contracts, transition runtime, resolver, controller, and product model become authority once introduced and certified.

### Repository Ownership

All workflow state, stage state, transition evidence, blockers, recovery state, products, chain progression, and storage authority must be interpretable from repository-owned evidence. Hidden process memory, global orchestrator state, cached decisions, and CLI call chains must not be required to reconstruct progress.

### Prompt Success Is Not Progress

Every milestone should preserve these distinctions:

- prompt rendered
- prompt executed
- raw output captured
- output interpreted
- output product emitted
- output product validated
- effects applied
- transition completed
- stage completed
- workflow completed
- workflow chain completed

No later state may be inferred solely from prompt completion or artifact existence.

### Freeze Rules

The deep-dive specs repeatedly introduce freeze points. The plan should make clear that later milestones consume earlier models rather than redefining them:

- M1 freezes vocabulary and contracts.
- M2 freezes transition runtime behavior.
- M3 freezes repository resolution.
- M4 freezes workflow chaining and boundary behavior.
- M9 freezes the public orchestration surface.

Any later need for a new foundational concept should explicitly revisit the appropriate earlier milestone.

## Milestone 0 Detail Gaps

### Baseline Deliverable Shape

The plan names baseline inventories and characterization tests, but should also require a baseline certification package containing:

- behavioral contract inventory
- executable characterization coverage
- behavioral invariant inventory
- compatibility inventory
- known-defect inventory
- migration risk inventory
- baseline certification summary

### Behavioral Contract Inventory

The inventory should capture observable behavior for:

- CLI arguments, subcommands, flags, defaults, exit codes, console behavior, logging, and cancellation behavior
- Traditional Roadmap, Plan, Execution, Storage, Completion, and Decision Session workflows
- filesystem persistence, SQLite persistence, fallback selection, migration, export, import, sync, and verification
- resume, restart, rerun, repair, failure, cancellation, blockers, and recovery
- approval, review, decision files, and other human interaction paths

### Characterization Test Philosophy

Tests should verify observable behavior, not implementation structure. The plan should explicitly avoid tests that lock in incidental class names, method boundaries, or internal sequencing unless those are externally observable.

### Behavioral Invariants

The plan should require invariants to be categorized, observable, and testable. Useful categories:

- workflow invariants, such as roadmap pausing at milestone specs, Plan requiring clean outputs, and execution requiring certified completion
- persistence invariants, such as storage authority, lifecycle guarantees, and migration guarantees
- execution invariants, such as `.agents` being excluded from progress, completion certification being required, and milestone semantics
- recovery invariants, such as blockers, evidence, and cancellation being preserved
- CLI invariants, such as exit codes, command semantics, and cancellation behavior

### Compatibility Inventory Classification

Every compatibility behavior should be classified as one of:

- required
- optional
- deprecated
- historical only

The inventory should cover filesystem compatibility, SQLite compatibility, legacy state compatibility, legacy exports, legacy journals, legacy lifecycle, legacy execution states, legacy roadmap state, and decision resume.

### Known-Defect Inventory Fields

Each known issue should record:

- current behavior
- expected behavior
- whether later milestones must preserve the behavior
- whether later milestones may eliminate the behavior

Likely issue areas include archive behavior, idempotency, permissions, storage, retry, completion, execution state, transition journals, SQLite, and Git.

### Migration Risk Inventory Fields

Each migration risk should include:

- risk
- reason
- observable contract
- regression consequence

Risk categories should include workflow resolution, stage resolution, persistence, recovery, completion, storage, prompt execution, decision sessions, publication, Git, and human interaction.

### Baseline Certification Questions

M0 should answer:

- Can future milestones determine whether a behavior changed?
- Can they distinguish intentional improvement from accidental regression?
- Can known defects be separated from new regressions?
- Can the future architecture be validated against observable behavior instead of implementation?

## Milestone 1 Detail Gaps

### Concept Definition Template

Every canonical concept should define:

- purpose
- responsibilities
- boundaries
- relationships
- lifecycle
- ownership

Concept definitions should explicitly avoid implementation, class names, interfaces, inheritance, storage, serialization, and prompt implementation.

### Vocabulary Coverage

The plan lists model types, but should ensure the vocabulary covers:

- Workflow
- Workflow Chain
- Workflow Identity
- Workflow Definition
- Stage
- Stage Identity
- Transition
- Transition Dependency
- Transition Identity
- Transition Result
- Product
- Product Identity
- Product Ownership
- Product Authority
- Input Gate
- Output Gate
- Workflow Entry Gate
- Workflow Exit Gate
- Blocker
- Recovery
- Workflow State
- Stage State
- Workflow Outcome
- Workflow Eligibility
- Stage Eligibility

### Workflow Contract Exclusions

Workflow definitions should declare identity, purpose, entry products, entry gate, stages, workflow dependencies, workflow completion gate, exit products, outcome vocabulary, recovery behavior, and blocker behavior. They should not embed prompt implementation, persistence, execution order, or CLI behavior.

### Stage Contract Metadata

Each stage should declare:

- identity
- purpose
- required products
- produced products
- dependencies
- allowed successors
- completion conditions
- failure conditions
- entry gate
- completion gate
- terminal outcomes

Stages should describe coherent domain phases, not rendering, loading, persisting, publication, or other mechanics.

### Transition Contract Metadata

Each transition should declare:

- identity
- purpose
- required input products
- input gate
- prompt identity
- execution posture
- produced products
- output gate
- validators
- effects
- transition result vocabulary
- successor dependencies
- recovery metadata

Transition definitions should exclude prompt content, prompt rendering, and persistence implementation.

### Product Contract Metadata

Product records should include:

- identity
- producer
- consumer
- ownership
- authority
- lifecycle
- validation status
- freshness
- dependencies
- version or causal identity
- compatibility representations

Representative products include Prepared Epic, Milestone Specification Set, Executable Plan, Operational Context, Execution Details, Milestone Set, Decision Set, Execution Handoff, Completion Evaluation, Certified Completion, and Roadmap Completion Context.

### Gate Contract Metadata

Every gate should declare:

- requirements
- authority
- validation logic
- failure semantics
- supporting evidence
- missing requirements
- blocking requirements
- ambiguity

Canonical gate outcomes are `Satisfied`, `Unsatisfied`, `Blocked`, `Waiting`, `Invalid`, and `Ambiguous`.

### Dependency Contract Metadata

Dependencies may be transition, stage, workflow, or product dependencies. Classifications are:

- required
- optional
- advisory
- freshness-sensitive
- invalidating

Each dependency should record producer, consumer, dependency type, dependency strength, and invalidation rules.

### Effect Contract Metadata

Effects are repository mutations performed after a validated transition result. Categories include:

- product persistence
- lifecycle updates
- evidence
- decision recording
- publication
- Git
- archives
- recovery bookkeeping
- compatibility exports

Each effect should declare identity, trigger, inputs, outputs, ordering, and failure semantics.

### Relationship Model

The plan should include the canonical relationship:

```text
Workflow Chain
  -> Workflow
  -> Stage
  -> Transition
  -> Prompt
  -> Transition Result
  -> Output Gate
  -> Effects
  -> Products
  -> Workflow Exit Gate
```

Dependency flow:

```text
Workflow owns Stages
Stages own Transitions
Transitions produce Products
Products satisfy Input Gates
Input Gates enable Transitions
Workflow Exit Gates enable Workflow Chains
```

## Milestone 2 Detail Gaps

### Runtime Boundary

M2 is about executing one transition, not workflows. It should not migrate workflows, implement chaining, unify CLI, implement automatic workflow selection, implement stage resolution, redesign prompts, or change repository semantics.

### Input Resolution Fields

Input resolution should locate required products and determine:

- identity
- authority
- freshness
- usability
- validation state
- compatibility representation
- evidence
- causal or hash identity

Failure modes should include missing, blocked, invalid, ambiguous, stale, and unsupported.

### Input Gate Checks

The input gate should check required products, authority, freshness, lifecycle, compatibility, and dependencies. It must return a structured gate result, not a boolean.

### Prompt Context Versus Prompt Rendering

Prompt context construction should resolve projections, project context, products, metadata, and compatibility inputs. It should not render the prompt.

Prompt rendering should load prompt content, inject resolved inputs, inject projections, inject metadata, and produce an immutable rendered prompt. It should not persist state, validate outputs, apply effects, or advance lifecycle.

### Prompt Execution Metadata

Prompt execution returns raw result and metadata only. Metadata should include:

- execution duration
- cancellation state
- runtime diagnostics
- session metadata
- prompt identity
- execution posture

Supported postures include one-shot, persistent session, warm session, scoped operation, read-only, decision session, and elevated or permission-aware execution where applicable.

### Output Interpretation Categories

The interpreter should classify output as:

- valid
- malformed
- incomplete
- unexpected
- blocked

It should produce a structured transition result before any effects are applied.

### Output Gate Checks

The output gate should verify that products exist, validate, are authoritative, are fresh, are complete, and satisfy dependencies. Prompt success must not equal transition success.

### Effect Execution Requirements

Effects execute after output gate satisfaction. They must be ordered, explicit, observable, recoverable where required, and able to persist partial failure evidence.

Effect categories include persistence, lifecycle, evidence, decision recording, publication, Git, archive, compatibility, telemetry if present, and recovery bookkeeping.

### Transition Persistence Metadata

Transition persistence should record:

- transition identity
- workflow
- stage
- products
- evidence
- gate results
- execution metadata
- recovery metadata

States should distinguish not started, started, prompt completed, output interpreted, output validated, effects partially applied, effects applied, completed, blocked, failed, and cancelled.

### Successor Resolution Boundary

The transition runtime returns eligible successor candidates. It does not choose which successor runs next; workflow orchestration chooses later.

### Recovery Model

Recovery should classify interrupted transitions as restart, resume, repair, rerun, or unsupported. Recovery state must be workflow-agnostic and based on transition evidence.

### Runtime Evidence Contents

Runtime evidence should be sufficient to explain:

- transition identity
- workflow
- stage
- inputs
- products
- prompt identity and rendered prompt evidence
- execution metadata
- validation results
- effects
- persistence
- recovery state
- successor eligibility

### Runtime Validation

M2 tests should stress missing inputs, invalid inputs, blocked gates, malformed prompt output, partial output, invalid products, effect failures, cancellation, persistence failure, and unsupported recovery.

## Milestone 3 Detail Gaps

### Resolution Boundary

M3 determines what can execute and why. It should not execute workflows, chain workflows, migrate workflows, redesign persistence, redesign prompts, or redesign recovery.

### Repository Observation Categories

Repository observation should produce an immutable snapshot with no interpretation. It should observe:

- storage
- workflow artifacts
- lifecycle
- evidence
- journals
- Git
- prompt contracts
- projection manifests
- completion artifacts
- decision state
- operational context
- repository metadata
- environment

### Storage Verification Result

Storage verification should include authority, usable authority, confidence qualifier, blocking conditions, observed conflicts, stale exports, corruption, unsupported schema, unresolved references, and partial workflow transactions.

Verification is automatic and read-only. Repair is never automatic.

### Workflow Identity Resolution Output

Workflow identity resolution should produce identity, evidence, authority, and reasoning. Explicit CLI mode overrides automatic detection; otherwise `.agents/evals/*.md` selects EvalRoadmap and absence selects TraditionalRoadmap. Single-workflow invocations retain explicit identity.

### Eligibility Is Not Selection

Workflow selection determines which workflow is under consideration. Workflow eligibility independently determines whether that workflow may execute. Eligibility states are `Eligible`, `Blocked`, `Waiting`, `Completed`, `Cancelled`, `Failed`, `Invalid`, and `Ambiguous`.

### Workflow State Resolution

Workflow state resolution should reconstruct current workflow state, completed stages, incomplete stages, blocked stages, recovery state, and workflow outcome from repository evidence.

### Transition Eligibility Output

Transition eligibility should output eligible transitions, blocked transitions, waiting transitions, and invalid transitions. It should not choose a transition.

### Blocker Model

Blocker categories include storage, workflow, stage, transition, validation, human, permission, recovery, and repository. Every blocker should include evidence, authority, required action, and recovery possibility.

### Ambiguity Model

Ambiguity categories include workflow ambiguity, stage ambiguity, authority ambiguity, repository ambiguity, storage ambiguity, recovery ambiguity, and completion ambiguity. Ambiguity must never silently resolve.

### Explainability Model

Every resolution decision should record decision, evidence, authority, supporting facts, ignored facts, conflicting facts, confidence qualifier, and remaining uncertainty.

### Repository Classification

M3 should produce a canonical repository classification independent of workflow implementation:

- Fresh
- In Progress
- Blocked
- Waiting
- Completed
- Cancelled
- Failed
- Ambiguous
- Corrupt
- Unsupported

### Human Interaction Requirement

Resolution should explicitly represent required human interaction with reason, authority, and blocking scope. Categories include approval, review, roadmap revision, strategic investigation, permission, evidence repair, and completion decisions.

## Milestone 4 Detail Gaps

### Workflow Chain Metadata

Each workflow chain should declare identity, entry workflow, workflow order, termination workflow, and future extension points. Execution closure loop, parallel branches, and dynamic workflow graphs remain excluded.

### Workflow Boundary Contract

Workflow boundaries validate upstream completion, validate downstream readiness, transfer semantic products, and determine downstream eligibility.

Boundary inputs:

- workflow outputs
- validated products
- workflow completion evidence
- repository observation

Boundary outputs:

- workflow completion
- downstream eligibility
- blocking information
- boundary evidence

Boundaries must depend on semantic products, not individual files.

### Workflow Entry Gate Responsibilities

Entry gates validate required products, authority, freshness, ownership, lifecycle, repository validity, and storage authority. Outcomes are `Satisfied`, `Blocked`, `Waiting`, `Invalid`, and `Ambiguous`.

### Workflow Exit Gate Responsibilities

Exit gates validate completed stages, required products, workflow guarantees, required downstream products, completion evidence, and outstanding blockers.

Workflow completion does not advance execution directly; it makes the downstream workflow eligible.

### Product Transfer Details

Product transfer determines products produced, products required, compatibility representations, authority, freshness, and ownership. Filesystem, SQLite, legacy exports, and current runtime records are compatibility details, not workflow contract boundaries.

### Chain Progression Algorithm

The controller progression should follow:

```text
Workflow completes
  -> workflow exit gate
  -> downstream workflow identified
  -> downstream entry gate evaluated
  -> if eligible, advance
  -> otherwise stop with explanation
```

Stopping conditions include blocked, waiting, cancelled, failed, completed, bounded invocation, and ambiguous.

### Workflow Controller Boundary

The controller resolves workflow, resolves stage, executes transitions through the runtime, monitors completion, advances workflows, and terminates the chain.

The controller must not own prompt execution, persistence, validation, rendering, or effects.

### Workflow Explainability

Every workflow decision should explain why a workflow was selected, why a stage was selected, why downstream execution is eligible, why progression stopped, why chaining occurred, and why chaining did not occur.

### Future Compatibility Constraint

M4 supports serial execution, linear chains, and one active workflow now. It should not prevent dependency graphs, multiple eligible transitions, concurrent Eval branches, join stages, execution loop-back, confidence, or telemetry later.

## Milestone 5 Detail Gaps

### TraditionalRoadmap Contract

Workflow identity: `TraditionalRoadmap`.

Purpose: transform strategic roadmap information into a validated implementation-ready roadmap product.

Consumes:

- roadmap context
- roadmap sources
- project context

Produces:

- prepared epic
- milestone specification set

Entry gate validates repository, storage authority, roadmap prerequisites, project context, and required products.

Exit gate validates prepared epic, validated milestone specification set, and downstream Plan contract satisfaction.

### Stage Purposes

Roadmap Context establishes strategic context required for roadmap decisions.

Strategic Initiative Selection determines the next implementation candidate.

Epic Preparation prepares the selected initiative into a validated implementation epic and includes audit, create, split, realign, reimagine, and retire transitions.

Milestone Specification produces implementation-ready milestone specifications.

Workflow Completion verifies the Plan entry contract.

### Transition Responsibility Reduction

Roadmap transition definitions should contain only identity, purpose, required products, prompt identity, validators, and effects. Prompt rendering, execution, persistence, journals, evidence, lifecycle, and state progression move to the runtime.

### Roadmap Product Set

Required semantic products include:

- Roadmap Context
- Strategic Initiative
- Prepared Epic
- Milestone Specification Set
- Roadmap Completion Context

Existing files continue to exist as serialization, but products are authoritative.

### Legacy State Handling

Legacy persisted states remain readable and must preserve resume capability where supported. They no longer define active orchestration and should not dictate the new model.

### TraditionalRoadmap Resolution

The resolver must determine TraditionalRoadmap stage, eligibility, blockers, waiting states, and completion. For default invocation, TraditionalRoadmap is selected only when `.agents/evals/*.md` is absent.

### M5 Validation Cases

Validation should cover fresh repository, existing roadmap, resume, blocked roadmap, cancelled roadmap, failed roadmap, stale projections, invalid projections, split, rewrite, create, retire, audit, milestone generation, and workflow completion.

### Certification Questions

TraditionalRoadmap certification should answer whether it executes through the runtime, whether transitions are declarative, whether stages are domain-oriented, whether products are canonical, whether workflow boundaries are explicit, whether explainability is preserved, whether recovery is preserved, and whether behavior is preserved.

## Milestone 6 Detail Gaps

### Plan Contract

Workflow identity: `Plan`.

Purpose: transform a prepared implementation epic into an execution-ready operational product set.

Consumes:

- Prepared Epic
- Milestone Specification Set

Produces:

- Executable Plan
- Operational Context
- Execution Details
- Execution Milestone Set
- Execution Readiness

Entry gate validates Prepared Epic, Milestone Specification Set, storage authority, repository readiness, and ownership.

Exit gate validates Executable Plan, Operational Context, Execution Details, Execution Milestone Set, and Execute contract satisfaction.

### Plan Stage Purposes

Planning creates the executable implementation plan.

Plan Validation critically evaluates and refines the plan, including adversarial projection, adversarial review, and plan refinement.

Execution Preparation materializes execution products, including operational context, details, and milestones.

Workflow Completion verifies the Execute entry contract.

### Warm Session Contract

The Write Executable Plan and Revise Plan transitions must preserve the held-open planning relationship:

```text
Write Executable Plan -> Revise Plan
```

Warm sessions are an execution posture owned by the runtime. The runtime owns session creation, reuse, closure, cancellation, recovery, and diagnostics. The workflow should not manage session lifecycle.

### Scoped Operation Contract

Operational Context, Details, Milestones, and Details refinement become ordinary transitions. The runtime owns permissions, allowed reads, allowed writes, rollback, evidence, validation, and publication. The workflow declares required products, expected products, and validators.

### Execute Entry Contract Certification

The Plan workflow must produce exactly one validated execution product set, regardless of which roadmap workflow produced the upstream product. At minimum:

- Executable Plan
- Operational Context
- Execution Details
- Execution Milestone Set
- Execution Readiness

Each product must be validated, authoritative, owned by the current repository, owned by the current workflow chain, fresh enough, and ownership-valid.

Execute must never infer readiness from artifact existence alone.

### Partial Plan Semantics

Plan state should distinguish:

- never started
- planning in progress
- plan authored
- validation in progress
- validation complete
- execution preparation in progress
- partially materialized execution products
- execution-ready
- blocked
- cancelled
- failed
- completed

This replaces the current behavior where existing outputs merely block a fresh run.

### Plan Validation Cases

Validation should cover fresh Plan, blocked Plan, resume, cancellation, failure, warm session, adversarial review, plan revision, operational context, details, milestones, execution contract, and publication.

## Milestone 7 Detail Gaps

### Execute Contract

Workflow identity: `Execute`.

Purpose: transform an execution-ready operational product set into a certified completed implementation.

Consumes:

- Execution Readiness
- Executable Plan
- Operational Context
- Execution Details
- Execution Milestone Set

Produces:

- Repository Changes
- Decision History
- Execution Handoff
- Operational Delta
- Completion Evidence
- Certified Completion

Entry gate validates Execution Readiness, storage authority, repository state, workflow ownership, and required products.

Exit gate validates Certified Completion, completion evidence, repository consistency, and closure requirements.

### Execute Stage Purposes

Execution Readiness determines whether implementation may begin or continue.

Implementation Planning determines the next implementation slice and includes decision proposal, decision transfer, and decision continuation.

Implementation executes the planned implementation slice.

Execution Continuity prepares the repository for the next execution iteration and includes handoff generation, operational context updates, decision retirement, publication, and commit evaluation.

Completion determines whether execution should continue or close and includes milestone completion, non-implementation review, completion certification, and completion routing.

Workflow Completion verifies the workflow exit contract.

### Canonical Iteration Model

Execute is not linear. Its loop is:

```text
Execution Readiness
  -> Planning
  -> Implementation
  -> Continuity
  -> Completion
  -> Continue to Execution Readiness or attempt Workflow Completion
```

Completion outcomes are:

- Continue
- Blocked
- Waiting
- Cancelled
- Failed
- Certified Complete

Iteration belongs to workflow progression, not the transition runtime.

### Decision Session Contract

Decision sessions become an execution posture. The runtime owns session lifecycle, resume, transfer, persistence, diagnostics, and recovery. The workflow declares decision transitions, decision products, and decision validators.

### Execution Product Set

Canonical execution products include:

- Execution Readiness
- Implementation Slice
- Decision Set
- Execution Handoff
- Operational Context
- Operational Delta
- Repository Changes
- Completion Evidence
- Certified Completion

Filesystem, SQLite, history, and live artifacts are serialization or compatibility forms.

### Completion Components

Completion becomes one explicit stage with:

- milestone evaluation
- completion review
- completion certification
- completion routing
- workflow completion

Execute becomes the single producer of completion claim, completion review, completion certification, completion route, and Certified Completion.

### Execute State

Execute state should include workflow, current stage, current transition, iteration count, completed stages, products, blockers, recovery, and outcome.

It must explicitly represent ready, planning, executing, continuity, completion, completed, blocked, cancelled, failed, and waiting.

### Repository Progress Semantics

Repository progress is evaluated from repository state and must preserve rigor around real implementation changes, milestone advancement, decision progression, publication, and completion evidence. Progress must never be inferred solely from prompt completion.

### Execute Validation Cases

Validation should cover fresh execution, resume, decision continuation, decision transfer, execution slice, repository changes, no-change iteration, stall, cancellation, failure, handoff, operational context, publication, completion review, completion certification, and workflow completion.

## Milestone 8 Detail Gaps

### EvalRoadmap Contract

Workflow identity: `EvalRoadmap`.

Purpose: transform evaluation intent into a prepared implementation roadmap.

Consumes:

- Evaluation Intent
- Project Context
- Repository Context

Produces:

- Prepared Epic
- Milestone Specification Set

Entry gate validates evaluation intent, repository readiness, project context, and storage authority.

Exit gate validates Prepared Epic, Milestone Specification Set, and Plan entry contract.

### Eval Stage Purposes

Evaluation Foundation establishes evaluation scope.

Dependency Analysis discovers and validates evaluation dependencies.

Hypothesis Development generates falsifiable architectural hypotheses.

Architectural Organization transforms hypotheses into architectural structure.

Roadmap Formation produces an implementation roadmap.

Epic Preparation produces the prepared implementation epic.

Milestone Specification generates implementation-ready milestone specifications.

Workflow Completion verifies the Plan entry contract.

### Evaluation Products

Semantic products:

- Evaluation Intent
- Dependency Inventory
- Hypothesis Inventory
- Architectural Catalog
- Dependency Graph
- Epic Roadmap
- Prepared Epic
- Milestone Specification Set

Each product should record identity, producer, authority, freshness, validation, dependencies, and lifecycle.

### Dependency Ordering

Current ordering:

```text
Evaluation Intent
  -> Dependency Inventory
  -> Hypothesis Inventory
  -> Architectural Catalog
  -> Dependency Graph
  -> Epic Roadmap
  -> Prepared Epic
  -> Milestone Specifications
```

Dependencies determine transition eligibility, not execution strategy. Execution remains serial now; future concurrent branches, join transitions, and deterministic merges should not require runtime redesign.

### Eval Workflow Resolution

Default detection selects EvalRoadmap when `.agents/evals/*.md` exists. `--eval` always selects EvalRoadmap in chained mode. `looprelay eval` runs only EvalRoadmap.

Eligibility states include eligible, blocked, waiting, cancelled, failed, completed, and ambiguous. Every EvalRoadmap decision must be explainable.

### Downstream Convergence

Both roadmap workflows terminate with:

```text
Prepared Epic
  -> Milestone Specification Set
  -> Plan Entry Gate
```

Plan, Execute, and future workflows must remain unaware of which roadmap workflow produced those products.

### Legacy Independence

EvalRoadmap must not be a mode inside TraditionalRoadmap. It should have no inherited orchestration, no copied state machine, no special-case runtime, and no conditional orchestration after workflow selection.

Shared pieces are runtime, controller, products, and contracts. Independent pieces are stages, transitions, prompts, products, and dependencies.

### Eval Validation Cases

Validation should cover fresh evaluation, resume, dependency generation, hypothesis generation, architecture generation, DAG generation, roadmap generation, epic generation, milestone generation, workflow completion, and Plan eligibility.

## Milestone 9 Detail Gaps

### Unified CLI Invocation Behavior

Primary invocation:

```text
looprelay
```

Performs storage verification, repository observation, workflow resolution, stage resolution, current workflow execution, and auto-chaining through downstream workflows.

Explicit chained modes:

```text
looprelay --traditional
looprelay --eval
```

Bounded modes:

```text
looprelay traditional
looprelay eval
looprelay plan
looprelay execute
```

### Repository Resolution Stack

Every invocation should perform the same stack:

```text
Storage verification
  -> Repository observation
  -> Workflow identity resolution
  -> Workflow eligibility
  -> Stage resolution
  -> Transition eligibility
```

There should be no duplicated discovery, storage validation, or stage selection in compatibility entry points.

### Unified User Experience Fields

The unified CLI should be able to report:

- current workflow
- current stage
- current transition
- current repository classification
- workflow chain
- blockers
- human interaction
- storage authority
- current products
- next eligible transition
- next eligible workflow

Every decision should identify evidence, authority, ignored evidence, conflicts, and uncertainty.

### Compatibility Layer Responsibilities

Existing Roadmap, Plan, and Execute entry points should become thin adapters. Their responsibilities are argument translation, legacy compatibility, exit-code compatibility, and migration assistance.

Compatibility layers must not contain orchestration logic and must not compete as authorities.

### Legacy Retirement Scope

Retire duplicate ownership of:

- Roadmap orchestration
- Plan pipeline orchestration
- Execution loop orchestration
- workflow progression
- prompt orchestration
- completion orchestration
- workflow discovery
- stage discovery
- transition sequencing

Preserve domain prompts, products, recovery, evidence, and behavior.

### Storage Integration

Automatic behavior:

- storage verification
- repository authority determination

Manual behavior:

- import
- export
- sync
- repair
- conflict resolution

Verification must never silently mutate repository state, and storage authority must always be visible.

### Certification Breakdown

M9 should separately certify:

- behavioral equivalence across TraditionalRoadmap, EvalRoadmap, Plan, Execute, Completion, Storage, Recovery, and CLI
- legacy compatibility across filesystem repositories, SQLite repositories, mixed repositories, legacy exports, legacy state, legacy journals, legacy lifecycle, legacy execution state, legacy roadmap state, decision resume, and completion archives
- orchestration architecture consistency

### Orchestration Certification Questions

M9 should answer:

- Is there exactly one orchestration authority?
- Is there exactly one workflow controller?
- Is there exactly one repository resolution engine?
- Is there exactly one transition runtime?
- Is there exactly one completion authority?
- Is there exactly one workflow chaining model?
- Does every workflow execute through identical orchestration?
- Can every orchestration decision be explained?
- Can every workflow resume correctly?
- Can every workflow chain automatically?

### Public Surface Freeze

M9 should freeze:

- CLI behavior
- workflow identities
- workflow chains
- workflow entry contracts
- workflow exit contracts
- repository resolution
- storage verification
- workflow progression
- workflow outcomes

Future evolution should extend the architecture, not redefine the public orchestration contract.

### Final Validation Matrix Additions

End-to-end validation should explicitly cover:

- `looprelay` with a Traditional repository
- `looprelay` with an Eval repository
- `looprelay --traditional`
- `looprelay --eval`
- `looprelay traditional`
- `looprelay eval`
- `looprelay plan`
- `looprelay execute`

For each, validate storage verification, workflow resolution, stage resolution, transition execution, workflow chaining, repository progression, completion, cancellation, recovery, legacy compatibility, and explainability.

### Legacy Retirement Matrix

Every retired responsibility should have exactly one new owner:

| Legacy Responsibility | New Owner |
|---|---|
| Roadmap state-machine orchestration | Workflow Controller + Transition Runtime |
| Plan pipeline sequencing | Workflow Controller + Transition Runtime |
| Execution loop orchestration | Workflow Controller + Transition Runtime |
| Prompt execution ownership | Transition Runtime |
| Stage progression | Workflow Controller |
| Workflow progression | Workflow Controller |
| Repository discovery | Repository Resolver |
| Workflow discovery | Repository Resolver |
| Stage discovery | Repository Resolver |
| Storage authority | Repository Resolver |
| Completion orchestration | Execute Workflow + Transition Runtime |
| CLI orchestration | Unified CLI |

### Architecture Closure Deliverables

The final closure package should contain:

- Canonical Runtime
- Repository Resolver
- Workflow Controller
- Workflow Contracts
- Workflow Definitions
- Unified CLI
- Behavioral Certification
- Compatibility Certification
- Architecture Certification

Final state:

- one Runtime
- one Resolver
- one Controller
- four Workflows
- two Workflow Chains
- one CLI
- one Orchestration Authority

