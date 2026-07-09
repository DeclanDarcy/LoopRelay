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

### Goal

Make `RealignEpic` prompt-owned for implementation-first policy while preserving audit-driven minimal patch behavior for `.agents/epic.md`.

### Code Changes

- Add:
  - `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`
  - `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RealignEpicPromptSections.cs`
- Modify:
  - `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- Do not modify unless a blocking bug is exposed:
  - `ActiveEpicRewriteTransition`
  - `ActiveEpicPromotionCoordinator`
  - `EpicAuthoringOutputClassifier`
  - `EpicArtifactValidator`
  - `PromptContractRegistry`

### Section Semantics

The strict guidance must state that:

- Updating `.agents/epic.md` is the sanctioned implementation-bearing output.
- Realignment is a minimal strategic patch, not a replacement epic.
- Epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content must be preserved.
- Audit findings may be compressed into scope, constraints, dependencies, assumptions, acceptance criteria, risks, and follow-up only when they affect implemented capability.
- Unaffected sections must not be rewritten for style or completeness.
- Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents are invalid auxiliary artifacts.
- `# Epic Realignment Blocked` does not authorize extra explanatory artifacts.

### Tests

Add tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`, beside `CreateNewEpicPromptPolicyTests`, covering:

- strict selection includes `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`
- allowed auxiliary mode omits both strict sections and leaves no placeholders
- strict render sanctions `.agents/epic.md` and preserving existing milestone roadmap content
- runtime runner does not append `ImplementationFirstPromptPolicyComposer.SectionHeading`
- a legacy control prompt still receives the composer
- section body text is not hard-coded in C# files

Extend transition identity coverage under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`:

- strict and allowed identities differ
- strict mode is `realign-epic-prompt-owned-v1`
- strict identity includes the `RealignEpic` prompt source hash and active section source hashes
- allowed identity records omitted section mode and no active section hashes

### Acceptance

- `RealignEpic` renders and runs without the legacy composer.
- Transition snapshots identify `realign-epic-prompt-owned-v1`.
- Existing active-epic rewrite, promotion, validation, blocked-output, and prompt contract tests remain passing.

## Milestone 2: Migrate `ReimagineEpic`

### Goal

Make `ReimagineEpic` prompt-owned for implementation-first policy while preserving audit-grounded full replacement behavior for `.agents/epic.md`.

### Code Changes

- Add:
  - `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`
  - `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/ReimagineEpicPromptSections.cs`
- Modify:
  - `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- Keep the existing active epic rewrite transition, promotion, blocked output, validation, and prompt contract paths unchanged.

### Section Semantics

The strict guidance must state that:

- Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
- The strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
- Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
- Every material redesign decision should trace to audit findings, projection context, or repository-grounded capability needs already supplied to the prompt.
- Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files are invalid auxiliary artifacts.
- `# Epic Reimagination Blocked` does not authorize extra explanatory artifacts.

### Tests

Add focused tests covering:

- strict section selection markers
- allowed branch placeholder removal
- strict render sanctions replacement `.agents/epic.md` and a coherent replacement milestone roadmap
- strict render does not imply side design reports or raw audit appendices
- runtime prompt contains no legacy composer heading
- transition identity mode is `reimagine-epic-prompt-owned-v1`
- strict identity includes prompt and section source hashes
- section body text is not hard-coded in C#

### Acceptance

- `ReimagineEpic` renders and runs without the legacy composer.
- Transition snapshots identify `reimagine-epic-prompt-owned-v1`.
- Full epic replacement remains audit-grounded through the existing transition and promotion path.

## Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

### Goal

Make `GenerateMilestoneDeepDivesForEpic` prompt-owned while preserving `.agents/specs/*.md` as the required primary machine-consumed implementation-planning output bundle.

### Code Changes

- Add:
  - `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`
  - `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/GenerateMilestoneDeepDivesForEpicPromptSections.cs`
