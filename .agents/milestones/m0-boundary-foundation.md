# Milestone 0: Boundary and Minimal Ontology Foundation

Goal: establish terminology, boundaries, materialization rules, and repository contracts before storage or workflows.

## Workstreams

- [ ] Add `docs/reasoning-taxonomy.md` with definitions for Reasoning Trajectory, Reasoning Event, Reasoning Thread, Reasoning Relationship, Reasoning Reference, Reasoning Graph, Reasoning Query, and Reasoning Reconstruction.
- [ ] Define hypothesis, alternative, contradiction, and direction as analytical categories first, not first-class persisted entities.
- [ ] Add `docs/reasoning-ownership-boundaries.md` with an ownership matrix:
  - [ ] Proposal revisions: Decision Lifecycle.
  - [ ] Decision outcomes: Decision Lifecycle.
  - [ ] Settled understanding: Operational Context.
  - [ ] Execution directives: Execution Projection.
  - [ ] Contradiction detection: Governance.
  - [ ] Contradiction history: Reasoning Trajectory events.
  - [ ] Hypothesis history: Reasoning Trajectory events.
  - [ ] Alternative history beyond proposal scope: Reasoning Trajectory events.
  - [ ] Direction evolution: derived from Reasoning Trajectory events until materialization is approved.
- [ ] Add `docs/reasoning-materialization-policy.md` documenting the materialization gate, the "derived if reconstructable" rule, thread review, and the rule that event families do not imply entity existence.
- [ ] Add `docs/reasoning-capture-policy.md` documenting manual capture, assisted capture, inferred capture, idempotency, and the expectation that inferred capture becomes dominant for source-domain transitions the system can observe directly.
- [ ] Add `docs/reasoning-authority-boundary.md` documenting that reasoning may support, influence, and explain decisions, but may not override decisions, become authority, or replace governance.
- [ ] Add `docs/reasoning-repository-contracts.md` defining `.agents/reasoning` as repository-scoped, event-led, recoverable, schema-versioned, and separate from `.agents/decisions` and `.agents/operational_context.md`.
- [ ] Add boundary certification notes answering:
  - [ ] Why this does not belong in Operational Context.
  - [ ] Why this does not belong in Decision Lifecycle.
  - [ ] Why this is not just another decision artifact.
  - [ ] How reasoning remains non-authoritative.
  - [ ] How the plan avoids becoming a second knowledge system.

## Tests and Verification

- [ ] Build succeeds after documentation and solution scaffolding.
- [ ] Documentation references current code paths and target paths accurately.
- [ ] No specialized hypothesis, alternative, contradiction, or direction persistence is introduced.
- [ ] Event families are documented as classification vocabulary, not lifecycle authority.
- [ ] Capture policy distinguishes user-supplied rationale from inferred source-domain transitions.

## Exit Criteria

- [ ] Vocabulary exists.
- [ ] Ownership exists.
- [ ] Materialization policy exists.
- [ ] Authority boundaries exist.
- [ ] Repository contracts exist.
- [ ] Boundary certification passes by documentation alone.
