# Milestone 5: Retirement Checkpoint And Regression Hardening

## Goal

- [ ] Certify that all roadmap artifact-authoring prompts in scope are prompt-owned for implementation-first policy while preserving the legacy composer for out-of-scope consumers.

## Extracted Details

### Gap Filled

- [ ] Preserve the full aggregate certification matrix and settings-separation coverage from the roadmap deep dive.

### Aggregate Prompt Set

All aggregate tests and identity matrix tests must explicitly cover:

- [ ] `CreateNewEpic`
- [ ] `RealignEpic`
- [ ] `ReimagineEpic`
- [ ] `GenerateMilestoneDeepDivesForEpic`
- [ ] `SplitEpic`

## Aggregate Tests

Add or update tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts` that:

- [ ] Run `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` through production `RoadmapPromptRunner` or production catalog paths in strict auxiliary mode.
- [ ] Assert none of the five captured prompts contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] Assert a legacy control prompt, such as `SelectNextEpic`, still receives `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] Assert `CreateNewEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `RealignEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `ReimagineEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `GenerateMilestoneDeepDivesForEpic` preserves its primary artifact contract for `.agents/specs/*.md`.
- [ ] Assert `SplitEpic` preserves its primary artifact contract for split bundle and child epic file sections.

## Identity Matrix Tests

Add or update identity matrix tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` that:

- [ ] Cover `create-new-epic-prompt-owned-v1`.
- [ ] Cover `realign-epic-prompt-owned-v1`.
- [ ] Cover `reimagine-epic-prompt-owned-v1`.
- [ ] Cover `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] Cover `split-epic-prompt-owned-v1`.
- [ ] Verify strict and allowed auxiliary identities differ for every migrated prompt.
- [ ] Verify strict identity includes planning prompt source hash and active section source hashes.
- [ ] Verify allowed identity records `sectionMode=omitted` and no active `section.*.sourceHash` entries.
- [ ] Verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned identity.
- [ ] Verify `AllowHitlRequestedNonImplementationFiles` does not affect prompt-owned section selection.
- [ ] Verify legacy prompts still use `legacy-implementation-first-composer-v1` and record `legacyImplementationFirstPromptPolicyHash`.

### Identity Input Matrix

| Runtime Prompt | Mode | Prompt Source Hash Key | Strict Section Source Hash Keys |
|---|---|---|---|
| `CreateNewEpic` | `create-new-epic-prompt-owned-v1` | existing `CreateNewEpic` key | existing `CreateNewEpic` section keys |
| `RealignEpic` | `realign-epic-prompt-owned-v1` | `realignEpicSourceHash` | `RealignEpicImplementationFirstGuidance`, `RealignEpicAuxiliaryArtifactLimits` |
| `ReimagineEpic` | `reimagine-epic-prompt-owned-v1` | `reimagineEpicSourceHash` | `ReimagineEpicImplementationFirstGuidance`, `ReimagineEpicAuxiliaryArtifactLimits` |
| `GenerateMilestoneDeepDivesForEpic` | `generate-milestone-deep-dives-for-epic-prompt-owned-v1` | `generateMilestoneDeepDivesForEpicSourceHash` | `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`, `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits` |
| `SplitEpic` | `split-epic-prompt-owned-v1` | `splitEpicSourceHash` | `SplitEpicImplementationFirstGuidance`, `SplitEpicAuxiliaryArtifactLimits` |

## Settings Fixtures

- [ ] Strict prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=false`, `AllowHitlRequestedNonImplementationFiles=false`.
- [ ] Allowed prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=true`, `AllowHitlRequestedNonImplementationFiles=false`.
- [ ] HITL separation fixture: policy variants that toggle `AllowHitlRequestedNonImplementationFiles` while prompt-owned selector behavior remains unchanged.

## Prompt Input Fixture Map

- [ ] `CreateNewEpic`: project context plus proposal.
- [ ] `RealignEpic`: project context plus audit input.
- [ ] `ReimagineEpic`: project context plus audit input.
- [ ] `GenerateMilestoneDeepDivesForEpic`: project context and empty secondary input.
- [ ] `SplitEpic`: project context plus split proposal.
- [ ] Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

## Certification Matrix

| Prompt | Primary Artifact Contract | Key Regression Surface |
|---|---|---|
| `CreateNewEpic` | `.agents/epic.md` | Existing prompt-owned reference behavior, runner skip, identity branch |
| `RealignEpic` | `.agents/epic.md` minimal realignment | Active epic rewrite, promotion, classifier, validator, blocked output |
| `ReimagineEpic` | `.agents/epic.md` audit-grounded replacement | Active epic rewrite, promotion, classifier, validator, blocked output |
| `GenerateMilestoneDeepDivesForEpic` | `.agents/specs/*.md` one spec per milestone | Bundle extraction, manifest writing, specs-ready state, HITL evidence, execution preparation provenance, invariants |
| `SplitEpic` | split bundle, split plan, child epic files, selected-child promotion | Bundle extraction, split interpreter, split family store, HITL capture, selected-child promotion, epic validation |

## Source-Adjacent Cleanup

- [ ] Update test names, comments, or narrow internal terminology only when they now inaccurately imply that roadmap artifact-authoring prompt policy is centralized in the legacy composer.
- [ ] Keep narrow internal comments clarifying that the composer remains transitional infrastructure for out-of-scope consumers when useful.
- [ ] Do not rename or delete public concepts used by out-of-scope consumers.
- [ ] Do not produce administrative migration reports, readiness reports, governance notes, ADRs, RFCs, or manual certification documents as implementation outputs.
- [ ] Do not delete `ImplementationFirstPromptPolicyComposer`.
- [ ] Do not remove non-roadmap composer tests.

## Acceptance

- [ ] All five migrated prompts skip the legacy composer at runtime.
- [ ] A legacy prompt still receives the composer.
- [ ] Transition provenance records source-hashed prompt-owned policy identity for every migrated prompt.
- [ ] Strict auxiliary mode injects prompt-owned sections and records active section hashes.
- [ ] Allowed auxiliary mode omits prompt-owned strict sections without weakening primary artifact contracts.
- [ ] `ImplementationFirstPromptPolicyComposer` remains present and tested.
- [ ] Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
- [ ] Existing non-roadmap composer tests remain passing.
- [ ] No future, out-of-scope migration or composer deletion is falsely claimed.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~PromptContractRegistryTests|FullyQualifiedName~NonImplementationReview"
```
