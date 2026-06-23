# Milestone 7: Long-Horizon Reconstruction Validation

Goal: prove event-led reasoning survives large project histories.

## Backend Work

- [x] Add long-horizon fixture builders for many decisions, many reasoning events, repeated alternatives, recurring contradictions, failed assumptions, and strategic shifts.
- [x] Implement decision evolution reconstruction for chains, branches, convergence, supersession, and replacement.
- [x] Implement direction reconstruction as an emergent narrative from events and traces.
- [x] Implement hypothesis reconstruction for raised, supported, challenged, invalidated, and retired hypothesis events.
- [x] Implement contradiction reconstruction for identified, investigated, resolved, accepted, and recurring contradiction events.
- [x] Implement project narrative reconstruction across hypotheses, alternatives, contradictions, direction events, and decisions.
- [x] Add performance diagnostics for large histories without relying on wall-clock elapsed time as a correctness criterion.

## UI Work

- [x] Add project-level narrative reconstruction view.
- [x] Add horizon selector for decision, milestone, epic, project, and multi-year reconstruction.
- [x] Add source evidence collapse/expand controls for large reconstructions.

## Tests

- [x] A large fixture can answer why current strategy exists.
- [x] A large fixture can answer why an architecture was chosen.
- [x] A large fixture can list rejected alternatives and their rationale.
- [x] A large fixture can list failed assumptions and their outcomes.
- [x] A large fixture can identify contradictions that changed direction.
- [x] Reconstruction remains traceable to events and source evidence.
- [x] Reconstruction remains usable enough for UI consumption.

## Slice Notes

- Added a long-horizon repository recovery characterization test for `Repository Truth -> Recovered Repository -> Equivalent Reconstruction`.
- The fixture includes historical evidence, rejected and selected alternatives, a supported hypothesis, an invalidated assumption, a recurring contradiction, a direction shift, and a decision supersession.
- The test recreates repository and reasoning service instances against the same repository-backed artifacts, then compares graph and query signatures excluding generated timestamps.
- The test also verifies no derived authority directories are created for hypotheses, alternatives, contradictions, directions, graphs, or queries.
- Added recovered answer-level query coverage for architecture choice, rejected alternatives, failed assumptions, and contradictions that changed direction.
- The answer-level tests continue to use the generic `Graph -> Trace -> Reconstruction` pipeline; no specialized read model or first-class hypothesis, alternative, contradiction, or direction entity was introduced.
- Added grouped reconstruction details for UI consumption: evidence summary, events, relationships, external references, and threads are emitted as deterministic sections.
- Added recovered long-horizon UI usability coverage that checks section ordering, scan-friendly line length, key evidence visibility, confidence, and absence of derived authority artifacts.
- Added UI consumption for grouped generic reconstruction details: the reasoning reconstruction panel now renders metadata separately, exposes a project narrative view with decision/milestone/epic/project/multi-year horizon framing, and collapses grouped evidence sections for large reconstructions.
- Added characterization coverage for project-level UI reconstruction consumption without adding category-specific narrative engines or first-class derived entities.
- Added answerability certification that runs the recovered long-horizon evidence through materialization review scenarios for direction, alternative, hypothesis, contradiction, thread grouping, and decision replacement questions.
- Certified that the generic `Graph -> Trace -> Reconstruction -> Materialization Review` path answers the target M7 questions with high-confidence evidence, so specialized category engines and first-class derived entities remain unjustified for M7.
- Added deterministic scale diagnostics to reconstruction details, reporting evidence, event, relationship, external-reference, and thread counts without treating wall-clock elapsed time as correctness evidence.

## Exit Criteria

- [x] Decision evolution reconstruction is operational.
- [x] Direction reconstruction works without first-class direction persistence.
- [x] Hypothesis reconstruction works without first-class hypothesis persistence unless materialization was approved.
- [x] Alternative reconstruction works without first-class alternative persistence unless materialization was approved.
- [x] Contradiction reconstruction works without first-class contradiction persistence unless materialization was approved.
- [x] Project narrative reconstruction is operational.
