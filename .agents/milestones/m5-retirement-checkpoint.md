# Milestone 5: Retirement Checkpoint And Regression Hardening

## Goal

- [ ] Certify that all roadmap artifact-authoring prompts in scope are prompt-owned for implementation-first policy while preserving the legacy composer for out-of-scope consumers.

## Aggregate Tests

Add or update tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts` that:

- [ ] Run `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` through `RoadmapPromptRunner` in strict auxiliary mode.
- [ ] Assert none of the five captured prompts contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] Assert a legacy control prompt, such as `SelectNextEpic`, still receives `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] Assert `CreateNewEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `RealignEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `ReimagineEpic` preserves its primary artifact contract for `.agents/epic.md`.
- [ ] Assert `GenerateMilestoneDeepDivesForEpic` preserves its primary artifact contract for `.agents/specs/*.md`.
- [ ] Assert `SplitEpic` preserves its primary artifact contract for split bundle and child epic file sections.

## Identity Matrix Tests

Add or update identity matrix tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` that:

- [ ] Cover all five prompt-owned mode strings.
- [ ] Verify strict and allowed auxiliary identities differ for every migrated prompt.
- [ ] Verify strict identity includes planning prompt source hash and active section source hashes.
- [ ] Verify allowed identity records `sectionMode=omitted` and no active `section.*.sourceHash` entries.
- [ ] Verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned identity.
- [ ] Verify `AllowHitlRequestedNonImplementationFiles` does not affect prompt-owned section selection.
- [ ] Verify legacy prompts still use `legacy-implementation-first-composer-v1` and record `legacyImplementationFirstPromptPolicyHash`.

## Source-Adjacent Cleanup

- [ ] Update test names, comments, or narrow internal terminology only when they now inaccurately imply that roadmap artifact-authoring prompt policy is centralized in the legacy composer.
- [ ] Do not rename or delete public concepts used by out-of-scope consumers.

## Acceptance

- [ ] All five migrated prompts skip the legacy composer at runtime.
- [ ] A legacy prompt still receives the composer.
- [ ] Transition provenance records source-hashed prompt-owned policy identity for every migrated prompt.
- [ ] Strict auxiliary mode injects prompt-owned sections and records active section hashes.
- [ ] Allowed auxiliary mode omits prompt-owned strict sections without weakening primary artifact contracts.
- [ ] `ImplementationFirstPromptPolicyComposer` remains present and tested.
- [ ] Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
