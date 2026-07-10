# Milestone 6: Plan Migration

Objective: migrate `PlanPipeline` into a first-class Plan workflow.

## Work

- [x] Add `PlanWorkflowDefinition` under `src/LoopRelay.Plan.Cli/Services/Workflows` or a shared workflow definitions location once dependencies allow it.
- [x] Define stages:
  - [x] Planning.
  - [x] Plan Validation.
  - [x] Execution Preparation.
  - [x] Workflow Completion.
- [x] Define transitions:
  - [x] Write Executable Plan.
  - [x] Generate Adversarial Projection.
  - [x] Run Adversarial Review.
  - [x] Revise Plan.
  - [x] Generate Operational Context.
  - [x] Collect Details.
  - [x] Generate Execution Milestones.
  - [x] Refine Execution Details.
  - [x] Verify Execute Contract.
- [x] Adapt current components:
  - [x] `PlanSession` becomes a prompt executor using warm-session posture.
  - [x] `ReviewStep` becomes a read-only prompt transition.
  - [x] `PermissionedArtifactOperationStep` becomes scoped-operation posture.
  - [x] `OneShotSteps` become transition definitions or transition-specific prompt context builders.
  - [x] `AgentsSubmodulePublisher` and parent gitlink recording become ordered effects.
- [x] Generated Plan prompt templates render through the unified runtime prompt renderer with source-hash evidence where generated prompt assets exist.
- [x] `GenerateOperationalContext` seeds `.agents/operational_context.md` through the canonical runtime as a deterministic local artifact effect while leaving Plan resumable in Execution Preparation.
- [x] Execution milestone sets without strict trackable checkboxes are invalid for the canonical Execute entry gate.
- [x] Add canonical Plan state that distinguishes:
  - [x] Not started.
  - [x] Planning in progress.
  - [x] Plan authored.
  - [x] Validation in progress.
  - [x] Validation complete.
  - [x] Execution preparation in progress.
  - [x] Partial execution products.
  - [x] Execution-ready.
  - [x] Blocked.
  - [x] Cancelled.
  - [x] Failed.
  - [x] Completed.
- [x] Define the canonical Execute entry product set:
  - [x] `ExecutablePlan`
  - [x] `OperationalContext`
  - [x] `ExecutionDetails`
  - [x] `ExecutionMilestoneSet`
  - [x] `ExecutionReadiness`
- [x] Replace fresh-run preflight ambiguity with durable partial-state semantics.
- [x] Treat existing outputs as products with producer evidence, validation state, and resume eligibility.
- [x] Retire `LoopRelay.Plan.Cli` as a public entry point once `src/LoopRelay.Cli` runs `Plan`; reusable planning services may remain only as internal/domain services.

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

- [x] Plan runs through the canonical runtime.
- [x] Plan can resume at the correct stage after interruption.
- [x] Plan completion satisfies or fails Execute entry through the canonical gate.
- [x] Current Plan pipeline tests pass or are intentionally updated to assert the new canonical behavior.
