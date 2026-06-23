# Milestone 3: Option Generation

## Goal

generate multiple viable alternatives for every promoted candidate.

## Work

- [ ] Add `DecisionOptionType`:
  - [ ] `Adopt`
  - [ ] `Preserve`
  - [ ] `Refactor`
  - [ ] `Replace`
  - [ ] `Delay`
  - [ ] `Remove`
  - [ ] `Expand`
  - [ ] `Constrain`
  - [ ] `Investigate`
- [ ] Extend `DecisionOption` with:
  - [ ] option type
  - [ ] assumptions
  - [ ] dependencies
  - [ ] diagnostics
  - [ ] source evidence
- [ ] Add `IOptionGenerationService` and keep `DecisionGenerationService.GenerateProposalAsync` as the public orchestrator.
- [ ] Generate candidate-type-specific options:
  - [ ] architectural fork: preserve, incrementally evolve, replace, hybrid
  - [ ] strategic direction: accelerate, maintain, reduce scope, pivot
  - [ ] tactical choice: implement now, implement later, implement differently, avoid
  - [ ] operational blocker: fix, work around, defer, escalate
  - [ ] contradiction: resolve toward source A, resolve toward source B, merge, investigate
  - [ ] constraint conflict: honor constraint A, honor constraint B, narrow scope, escalate
  - [ ] supersession: keep active decision, supersede, archive, gather evidence
  - [ ] workflow continuation: continue, pause, re-sequence, reduce scope
- [ ] Target three options by default.
- [ ] Require at least two options unless an explicit single-option justification is persisted.
- [ ] Add option validation:
  - [ ] reject duplicate options
  - [ ] reject non-actionable options
  - [ ] reject options unrelated to candidate evidence
  - [ ] reject empty assumptions/dependencies when they are required by option type
- [ ] Add option deduplication by normalized title, type, and semantic evidence overlap.
- [ ] Add `DecisionOptionRelationship` for conflicts and dependencies between options.
- [ ] Persist generation diagnostics with generated, rejected, and deduplicated options.

## Tests

- [ ] Every candidate type can generate options.
- [ ] Default candidates generate at least two options.
- [ ] Architectural forks generate materially distinct architecture options.
- [ ] Operational blockers generate fix/workaround/defer/escalate choices when evidence supports them.
- [ ] Duplicate options collapse.
- [ ] Invalid options are rejected with diagnostics.
- [ ] Single-option output requires explicit persisted justification.
- [ ] Recommendation is not generated in this milestone except as an absent/placeholder field when the existing API shape requires it.

## Exit Criteria

- [ ] Humans receive real alternatives instead of inventing options.
- [ ] The current `options[0]` recommendation path is no longer part of option generation.