- Modify:
  - `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- Preserve existing behavior in:
  - `GenerateMilestoneDeepDivesTransition`
  - `BundleFileExtractor`
  - `BundleManifestWriter`
  - execution preparation provenance services
  - HITL evidence capture
  - `InvariantValidator`
  - prompt contract registry semantics

### Section Semantics

The strict guidance must state that:

- The primary output bundle under `.agents/specs/*.md` is not auxiliary output.
- One deep-dive spec per epic milestone remains required in strict and allowed auxiliary modes.
- Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution stages.
- Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
- Allowed auxiliary mode must not permit narrative output to replace the contracted spec bundle.
- Blocking or partial generation must use the existing output protocol and must not create side-channel explanations.

### Tests

Add focused tests covering:

- strict render includes `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance`
- strict render includes `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`
- strict and allowed renders both require `.agents/specs/*.md`
- allowed render omits strict sections and leaves no placeholders
- runtime prompt contains no legacy composer heading
- transition identity mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1`
- strict identity includes prompt and active section source hashes
- `.agents/specs/*.md` is treated as a primary contracted output, not auxiliary output

Regression tests must keep passing for:

- bundle extraction
- bundle manifest writing
- execution preparation provenance
- HITL artifact capture
- invariant validation
- roadmap state transition to milestone specs ready

### Acceptance

- `GenerateMilestoneDeepDivesForEpic` renders and runs without the legacy composer.
- Transition snapshots identify `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- `.agents/specs/*.md` generation remains required and machine-consumed in both strict and allowed auxiliary modes.

## Milestone 4: Migrate `SplitEpic`

### Goal

Make `SplitEpic` prompt-owned while preserving split bundle semantics, child epic authoring quality, split lineage persistence, HITL capture, and selected-child promotion.

### Code Changes

- Add:
  - `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`
  - `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/SplitEpicPromptSections.cs`
- Modify:
  - `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
  - `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`
- Preserve existing behavior in:
  - `SplitEpicTransition`
  - `BundleFileExtractor`
  - `SplitEpicBundleInterpreter`
  - `SplitFamilyStore`
  - `HitlArtifactCapture`
  - `ActiveEpicPromotionCoordinator`
  - `EpicArtifactValidator`
  - prompt contract writer/parser boundaries

### Section Semantics

The strict guidance must state that:

- The sanctioned output boundary is the split bundle consumed by the split transition.
- The split bundle includes a split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
- Child epic files are primary contracted outputs, not invalid auxiliary artifacts.
- The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
- Child epics should follow the `CreateNewEpic` authoring standard at the concept level, while `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
- Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
- Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices are invalid auxiliary artifacts unless explicitly requested or required by an existing machine-consumed contract.
- `# Split Epic Blocked` does not authorize extra explanatory artifacts.

### Tests

Add focused tests covering:

- strict render includes `# SplitEpic Implementation-First Guidance`
- strict render includes `# SplitEpic Auxiliary Artifact Limits`
- strict render sanctions the split bundle, split plan, and child epic file sections
- strict render does not treat child epic files as invalid auxiliary artifacts
- allowed render omits strict sections and leaves no placeholders
- allowed render does not weaken bundle requirements
- runtime prompt contains no legacy composer heading
- transition identity mode is `split-epic-prompt-owned-v1`
- strict identity includes prompt and active section source hashes

Regression tests must keep passing for:

- prompt contract writer/parser boundaries
- bundle extraction
- split bundle interpretation
- split family persistence
- HITL capture
- selected-child promotion
- epic validation

### Acceptance

- `SplitEpic` renders and runs without the legacy composer.
- Transition snapshots identify `split-epic-prompt-owned-v1`.
- The split bundle remains the sole sanctioned operational output boundary for the transition.

## Milestone 5: Retirement Checkpoint And Regression Hardening

### Goal

Certify that all roadmap artifact-authoring prompts in scope are prompt-owned for implementation-first policy while preserving the legacy composer for out-of-scope consumers.

### Aggregate Tests

Add or update tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts` that:

- run `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` through `RoadmapPromptRunner` in strict auxiliary mode
- assert none of the five captured prompts contain `ImplementationFirstPromptPolicyComposer.SectionHeading`
- assert a legacy control prompt, such as `SelectNextEpic`, still receives `ImplementationFirstPromptPolicyComposer.SectionHeading`
- assert each migrated prompt preserves its primary artifact contract:
  - `CreateNewEpic`, `RealignEpic`, `ReimagineEpic`: `.agents/epic.md`
  - `GenerateMilestoneDeepDivesForEpic`: `.agents/specs/*.md`
  - `SplitEpic`: split bundle and child epic file sections

Add or update identity matrix tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState` that:

- cover all five prompt-owned mode strings
- verify strict and allowed auxiliary identities differ for every migrated prompt
- verify strict identity includes planning prompt source hash and active section source hashes
- verify allowed identity records `sectionMode=omitted` and no active `section.*.sourceHash` entries
- verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned identity
- verify `AllowHitlRequestedNonImplementationFiles` does not affect prompt-owned section selection
- verify legacy prompts still use `legacy-implementation-first-composer-v1` and record `legacyImplementationFirstPromptPolicyHash`

### Source-Adjacent Cleanup

Update test names, comments, or narrow internal terminology only when they now inaccurately imply that roadmap artifact-authoring prompt policy is centralized in the legacy composer. Do not rename or delete public concepts used by out-of-scope consumers.

### Acceptance

- All five migrated prompts skip the legacy composer at runtime.
- A legacy prompt still receives the composer.
- Transition provenance records source-hashed prompt-owned policy identity for every migrated prompt.
- Strict auxiliary mode injects prompt-owned sections and records active section hashes.
- Allowed auxiliary mode omits prompt-owned strict sections without weakening primary artifact contracts.
- `ImplementationFirstPromptPolicyComposer` remains present and tested.
- Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.

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
