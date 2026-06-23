# Milestone 1: Decision Context Foundation Upgrade

## Goal

turn the deterministic item-based context into a typed generation context while preserving the existing `DecisionContext` snapshot behavior.

## Work

- [ ] Add `DecisionGenerationContext` with sections:
  - [ ] `Project`
  - [ ] `Milestone`
  - [ ] `OperationalContext`
  - [ ] `Handoff`
  - [ ] `DecisionHistory`
  - [ ] `Repository`
  - [ ] `Constraints`
  - [ ] `Risks`
  - [ ] `Questions`
- [ ] Add `IDecisionContextProjectionService` that maps the current `DecisionContext.Items` model into the typed generation context.
- [ ] Preserve the existing `DecisionContextService.BuildContextAsync` and snapshot APIs.
- [ ] Add source diagnostics for each typed context section:
  - [ ] sources used
  - [ ] sources missing
  - [ ] warning count
  - [ ] context size
  - [ ] source fingerprints
- [ ] Add repository-state context using existing `IGitService` or repository snapshot capabilities where available:
  - [ ] branch
  - [ ] dirty state
  - [ ] modified paths
  - [ ] recent commit summaries when available
- [ ] Extract constraints, risks, and questions from operational context, decisions, handoffs, and milestones using evidence-backed source references.
- [ ] Validate required inputs:
  - [ ] plan
  - [ ] active milestone
  - [ ] repository metadata
- [ ] Treat operational context, handoff, and decision history as optional but diagnostic.

## Tests

- [ ] Context projection produces all typed sections from existing repository artifacts.
- [ ] Missing required inputs fail validation.
- [ ] Missing optional inputs produce warnings, not failure.
- [ ] Constraints, risks, and questions carry source references.
- [ ] Context fingerprint changes when a contributing source changes.
- [ ] Context snapshot reloads after restart.

## Exit Criteria

- [ ] Decision generation can consume one typed context object.
- [ ] The UI can inspect objectives, constraints, risks, questions, decision history, and diagnostics.
- [ ] Existing context endpoints and tests continue to pass.
