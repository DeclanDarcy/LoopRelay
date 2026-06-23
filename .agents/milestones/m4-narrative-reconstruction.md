# Milestone 4: Narrative Reconstruction Queries

Goal: turn graph traversal into explanations.

## Backend Work

- [x] Add `ReasoningQuery`, `ReasoningQueryCategory`, `ReasoningQueryResult`, `ReasoningReconstruction`, `ReasoningNarrative`, and explainability models.
- [x] Implement `IReasoningQueryService` and `IReasoningReconstructionService`.
- [x] Support query categories:
  - [x] Decision: why made, why superseded, what alternatives existed.
  - [x] Hypothesis: what happened, why failed, what evidence mattered.
  - [x] Contradiction: how resolved, did it recur.
  - [x] Direction: why strategy changed, what replaced it.
  - [x] Thread: how a reasoning thread evolved.
- [x] Convert traces into narratives with cited events, relationships, references, and evidence.
- [ ] Implement "why" reconstruction for decisions, rejected alternatives, direction shifts, accepted contradictions, and invalidated assumptions.
- [ ] Implement historical state reconstruction from event timelines:
  - [ ] What hypothesis events were active at a point in time.
  - [ ] What alternatives existed at a point in time.
  - [ ] What contradictions were active at a point in time.
  - [ ] What direction events were visible at a point in time.
- [ ] Persist reconstruction reports only when explicitly requested.
- [x] Add query and reconstruction endpoints.

## UI Work

- [x] Add `ReasoningQueryPanel` with predefined question categories and scoped target selection.
- [x] Add `ReasoningReconstructionPanel` showing narrative, confidence, evidence, graph path, and diagnostics.
- [x] Make source evidence visible without forcing users to inspect JSON files.

## Tests

- [x] "Why was this decision superseded?" reconstructs the chain.
- [ ] "What killed this hypothesis?" reconstructs contradicting evidence.
- [x] "Why does current strategy exist?" reconstructs direction evolution from events.
- [x] "What alternatives were rejected?" reconstructs alternative history.
- [x] M4 does not require persisted hypothesis, alternative, contradiction, or direction entities.
- [x] Same query over unchanged repository state returns the same reasoning path.
- [x] UI exposes narrative and supporting evidence.

## Exit Criteria

- [x] Query model is operational.
- [x] Narrative reconstruction is operational.
- [ ] Historical reconstruction is operational.
- [x] Explainability is operational.
