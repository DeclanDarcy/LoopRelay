# Milestone 5: TraditionalRoadmap Migration

Objective: migrate the existing roadmap workflow onto the canonical runtime and controller.

## Work

- [ ] Add `TraditionalRoadmapWorkflowDefinition` under `src/LoopRelay.Roadmap.Cli/Services/Workflows`.
- [ ] Define stages:
  - [ ] Roadmap Context.
  - [ ] Strategic Initiative Selection.
  - [ ] Epic Preparation.
  - [ ] Milestone Specification.
  - [ ] Workflow Completion.
- [ ] Define transitions:
  - [ ] Bootstrap Roadmap Completion Context.
  - [ ] Update Roadmap Completion Context.
  - [ ] Select Next Initiative.
  - [ ] Audit Existing Epic.
  - [ ] Create Epic.
  - [ ] Split Epic.
  - [ ] Realign Epic.
  - [ ] Reimagine Epic.
  - [ ] Retire Epic.
  - [ ] Generate Milestone Deep Dives.
  - [ ] Verify Workflow Exit Gate.
- [ ] Convert existing transition classes into runtime transition definitions and components:
  - [ ] Prompt identity.
  - [ ] Product requirements.
  - [ ] Parser.
  - [ ] Output validator.
  - [ ] Effects.
  - [ ] Blocker and recovery metadata.
- [ ] Move orchestration responsibilities out of `RoadmapStateMachine`:
  - [ ] Transition ordering.
  - [ ] Prompt execution sequencing.
  - [ ] Transition persistence sequencing.
  - [ ] Lifecycle advancement.
  - [ ] Next-transition decisions.
- [ ] Preserve current roadmap rigor:
  - [ ] Projection freshness.
  - [ ] Prompt contract snapshots.
  - [ ] Input snapshots.
  - [ ] Selection provenance.
  - [ ] Artifact promotion validation.
  - [ ] Lifecycle state.
  - [ ] Decision ledger.
  - [ ] Split lineage.
  - [ ] Blocker evidence.
  - [ ] Recovery intent.
- [ ] Define the canonical downstream products:
  - [ ] `PreparedEpic`
  - [ ] `MilestoneSpecificationSet`
- [ ] Treat pre-unification roadmap states related only to old execution handoff as migration-only states.
- [ ] Recognize and report pre-unification execution handoff states safely, but do not let them define active orchestration.
- [ ] Retire `LoopRelay.Roadmap.Cli` as a public entry point once `src/LoopRelay.Cli` runs `TraditionalRoadmap`; reusable roadmap services may remain only as internal/domain services.

## Detail Requirements

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

### Pre-Unification State Handling

Pre-unification persisted states remain readable and must preserve resume capability where supported. They no longer define active orchestration and should not dictate the new model.

### TraditionalRoadmap Resolution

The resolver must determine TraditionalRoadmap stage, eligibility, blockers, waiting states, and completion. For default invocation, TraditionalRoadmap is selected only when `.agents/evals/*.md` is absent.

### M5 Validation Cases

Validation should cover fresh repository, existing roadmap, resume, blocked roadmap, cancelled roadmap, failed roadmap, stale projections, invalid projections, split, rewrite, create, retire, audit, milestone generation, and workflow completion.

### Certification Questions

TraditionalRoadmap certification should answer whether it executes through the runtime, whether transitions are declarative, whether stages are domain-oriented, whether products are canonical, whether workflow boundaries are explicit, whether explainability is preserved, whether recovery is preserved, and whether behavior is preserved.

## Acceptance

- [ ] TraditionalRoadmap runs through the canonical transition runtime.
- [ ] TraditionalRoadmap reaches a canonical workflow-complete state that satisfies Plan entry.
- [ ] Existing roadmap characterization tests pass.
- [ ] Old roadmap orchestration is no longer an active authority.
