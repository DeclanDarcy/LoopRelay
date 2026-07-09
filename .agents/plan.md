# Roadmap Runtime Prompt-Owned Policy Migration Plan

## Objective

Move the remaining roadmap artifact-authoring runtime prompts off the legacy `ImplementationFirstPromptPolicyComposer` path and into prompt-owned generated implementation-first policy sections.

The migrated prompt set is:

- `CreateNewEpic` (already prompt-owned, kept as the reference and regression guard)
- `RealignEpic`
- `ReimagineEpic`
- `GenerateMilestoneDeepDivesForEpic`
- `SplitEpic`

Completion means all five prompts render implementation-first guidance from generated `.prompt` sources, skip the legacy composer at runtime, and record prompt-owned transition policy identity with prompt and active section source hashes. The legacy composer remains available and tested for out-of-scope consumers.

## Current Codebase Baseline

- Prompt source files live under `src/LoopRelay.Core/Prompts`.
- `src/LoopRelay.Core/LoopRelay.Core.csproj` includes `Prompts/**/*.prompt` as analyzer `AdditionalFiles`, so new `.prompt` files produce generated classes with `Text`, `Render(...)`, and `SourceHash`.
- `CreateNewEpic` already uses prompt-owned policy sections:
  - `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicImplementationFirstGuidance.prompt`
  - `src/LoopRelay.Core/Prompts/NonImplementation/CreateNewEpicAuxiliaryArtifactLimits.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/CreateNewEpicPromptSections.cs`
  - `RoadmapPromptCatalog.RenderRuntime("CreateNewEpic", ...)` routes through `RenderCreateNewEpic(...)`.
  - `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy("CreateNewEpic")` returns `true`.
  - `RoadmapRuntimePromptPolicy.CreateIdentity("CreateNewEpic")` emits `create-new-epic-prompt-owned-v1`.
- `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` currently render directly from their planning prompt classes and still receive the legacy composer from `RoadmapPromptRunner`.
- `RoadmapPromptRunner.RunRuntimePromptAsync(...)` appends `ImplementationFirstPromptPolicyComposer` only when `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(runtimePromptName)` is false.
- `RoadmapPromptTransitionRunner` records prompt policy provenance by calling `RoadmapRuntimePromptPolicy.CreateIdentity(prompt)`.
- Prompt contracts for the in-scope prompts are declared in `PromptContractRegistry` and must remain behaviorally unchanged.

## In Scope

- Add prompt-owned generated implementation-first guidance and auxiliary-artifact limit sections for `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`.
- Add explicit placeholders to each owning planning prompt so generated prompt rendering can inject selected sections.
- Add prompt-specific section selectors under `src/LoopRelay.Roadmap.Cli/Services/Prompts`.
- Update `RoadmapPromptCatalog` to render each migrated prompt through a prompt-specific helper.
- Update `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(...)` so all five migrated prompts skip the legacy composer.
- Update `RoadmapRuntimePromptPolicy.CreateIdentity(...)` to emit prompt-owned identity branches for all five migrated prompts.
- Add focused and aggregate regression tests for render behavior, runner composer skipping, transition identity, source-hash provenance, and unchanged artifact contracts.

## Out Of Scope

- Do not delete `ImplementationFirstPromptPolicyComposer`.
- Do not migrate `AdversarialPlanReview`, Plan CLI prompts, Loop CLI prompts, completion prompts, decision prompts, execution prompts, or roadmap prompts outside the artifact-authoring set.
- Do not change artifact authorization, artifact promotion, validators, parser boundaries, repository write semantics, projection freshness, HITL behavior, post-execution non-implementation review, or transition state machines unless a pre-existing bug blocks the prompt-policy migration.
- Do not introduce a generic prompt-policy registry or broad framework for prompts that are not being migrated.
- Do not move section body text into C#.
- Do not reuse `InvalidContent.prompt` unchanged for these prompt-specific migrations.

## Common Implementation Pattern

For each newly migrated prompt:

1. Add two `.prompt` files under `src/LoopRelay.Core/Prompts/NonImplementation`:
   - `{PromptName}ImplementationFirstGuidance.prompt`
   - `{PromptName}AuxiliaryArtifactLimits.prompt`
2. Add two placeholders to the corresponding planning prompt under `src/LoopRelay.Core/Prompts/Planning`.
3. Add a prompt-specific selector under `src/LoopRelay.Roadmap.Cli/Services/Prompts`, matching the `CreateNewEpicPromptSections` shape:
   - an internal sealed record containing the two selected section strings and `IReadOnlyDictionary<string, string> ActiveSectionSourceHashes`
   - `ForAuxiliaryArtifactPolicy(bool allowAuxiliaryNonImplementationFiles)`
   - strict mode returns both generated section texts and both section `SourceHash` values
   - allowed auxiliary mode returns empty section strings and an empty hash map
4. Update `RoadmapPromptCatalog.RenderRuntime(...)`:
   - route the prompt through a private render helper
   - call the prompt-specific selector with `policy.AllowAuxiliaryNonImplementationFiles`
   - pass selected section strings into the generated planning prompt render method
5. Update `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(...)`:
   - return true for `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic`
6. Update `RoadmapRuntimePromptPolicy.CreateIdentity(...)`:
   - use the same section selection branch as rendering
   - include `allowAuxiliaryNonImplementationFiles`
   - include the planning prompt `SourceHash`
   - include `sectionMode` as `strict` when sections are active and `omitted` when allowed auxiliary mode omits them
   - include `section.{SectionName}.sourceHash` entries for strict mode
   - emit the prompt-specific mode string listed below
