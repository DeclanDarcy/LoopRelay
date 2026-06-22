# Decisions

## Newly Authorized

- M0B is complete for the decision lifecycle foundation; M0 remains in progress until M0C projection generation and M0D recovery/regeneration/compatibility are complete.
- M0C should proceed next as the Decision Artifact Projection Foundation.
- `DecisionArtifactProjectionService` must preserve the authority direction `structured JSON -> markdown`; markdown projections must not become lifecycle authority.
- M0C should focus on deterministic projection generation and existing artifact ecosystem compatibility; recovery logic should remain deferred to M0D.
- Projection ordering must be deterministic, including relationships, evidence, assumptions, options, and history, to support clean diffs, rotation, certification, and regeneration.
- Generic artifact discovery/editing must exclude `decision.json`, `candidate.json`, `proposal.json`, and `history.json`; only markdown lifecycle projections should appear where artifact browser compatibility requires them.
- M0C implementation order is: `DecisionArtifactProjectionService`, `decision.md` generation, `candidate.md` generation, `proposal.md` generation, `decisions.md` index generation, deterministic projection ordering, artifact discovery compatibility tests, decision index compatibility tests, rotation compatibility tests.
