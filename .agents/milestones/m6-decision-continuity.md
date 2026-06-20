# Milestone M6 - Decision Continuity

## Objective

Assimilate important decisions and rationale into current understanding while keeping decision artifacts as the decision history authority.

M6 completes the decision-aware portion of compression. After M6, compression can distinguish durable decision rationale from tactical or historical decision noise.

## Backend Changes

- [ ] Add `IDecisionAnalysisService`.
- [ ] Parse current decisions and bounded relevant historical decision artifacts through `ArtifactService`.
- [ ] Introduce decision taxonomy:
  - [ ] `ArchitecturalDecision`.
  - [ ] `StrategicDecision`.
  - [ ] `TacticalDecision`.
  - [ ] `HistoricalDecision`.
- [ ] Analyze decisions for:
  - [ ] Decision statement.
  - [ ] Rationale.
  - [ ] Constraints introduced.
  - [ ] Consequences.
  - [ ] Open decision questions.
  - [ ] Superseded or retired decisions.
- [ ] Generation must assimilate:
  - [ ] Architectural decisions.
  - [ ] Strategic decisions.
  - [ ] Decision rationale that explains durable constraints.
  - [ ] Open decisions as open questions.
- [ ] Generation must not assimilate:
  - [ ] One-time approvals.
  - [ ] Temporary workarounds with no future relevance.
  - [ ] Execution detail.
  - [ ] Closed investigations without current consequence.
- [ ] Extend semantic changes with decision-specific change types:
  - [ ] Important decision introduced.
  - [ ] Decision retired.
  - [ ] Rationale changed.
  - [ ] Rationale lost warning.
  - [ ] Open decision preserved.
  - [ ] Open decision resolved.
- [ ] Extend compression and review warnings for:
  - [ ] Lost decision rationale.
  - [ ] Tactical decision accumulation.
  - [ ] Historical decision replay.
  - [ ] Contradictory decision preservation.

## UI Changes

- [ ] Understanding surface shows:
  - [ ] Stable decisions.
  - [ ] Open decisions.
  - [ ] Decision rationale.
  - [ ] Decision changes between revisions.
  - [ ] Decision rationale changes.
- [ ] Review panel asks whether important decisions and rationale were preserved.

## Tests

Add backend tests:

- [ ] Architectural decisions survive proposals and promotions.
- [ ] Strategic decisions survive while relevant.
- [ ] Tactical decisions remain in decisions history without bloating operational context.
- [ ] Rationale survives for assimilated decisions.
- [ ] Open decisions appear as open questions.
- [ ] Duplicate contradictory decisions are flagged.
- [ ] Decision rationale loss is surfaced as a warning.

## Certification

Decision continuity is certified when important decisions and their rationale become durable current understanding, unresolved decisions remain visible, and operational context does not become a decision archive.
