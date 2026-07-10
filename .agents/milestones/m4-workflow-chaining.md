# Milestone 4: Workflow Chaining and Unified Gates

Objective: compose workflows through the same product/gate mechanism used by transitions.

## Work

- [ ] Add chain definitions:
  - [ ] `TraditionalRoadmapChain`: `TraditionalRoadmap -> Plan -> Execute`
  - [ ] `EvalRoadmapChain`: `EvalRoadmap -> Plan -> Execute`
- [ ] Add workflow boundary services:
  - [ ] `WorkflowEntryGateEvaluator`
  - [ ] `WorkflowExitGateEvaluator`
  - [ ] `ProductTransferEvaluator`
  - [ ] `WorkflowBoundaryEvidenceWriter`
- [ ] Add `WorkflowController` and `WorkflowChainRunner`.
- [ ] Ensure the controller owns workflow selection, stage selection, transition selection among eligible transitions, workflow completion checks, downstream eligibility, bounded stop conditions, and terminal outcome mapping.
- [ ] Ensure the controller does not render prompts, execute prompts, validate products, apply effects, or write transition persistence directly. Those remain runtime responsibilities.
- [ ] Add stopping conditions:
  - [ ] Chain completed.
  - [ ] Bounded workflow completed.
  - [ ] Blocked.
  - [ ] Waiting.
  - [ ] Cancelled.
  - [ ] Failed.
  - [ ] Stalled.
  - [ ] Ambiguous.
  - [ ] No eligible transition.
- [ ] Add explainability for why chaining occurred or stopped.
- [ ] Use fake workflow definitions and compatibility adapters for tests.
- [ ] Do not migrate production workflows yet.

## Detail Requirements

### Freeze Scope

M4 freezes workflow chaining and workflow boundary behavior. Later milestones consume this model rather than redefining chain metadata, entry gates, exit gates, product transfer, controller ownership, chain progression, stop conditions, or workflow-level explainability.

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

## Acceptance

- [ ] Chain progression tests prove workflow boundaries use validated products, not files.
- [ ] Bounded commands stop after one workflow.
- [ ] Default, forced eval, and forced traditional modes select the correct chain.
- [ ] No production workflow has been migrated yet.
