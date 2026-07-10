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
- [ ] Convert existing transition classes into runtime adapters:
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
- [ ] Treat legacy roadmap states related only to old execution handoff as compatibility states.
- [ ] Recognize and report legacy execution handoff states safely, but do not let them define active orchestration.
- [ ] Keep `LoopRelay.Roadmap.Cli` as a compatibility adapter while `src/LoopRelay.Cli` becomes able to run `TraditionalRoadmap`.

## Acceptance

- [ ] TraditionalRoadmap runs through the canonical transition runtime.
- [ ] TraditionalRoadmap reaches a canonical workflow-complete state that satisfies Plan entry.
- [ ] Existing roadmap characterization tests pass.
- [ ] Legacy roadmap orchestration is no longer an active authority.
