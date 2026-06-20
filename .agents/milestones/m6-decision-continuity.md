# Milestone M6 - Decision Continuity

## Objective

Assimilate important decisions and rationale into current understanding while keeping decision artifacts as the decision history authority.

M6 completes the decision-aware portion of compression. After M6, compression can distinguish durable decision rationale from tactical or historical decision noise.

## Backend Changes

- [x] Add `IDecisionAnalysisService`.
- [x] Parse current decisions and bounded relevant historical decision artifacts through `ArtifactService`.
- [x] Introduce decision taxonomy:
  - [x] `ArchitecturalDecision`.
  - [x] `StrategicDecision`.
  - [x] `TacticalDecision`.
  - [x] `HistoricalDecision`.
- [x] Analyze decisions for:
  - [x] Decision statement.
  - [x] Rationale.
  - [x] Constraints introduced.
  - [x] Consequences.
  - [x] Open decision questions.
  - [x] Superseded or retired decisions.
- [x] Generation must assimilate:
  - [x] Architectural decisions.
  - [x] Strategic decisions.
  - [x] Decision rationale that explains durable constraints.
  - [x] Open decisions as open questions.
- [x] Generation must not assimilate:
  - [x] One-time approvals.
  - [x] Temporary workarounds with no future relevance.
  - [x] Execution detail.
  - [x] Closed investigations without current consequence.
- [ ] Extend semantic changes with decision-specific change types:
  - [ ] Important decision introduced.
  - [ ] Decision retired.
  - [ ] Rationale changed.
  - [ ] Rationale lost warning.
  - [ ] Open decision preserved.
  - [ ] Open decision resolved.
- [x] Extend compression and review warnings for:
  - [x] Lost decision rationale.
  - [x] Tactical decision accumulation.
  - [x] Historical decision replay.
  - [x] Contradictory decision preservation.

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

- [x] Architectural decisions survive proposals and promotions.
- [ ] Strategic decisions survive while relevant.
- [x] Tactical decisions remain in decisions history without bloating operational context.
- [x] Rationale survives for assimilated decisions.
- [x] Open decisions appear as open questions.
- [x] Duplicate contradictory decisions are flagged.
- [x] Decision rationale loss is surfaced as a warning.

## Certification

Decision continuity is certified when important decisions and their rationale become durable current understanding, unresolved decisions remain visible, and operational context does not become a decision archive.
