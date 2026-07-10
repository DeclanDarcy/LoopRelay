# Milestone 7: Execute Migration

Objective: migrate the implementation loop into a first-class iterative Execute workflow.

## Work

- [ ] Add `ExecuteWorkflowDefinition` under `src/LoopRelay.Cli/Services/Workflows`.
- [ ] Define stages:
  - [ ] Execution Readiness.
  - [ ] Implementation Planning.
  - [ ] Implementation.
  - [ ] Execution Continuity.
  - [ ] Completion.
  - [ ] Workflow Completion.
- [ ] Define transitions:
  - [ ] Verify Execution Readiness.
  - [ ] Generate Decision.
  - [ ] Transfer Decision Session.
  - [ ] Continue Decision Session.
  - [ ] Execute Implementation Slice.
  - [ ] Generate Handoff.
  - [ ] Update Operational Context.
  - [ ] Publish Repository State.
  - [ ] Evaluate Commit.
  - [ ] Evaluate Milestone Completion.
  - [ ] Run Non-Implementation Review.
  - [ ] Run Completion Certification.
  - [ ] Interpret Completion Route.
  - [ ] Verify Workflow Exit Gate.
- [ ] Model iteration explicitly:
  - [ ] Readiness.
  - [ ] Planning.
  - [ ] Implementation.
  - [ ] Continuity.
  - [ ] Completion.
  - [ ] Continue to readiness or close.
- [ ] Adapt current components:
  - [ ] `MilestoneGate` becomes readiness/completion gate support.
  - [ ] `DecisionSession` becomes decision-session execution posture.
  - [ ] `ExecutionStep` becomes implementation-slice transition execution.
  - [ ] `LoopArtifacts` rotation methods become effects with evidence.
  - [ ] `AgentsSubmodulePublisher` becomes publish effect.
  - [ ] `CommitGate` becomes commit evaluation effect/gate support.
  - [ ] Non-implementation post-execution review becomes a transition.
  - [ ] Non-implementation completion review and completion certification become the canonical completion stage.
- [ ] Establish a durable closed-state marker:
  - [ ] `CertifiedCompletion` product.
  - [ ] Completed Execute workflow state.
  - [ ] Archive record.
  - [ ] Completion evidence.
  - [ ] Product references that remain resolvable after live Plan/milestone artifacts are archived.
- [ ] Make completion authority singular.
- [ ] Ensure Execute is the only active orchestration path that may own completion closure; old execution entry points are retired rather than delegated.
- [ ] Add recovery for interruption during:
  - [ ] Completion review.
  - [ ] Completion evaluation.
  - [ ] Archive materialization.
  - [ ] Archive synthesis.
  - [ ] Roadmap completion-context update.
  - [ ] Final closed-state persistence.
- [ ] Preserve stall semantics with durable evidence.

## Detail Requirements

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

Filesystem, SQLite, history, and live artifacts are serialization or migration forms.

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

## Acceptance

- [ ] Execute runs through the canonical runtime and controller.
- [ ] Execution stage resolves correctly after process restart.
- [ ] Already-closed execution is idempotently discoverable.
- [ ] Completion closure is singular and durable.
- [ ] Current execution tests pass or are intentionally updated to assert canonical state.
