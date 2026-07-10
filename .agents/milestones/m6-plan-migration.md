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

## Detail Requirements

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

## Acceptance

- [ ] Plan runs through the canonical runtime.
- [ ] Plan can resume at the correct stage after interruption.
- [ ] Plan completion satisfies or fails Execute entry through the canonical gate.
- [ ] Current Plan pipeline tests pass or are intentionally updated to assert the new canonical behavior.
