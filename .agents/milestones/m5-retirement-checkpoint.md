# Milestone 5: Retirement Checkpoint And Regression Hardening

## Goal

- [x] Certify that all roadmap artifact-authoring prompts in scope are prompt-owned for implementation-first policy while preserving the legacy composer for out-of-scope consumers.

## Extracted Details

### Gap Filled

- [x] Preserve the full aggregate certification matrix and settings-separation coverage from the roadmap deep dive.

### Aggregate Prompt Set

All aggregate tests and identity matrix tests must explicitly cover:

- [x] `CreateNewEpic`
- [x] `RealignEpic`
- [x] `ReimagineEpic`
- [x] `GenerateMilestoneDeepDivesForEpic`
- [x] `SplitEpic`

## Aggregate Tests

Add or update tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts` that:

- [x] Run `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` through production `RoadmapPromptRunner` or production catalog paths in strict auxiliary mode.
- [x] Assert none of the five captured prompts contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [x] Assert a legacy control prompt, such as `SelectNextEpic`, still receives `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [x] Assert `CreateNewEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [x] Assert `RealignEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [x] Assert `ReimagineEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [x] Assert `GenerateMilestoneDeepDivesForEpic` preserves its primary artifact contract for `.agents/specs/*.md`.
- [x] Assert `SplitEpic` preserves its primary artifact contract for split bundle and child epic file sections.

## Identity Matrix Tests

Add or update identity matrix tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` that:

- [x] Cover `create-new-epic-prompt-owned-v1`.
- [x] Cover `realign-epic-prompt-owned-v1`.
- [x] Cover `reimagine-epic-prompt-owned-v1`.
- [x] Cover `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [x] Cover `split-epic-prompt-owned-v1`.
- [x] Verify strict and allowed auxiliary identities differ for every migrated prompt.
- [x] Verify strict identity includes planning prompt source hash and active section source hashes.
- [x] Verify allowed identity records `sectionMode=omitted` and no active `section.*.sourceHash` entries.
- [x] Verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned identity.
- [x] Verify `AllowHitlRequestedNonImplementationFiles` does not affect prompt-owned section selection.
- [x] Verify legacy prompts still use `legacy-implementation-first-composer-v1` and record `legacyImplementationFirstPromptPolicyHash`.

### Identity Input Matrix

| Runtime Prompt | Mode | Prompt Source Hash Key | Strict Section Source Hash Keys |
|---|---|---|---|
| `CreateNewEpic` | `create-new-epic-prompt-owned-v1` | existing `CreateNewEpic` key | existing `CreateNewEpic` section keys |
| `RealignEpic` | `realign-epic-prompt-owned-v1` | `realignEpicSourceHash` | `RealignEpicImplementationFirstGuidance`, `RealignEpicAuxiliaryArtifactLimits` |
| `ReimagineEpic` | `reimagine-epic-prompt-owned-v1` | `reimagineEpicSourceHash` | `ReimagineEpicImplementationFirstGuidance`, `ReimagineEpicAuxiliaryArtifactLimits` |
| `GenerateMilestoneDeepDivesForEpic` | `generate-milestone-deep-dives-for-epic-prompt-owned-v1` | `generateMilestoneDeepDivesForEpicSourceHash` | `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`, `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits` |
| `SplitEpic` | `split-epic-prompt-owned-v1` | `splitEpicSourceHash` | `SplitEpicImplementationFirstGuidance`, `SplitEpicAuxiliaryArtifactLimits` |

## Settings Fixtures

- [x] Strict prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=false`, `AllowHitlRequestedNonImplementationFiles=false`.
- [x] Allowed prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=true`, `AllowHitlRequestedNonImplementationFiles=false`.
- [x] HITL separation fixture: policy variants that toggle `AllowHitlRequestedNonImplementationFiles` while prompt-owned selector behavior remains unchanged.

## Prompt Input Fixture Map

- [x] `CreateNewEpic`: project context plus proposal.
- [x] `RealignEpic`: project context plus audit input.
- [x] `ReimagineEpic`: project context plus audit input.
- [x] `GenerateMilestoneDeepDivesForEpic`: project context and empty secondary input.
- [x] `SplitEpic`: project context plus split proposal.
- [x] Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

## Certification Matrix

| Prompt | Primary Artifact Contract | Key Regression Surface |
|---|---|---|
| `CreateNewEpic` | `.agents/epic.md` | Existing prompt-owned reference behavior, runner skip, identity branch |
| `RealignEpic` | `.agents/epic.md` minimal realignment | Active epic rewrite, promotion, classifier, validator, blocked output |
| `ReimagineEpic` | `.agents/epic.md` audit-grounded replacement | Active epic rewrite, promotion, classifier, validator, blocked output |
| `GenerateMilestoneDeepDivesForEpic` | `.agents/specs/*.md` one spec per milestone | Bundle extraction, manifest writing, specs-ready state, HITL evidence, execution preparation provenance, invariants |
| `SplitEpic` | split bundle, split plan, child epic files, selected-child promotion | Bundle extraction, split interpreter, split family store, HITL capture, selected-child promotion, epic validation |

## Source-Adjacent Cleanup

- [x] Update test names, comments, or narrow internal terminology only when they now inaccurately imply that roadmap artifact-authoring prompt policy is centralized in the legacy composer.
- [x] Keep narrow internal comments clarifying that the composer remains transitional infrastructure for out-of-scope consumers when useful.
- [x] Do not rename or delete public concepts used by out-of-scope consumers.
- [x] Do not produce administrative migration reports, readiness reports, governance notes, ADRs, RFCs, or manual certification documents as implementation outputs.
- [x] Do not delete `ImplementationFirstPromptPolicyComposer`.
- [x] Do not remove non-roadmap composer tests.

## Acceptance

- [x] All five migrated prompts skip the legacy composer at runtime.
- [x] A legacy prompt still receives the composer.
- [x] Transition provenance records source-hashed prompt-owned policy identity for every migrated prompt.
- [x] Strict auxiliary mode injects prompt-owned sections and records active section hashes.
- [x] Allowed auxiliary mode omits prompt-owned strict sections without weakening primary artifact contracts.
- [x] `ImplementationFirstPromptPolicyComposer` remains present and tested.
- [x] Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
- [x] Existing non-roadmap composer tests remain passing.
- [x] No future, out-of-scope migration or composer deletion is falsely claimed.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~PromptContractRegistryTests|FullyQualifiedName~NonImplementationReview"
```
