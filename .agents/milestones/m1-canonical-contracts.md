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

## Acceptance

- [ ] New tests in `tests/LoopRelay.Orchestration.Primitives.Tests` validate contract invariants.
- [ ] All four workflow definitions can be represented through the same contract types.
- [ ] Existing CLI and workflow tests still pass.
