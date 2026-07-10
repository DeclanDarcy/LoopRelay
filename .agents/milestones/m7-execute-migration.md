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
- [ ] Ensure compatibility callers may delegate to Execute completion, but no other active orchestration path may own completion closure.
- [ ] Add recovery for interruption during:
  - [ ] Completion review.
  - [ ] Completion evaluation.
  - [ ] Archive materialization.
  - [ ] Archive synthesis.
  - [ ] Roadmap completion-context update.
  - [ ] Final closed-state persistence.
- [ ] Preserve stall semantics with durable evidence.

## Acceptance

- [ ] Execute runs through the canonical runtime and controller.
- [ ] Execution stage resolves correctly after process restart.
- [ ] Already-closed execution is idempotently discoverable.
- [ ] Completion closure is singular and durable.
- [ ] Current execution tests pass or are intentionally updated to assert canonical state.