7. Keep `AllowHitlRequestedNonImplementationFiles` out of prompt-owned section selection. It remains part of the legacy composer path through `ImplementationFirstPromptPolicyComposer.Compose(...)`.

Prompt-owned identity modes:

| Runtime Prompt | Mode |
|---|---|
| `CreateNewEpic` | `create-new-epic-prompt-owned-v1` |
| `RealignEpic` | `realign-epic-prompt-owned-v1` |
| `ReimagineEpic` | `reimagine-epic-prompt-owned-v1` |
| `GenerateMilestoneDeepDivesForEpic` | `generate-milestone-deep-dives-for-epic-prompt-owned-v1` |
| `SplitEpic` | `split-epic-prompt-owned-v1` |

Recommended placeholder and identity key names:

| Runtime Prompt | Guidance Placeholder | Auxiliary Limits Placeholder | Prompt Source Hash Key | Section Source Hash Keys |
|---|---|---|---|---|
| `RealignEpic` | `{realignEpicImplementationFirstGuidance}` | `{realignEpicAuxiliaryArtifactLimits}` | `realignEpicSourceHash` | `RealignEpicImplementationFirstGuidance`, `RealignEpicAuxiliaryArtifactLimits` |
| `ReimagineEpic` | `{reimagineEpicImplementationFirstGuidance}` | `{reimagineEpicAuxiliaryArtifactLimits}` | `reimagineEpicSourceHash` | `ReimagineEpicImplementationFirstGuidance`, `ReimagineEpicAuxiliaryArtifactLimits` |
| `GenerateMilestoneDeepDivesForEpic` | `{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}` | `{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}` | `generateMilestoneDeepDivesForEpicSourceHash` | `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`, `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits` |
| `SplitEpic` | `{splitEpicImplementationFirstGuidance}` | `{splitEpicAuxiliaryArtifactLimits}` | `splitEpicSourceHash` | `SplitEpicImplementationFirstGuidance`, `SplitEpicAuxiliaryArtifactLimits` |

## Milestone 1: Migrate `RealignEpic`

(See ./milestones/m1-realign-epic.md)

## Milestone 2: Migrate `ReimagineEpic`

(See ./milestones/m2-reimagine-epic.md)

## Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

(See ./milestones/m3-milestone-deep-dives.md)

## Milestone 4: Migrate `SplitEpic`

(See ./milestones/m4-split-epic.md)

## Milestone 5: Retirement Checkpoint And Regression Hardening

(See ./milestones/m5-retirement-checkpoint.md)

## Verification Commands

Run focused tests after each milestone:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

Run affected regressions for spec generation:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~GenerateMilestoneDeepDivesForEpicPromptPolicyTests|FullyQualifiedName~BundleFileExtractorTests|FullyQualifiedName~BundleManifestWriterTests|FullyQualifiedName~ExecutionPreparationProvenanceTests|FullyQualifiedName~InvariantValidatorTests"
```

Run affected regressions for split handling:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~SplitEpicPromptPolicyTests|FullyQualifiedName~SplitEpicBundleInterpreterTests|FullyQualifiedName~SplitFamilyStoreTests|FullyQualifiedName~HitlArtifactCaptureTests|FullyQualifiedName~EpicArtifactPromotionTests"
```

Run final certification:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~PromptContractRegistryTests|FullyQualifiedName~NonImplementationReview"
```

Run the full solution before declaring the migration complete:

```powershell
dotnet test LoopRelay.slnx
```

## Cross-Milestone Invariants

- Prompt body and policy section text remain in `.prompt` files, not C# string literals.
- Prompt-specific selectors are preferred over a generic registry until duplication causes a concrete maintenance problem.
- Strict prompt-owned sections are selected only by `AllowAuxiliaryNonImplementationFiles`.
- `AllowHitlRequestedNonImplementationFiles` remains a legacy composer concern.
- Primary contracted outputs are never treated as auxiliary artifacts.
- Allowed auxiliary mode omits strict policy sections but does not weaken primary output requirements.
- Runtime prompt render branch and transition identity branch must use equivalent section selection.
- Render or identity failures for prompt-owned prompts must fail before agent execution and must not fall back to appending the legacy composer.
- Existing writer/parser boundaries remain unchanged unless a pre-existing defect blocks the migration.
- `AdversarialPlanReview` and non-roadmap consumers are not counted as migrated by this work.

## Final Done Definition

The migration is complete when:

- `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` have prompt-owned generated implementation-first guidance and auxiliary-artifact limit sections.
- `CreateNewEpic` remains prompt-owned and passing its existing regressions.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(...)` classifies all five in-scope artifact-authoring prompts as prompt-owned.
- `RoadmapPromptRunner` does not append `ImplementationFirstPromptPolicyComposer` to any migrated prompt.
- `RoadmapRuntimePromptPolicy.CreateIdentity(...)` emits prompt-owned mode strings and source-hash provenance for every migrated prompt.
- Strict and allowed auxiliary branches produce different identities for every migrated prompt.
- Strict identity includes active section source hashes, and allowed identity records omitted section mode.
- Primary artifact contracts remain intact for `.agents/epic.md`, `.agents/specs/*.md`, split bundles, and child epic files.
- Existing transition, promotion, parser, validator, HITL, invariant, and execution-preparation regressions pass.
- Legacy composer behavior remains intact for out-of-scope consumers.
