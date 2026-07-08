# Milestone 2: Extract Shared Services Without Moving Transitions

## Work Items

- [ ] Move existing private helper behavior into the shared services listed in the plan.
- [ ] Keep `RoadmapStateMachine` calling the same operations through the new services.
- [ ] Do not move transition bodies yet.

## Acceptance

- [ ] `RoadmapStateMachine` behavior is unchanged.
- [ ] Existing constructor wiring is updated in `RoadmapCliComposition` and `StateMachineFactory`.
- [ ] No handler is introduced until shared helper tests pass.
- [ ] Roadmap CLI tests pass after each helper extraction.

## Equivalence Checks

- [ ] `SaveStateAsync` still preserves existing retired epics, blockers, transition intent, active artifacts, projection manifest counts, split-family count, and last decision id.
- [ ] Default `NextTransitions` values remain identical.
- [ ] Runtime prompt failures still bypass generic failure overwrite by throwing `RoadmapStepException.AlreadyPersisted`.
- [ ] `OperationCanceledException` is not caught by prompt failure blocks.
