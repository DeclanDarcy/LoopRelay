# Milestone 1: Canonical Contracts and Vocabulary

Objective: add implementation-neutral contracts that can describe every workflow without changing execution.

Production behavior change allowed: none.

## Work

- [ ] Add contract models under `src/LoopRelay.Orchestration.Primitives/Workflows`:
  - [ ] `WorkflowIdentity`
  - [ ] `WorkflowChainDefinition`
  - [ ] `WorkflowDefinition`
  - [ ] `WorkflowStageDefinition`
  - [ ] `WorkflowTransitionDefinition`
  - [ ] `TransitionDependency`
  - [ ] `ProductDefinition`
  - [ ] `ProductIdentity`
  - [ ] `ProductRequirement`
  - [ ] `GateDefinition`
  - [ ] `GateResult`
  - [ ] `GateRequirementResult`
  - [ ] `WorkflowOutcome`
  - [ ] `StageOutcome`
  - [ ] `TransitionOutcome`
  - [ ] `ExecutionPosture`
  - [ ] `EffectDefinition`
  - [ ] `BlockerDefinition`
  - [ ] `RecoveryDefinition`
- [ ] Model gate outcomes as structured results:
  - [ ] `Satisfied`
  - [ ] `Unsatisfied`
  - [ ] `Blocked`
  - [ ] `Waiting`
  - [ ] `Invalid`
  - [ ] `Ambiguous`
- [ ] Model runtime outcomes as structured results:
  - [ ] `Completed`
  - [ ] `Paused`
  - [ ] `Blocked`
  - [ ] `Failed`
  - [ ] `Cancelled`
  - [ ] `Waiting`
  - [ ] `Stalled`
  - [ ] `Ambiguous`
- [ ] Model dependencies as required, optional, advisory, freshness-sensitive, or invalidating.
- [ ] Add workflow definition validation that checks:
  - [ ] Identity is explicit.
  - [ ] Stages reference known transitions.
  - [ ] Transition dependencies reference known products or transitions.
  - [ ] Products have producer/consumer metadata.
  - [ ] Entry and exit gates have explainable requirements.
  - [ ] Workflow definitions do not embed CLI or persistence implementation details.
- [ ] Add non-wired definition sketches for all four workflows to prove the contracts fit the domain.
- [ ] Ensure the definition sketches do not drive production execution yet.

## Detail Requirements

### Freeze Scope

M1 freezes canonical vocabulary and workflow contract semantics for downstream milestones. Later milestones consume these concepts rather than redefining workflow, stage, transition, product, gate, dependency, effect, blocker, recovery, or outcome vocabulary.

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

The vocabulary must cover:

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
- storage representations

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
- pre-unification exports

Each effect should declare identity, trigger, inputs, outputs, ordering, and failure semantics.

### Relationship Model

The canonical relationship is:

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

Dependency flow is:

```text
Workflow owns Stages
Stages own Transitions
Transitions produce Products
Products satisfy Input Gates
Input Gates enable Transitions
Workflow Exit Gates enable Workflow Chains
```

## Acceptance

- [ ] New tests in `tests/LoopRelay.Orchestration.Primitives.Tests` validate contract invariants.
- [ ] All four workflow definitions can be represented through the same contract types.
- [ ] Existing CLI and workflow tests still pass.
