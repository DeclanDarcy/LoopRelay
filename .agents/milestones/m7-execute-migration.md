# Milestone 7: Execute Migration

Objective: migrate the implementation loop into a first-class iterative Execute workflow.

## Work

- [x] Add `ExecuteWorkflowDefinition` under `src/LoopRelay.Cli/Services/Workflows`.
- [x] Define stages:
  - [x] Execution Readiness.
  - [x] Implementation Planning.
  - [x] Implementation.
  - [x] Execution Continuity.
  - [x] Completion.
  - [x] Workflow Completion.
- [x] Define transitions:
  - [x] Verify Execution Readiness.
  - [x] Generate Decision.
  - [x] Transfer Decision Session.
  - [x] Continue Decision Session.
  - [x] Execute Implementation Slice.
  - [x] Generate Handoff.
  - [x] Update Operational Context.
  - [x] Publish Repository State.
  - [x] Evaluate Commit.
  - [x] Evaluate Milestone Completion.
  - [x] Run Non-Implementation Review.
  - [x] Run Completion Certification.
  - [x] Interpret Completion Route.
  - [x] Verify Workflow Exit Gate.
- [x] Model iteration explicitly:
  - [x] Readiness.
  - [x] Planning.
  - [x] Implementation.
  - [x] Continuity.
  - [x] Completion.
  - [x] Continue to readiness or close.
- [x] Adapt current components:
  - [x] `MilestoneGate` becomes readiness/completion gate support.
  - [x] `DecisionSession` becomes decision-session execution posture.
  - [x] `ExecutionStep` becomes implementation-slice transition execution.
  - [x] `LoopArtifacts` rotation methods become effects with evidence.
  - [x] `AgentsSubmodulePublisher` becomes publish effect.
  - [x] `CommitGate` becomes commit evaluation effect/gate support.
  - [x] Non-implementation post-execution review becomes a transition.
  - [x] Non-implementation completion review and completion certification become the canonical completion stage.
    - [x] Non-implementation completion review becomes a canonical completion-stage transition.
    - [x] Completion certification becomes a canonical completion-stage transition.
- [x] Generated Execute prompt templates render through the unified runtime prompt renderer with source-hash evidence where generated prompt assets exist.
- [x] Establish a durable closed-state marker:
  - [x] `CertifiedCompletion` product.
  - [x] Completed Execute workflow state.
  - [x] Archive record.
  - [x] Completion evidence.
  - [x] Product references that remain resolvable after live Plan/milestone artifacts are archived.
  - [x] `VerifyWorkflowExitGate` closes Execute through the canonical runtime from canonical completion evidence and completion route products.
- [x] Make completion authority singular.
- [x] Ensure Execute is the only active orchestration path that may own completion closure; old execution entry points are retired rather than delegated.
- [x] Add recovery for interruption during:
  - [x] Completion review.
  - [x] Completion evaluation.
  - [x] Archive materialization.
  - [x] Archive synthesis.
  - [x] Roadmap completion-context update.
  - [x] Final closed-state persistence.
- [x] Preserve stall semantics with durable evidence.

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

- [x] Execute runs through the canonical runtime and controller.
- [x] Execution stage resolves correctly after process restart.
- [x] Already-closed execution is idempotently discoverable.
- [x] Completion closure is singular and durable.
- [x] Current execution tests pass or are intentionally updated to assert canonical state.
