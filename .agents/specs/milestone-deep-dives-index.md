# Milestone Deep-Dive Index

Source roadmap: `.agents/specs/roadmap.md`

Supporting context: `.agents/specs/audit.md`

Generation status: Complete

## Milestones Found

| Roadmap Order | Milestone Identifier | Milestone Name | Deep-Dive File | Status |
|---|---|---|---|---|
| 1 | Milestone 1 | Migrate `RealignEpic` | `.agents/specs/m1-migrate-realign-epic-deep-dive.md` | Written |
| 2 | Milestone 2 | Migrate `ReimagineEpic` | `.agents/specs/m2-migrate-reimagine-epic-deep-dive.md` | Written |
| 3 | Milestone 3 | Migrate `GenerateMilestoneDeepDivesForEpic` | `.agents/specs/m3-migrate-generate-milestone-deep-dives-for-epic-deep-dive.md` | Written |
| 4 | Milestone 4 | Migrate `SplitEpic` | `.agents/specs/m4-migrate-split-epic-deep-dive.md` | Written |
| 5 | Milestone 5 | Retirement Checkpoint and Regression Hardening | `.agents/specs/m5-retirement-checkpoint-and-regression-hardening-deep-dive.md` | Written |

## Dependency Order Preserved

```text
Milestone 1: Migrate RealignEpic
  -> Milestone 2: Migrate ReimagineEpic
  -> Milestone 3: Migrate GenerateMilestoneDeepDivesForEpic
  -> Milestone 4: Migrate SplitEpic
  -> Milestone 5: Retirement Checkpoint and Regression Hardening
```

## Blocked Milestones

None.

## Ambiguities Detected

None that block generation.

The roadmap explicitly names all five milestones, their order, expected implementation changes, section semantics, tests, and acceptance criteria. The deep dives preserve those identifiers and sequencing.

## Cross-Milestone Constraints Preserved

- Prompt-owned section bodies remain in generated `.prompt` files.
- Section body text is not hard-coded in C#.
- `AllowAuxiliaryNonImplementationFiles` controls strict prompt-owned section injection.
- `AllowHitlRequestedNonImplementationFiles` remains a legacy composer concern.
- Primary contracted outputs are not auxiliary artifacts.
- Artifact authorization, promotion, validators, parser boundaries, repository write semantics, and projection freshness remain unchanged unless an existing bug blocks migration.
- `ImplementationFirstPromptPolicyComposer` remains transitional infrastructure for out-of-scope consumers.
