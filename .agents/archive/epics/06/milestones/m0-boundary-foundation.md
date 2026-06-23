# Milestone 0: Boundary and Minimal Ontology Foundation

Goal: establish terminology, boundaries, materialization rules, and repository contracts before storage or workflows.

## Workstreams

- [x] Add `docs/reasoning-taxonomy.md` with definitions for Reasoning Trajectory, Reasoning Event, Reasoning Thread, Reasoning Relationship, Reasoning Reference, Reasoning Graph, Reasoning Query, and Reasoning Reconstruction.
- [x] Define hypothesis, alternative, contradiction, and direction as analytical categories first, not first-class persisted entities.
- [x] Add `docs/reasoning-ownership-boundaries.md` with an ownership matrix:
  - [x] Proposal revisions: Decision Lifecycle.
  - [x] Decision outcomes: Decision Lifecycle.
  - [x] Settled understanding: Operational Context.
  - [x] Execution directives: Execution Projection.
  - [x] Contradiction detection: Governance.
  - [x] Contradiction history: Reasoning Trajectory events.
  - [x] Hypothesis history: Reasoning Trajectory events.
  - [x] Alternative history beyond proposal scope: Reasoning Trajectory events.
  - [x] Direction evolution: derived from Reasoning Trajectory events until materialization is approved.
- [x] Add `docs/reasoning-materialization-policy.md` documenting the materialization gate, the "derived if reconstructable" rule, thread review, and the rule that event families do not imply entity existence.
- [x] Add `docs/reasoning-capture-policy.md` documenting manual capture, assisted capture, inferred capture, idempotency, and the expectation that inferred capture becomes dominant for source-domain transitions the system can observe directly.
- [x] Add `docs/reasoning-authority-boundary.md` documenting that reasoning may support, influence, and explain decisions, but may not override decisions, become authority, or replace governance.
- [x] Add `docs/reasoning-repository-contracts.md` defining `.agents/reasoning` as repository-scoped, event-led, recoverable, schema-versioned, and separate from `.agents/decisions` and `.agents/operational_context.md`.
- [x] Add boundary certification notes answering:
  - [x] Why this does not belong in Operational Context.
  - [x] Why this does not belong in Decision Lifecycle.
  - [x] Why this is not just another decision artifact.
  - [x] How reasoning remains non-authoritative.
  - [x] How the plan avoids becoming a second knowledge system.

## Tests and Verification

- [x] Build succeeds after documentation and solution scaffolding.
- [x] Documentation references current code paths and target paths accurately.
- [x] No specialized hypothesis, alternative, contradiction, or direction persistence is introduced.
- [x] Event families are documented as classification vocabulary, not lifecycle authority.
- [x] Capture policy distinguishes user-supplied rationale from inferred source-domain transitions.

## Exit Criteria

- [x] Vocabulary exists.
- [x] Ownership exists.
- [x] Materialization policy exists.
- [x] Authority boundaries exist.
- [x] Repository contracts exist.
- [x] Boundary certification passes by documentation alone.

## Boundary Certification Notes

Reasoning Trajectory does not belong in Operational Context because operational context represents current settled understanding, while reasoning preserves event-level history, rejected alternatives, failed assumptions, contradictions, and direction changes that may no longer be current.

Reasoning Trajectory does not belong in Decision Lifecycle because Decision Lifecycle owns decision artifacts and lifecycle authority. Reasoning may explain why thinking changed across decisions, but it must not approve, reject, supersede, resolve, or archive decisions.

Reasoning Trajectory is not just another decision artifact because it also spans governance findings, operational-context revisions, handoffs, execution outputs, assumptions, constraints, alternatives outside proposal scope, and long-running direction evolution.

Reasoning remains non-authoritative by using append-only explanatory events, typed source references, provenance, deterministic projections, and derived reconstructions. It does not own current state or mutation authority for other domains.

The plan avoids a second knowledge system by keeping repository files authoritative, making graph/query/reconstruction outputs derived, requiring materialization review before specialized entities, and explicitly forbidding first-class hypothesis, alternative, contradiction, and direction persistence at the start.
