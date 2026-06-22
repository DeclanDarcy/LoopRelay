# Milestone 4: Narrative Reconstruction Queries

Goal: turn graph traversal into explanations.

## Backend Work

- [ ] Add `ReasoningQuery`, `ReasoningQueryCategory`, `ReasoningQueryResult`, `ReasoningReconstruction`, `ReasoningNarrative`, and explainability models.
- [ ] Implement `IReasoningQueryService` and `IReasoningReconstructionService`.
- [ ] Support query categories:
  - [ ] Decision: why made, why superseded, what alternatives existed.
  - [ ] Hypothesis: what happened, why failed, what evidence mattered.
  - [ ] Contradiction: how resolved, did it recur.
  - [ ] Direction: why strategy changed, what replaced it.
  - [ ] Thread: how a reasoning thread evolved.
- [ ] Convert traces into narratives with cited events, relationships, references, and evidence.
- [ ] Implement "why" reconstruction for decisions, rejected alternatives, direction shifts, accepted contradictions, and invalidated assumptions.
- [ ] Implement historical state reconstruction from event timelines:
  - [ ] What hypothesis events were active at a point in time.
  - [ ] What alternatives existed at a point in time.
  - [ ] What contradictions were active at a point in time.
  - [ ] What direction events were visible at a point in time.
- [ ] Persist reconstruction reports only when explicitly requested.
- [ ] Add query and reconstruction endpoints.

## UI Work

- [ ] Add `ReasoningQueryPanel` with predefined question categories and scoped target selection.
- [ ] Add `ReasoningReconstructionPanel` showing narrative, confidence, evidence, graph path, and diagnostics.
- [ ] Make source evidence visible without forcing users to inspect JSON files.

## Tests

- [ ] "Why was this decision superseded?" reconstructs the chain.
- [ ] "What killed this hypothesis?" reconstructs contradicting evidence.
- [ ] "Why does current strategy exist?" reconstructs direction evolution from events.
- [ ] "What alternatives were rejected?" reconstructs alternative history.
- [ ] M4 does not require persisted hypothesis, alternative, contradiction, or direction entities.
- [ ] Same query over unchanged repository state returns the same reasoning path.
- [ ] UI exposes narrative and supporting evidence.

## Exit Criteria

- [ ] Query model is operational.
- [ ] Narrative reconstruction is operational.
- [ ] Historical reconstruction is operational.
- [ ] Explainability is operational.
