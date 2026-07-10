# Milestone 6: Plan Migration

Objective: migrate `PlanPipeline` into a first-class Plan workflow.

## Work

- [ ] Add `PlanWorkflowDefinition` under `src/LoopRelay.Plan.Cli/Services/Workflows` or a shared workflow definitions location once dependencies allow it.
- [ ] Define stages:
  - [ ] Planning.
  - [ ] Plan Validation.
  - [ ] Execution Preparation.
  - [ ] Workflow Completion.
- [ ] Define transitions:
  - [ ] Write Executable Plan.
  - [ ] Generate Adversarial Projection.
  - [ ] Run Adversarial Review.
  - [ ] Revise Plan.
  - [ ] Generate Operational Context.
  - [ ] Collect Details.
  - [ ] Generate Execution Milestones.
  - [ ] Refine Execution Details.
  - [ ] Verify Execute Contract.
- [ ] Adapt current components:
  - [ ] `PlanSession` becomes a prompt executor using warm-session posture.
  - [ ] `ReviewStep` becomes a read-only prompt transition.
  - [ ] `PermissionedArtifactOperationStep` becomes scoped-operation posture.
  - [ ] `OneShotSteps` become transition definitions or transition-specific prompt context builders.
  - [ ] `AgentsSubmodulePublisher` and parent gitlink recording become ordered effects.
- [ ] Add canonical Plan state that distinguishes:
  - [ ] Not started.
  - [ ] Planning in progress.
  - [ ] Plan authored.
  - [ ] Validation in progress.
  - [ ] Validation complete.
  - [ ] Execution preparation in progress.
  - [ ] Partial execution products.
  - [ ] Execution-ready.
  - [ ] Blocked.
  - [ ] Cancelled.
  - [ ] Failed.
  - [ ] Completed.
- [ ] Define the canonical Execute entry product set:
  - [ ] `ExecutablePlan`
  - [ ] `OperationalContext`
  - [ ] `ExecutionDetails`
  - [ ] `ExecutionMilestoneSet`
  - [ ] `ExecutionReadiness`
- [ ] Replace fresh-run preflight ambiguity with durable partial-state semantics.
- [ ] Treat existing outputs as products with producer evidence, validation state, and resume eligibility.
- [ ] Keep `LoopRelay.Plan.Cli` as a compatibility adapter while `src/LoopRelay.Cli` becomes able to run `Plan`.

## Acceptance

- [ ] Plan runs through the canonical runtime.
- [ ] Plan can resume at the correct stage after interruption.
- [ ] Plan completion satisfies or fails Execute entry through the canonical gate.
- [ ] Current Plan pipeline tests pass or are intentionally updated to assert the new canonical behavior.
