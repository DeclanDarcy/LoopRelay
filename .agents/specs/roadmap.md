# Roadmap Runtime Prompt-Owned Policy Migration

## Objective

Retire the legacy roadmap runtime dependency on `ImplementationFirstPromptPolicyComposer` for the remaining roadmap artifact-authoring prompts:

* `RealignEpic`
* `ReimagineEpic`
* `GenerateMilestoneDeepDivesForEpic`
* `SplitEpic`

`CreateNewEpic` is already migrated and remains the architectural reference. Completion means these roadmap artifact-authoring prompts own their implementation-first guidance through generated prompt sections and no longer receive the legacy composer at runtime.

This roadmap does not delete `ImplementationFirstPromptPolicyComposer`. The composer remains transitional infrastructure for non-roadmap consumers and any roadmap prompts outside this artifact-authoring migration scope.

## Scope Boundaries

In scope:

* Prompt-owned generated implementation-first guidance for the four remaining roadmap artifact-authoring prompts.
* Prompt-specific auxiliary artifact limits for those prompts.
* Runtime prompt rendering changes near `RoadmapPromptCatalog`.
* Runtime composer-skip behavior in `RoadmapPromptRunner` for migrated prompts.
* Transition prompt-policy identity for each migrated prompt.
* Focused regression tests that prove behavior, provenance, and ownership.

Out of scope:

* `CreateNewEpic`, except as the reference implementation and regression guard.
* `AdversarialPlanReview`.
* Plan CLI prompts, Loop CLI prompts, completion prompts, decision prompts, and execution prompts.
* Global deletion of `ImplementationFirstPromptPolicyComposer`.
* Runtime artifact authorization, promotion, validators, parser boundaries, and repository semantics.
* Post-execution non-implementation review behavior.
* Broad prompt-policy architecture redesign.

## Reference Pattern

Each migration should follow the proven `CreateNewEpic` shape:

* Store section bodies in prompt-owned `.prompt` files under `src/LoopRelay.Core/Prompts/NonImplementation`.
* Keep prompt body ownership in the generated prompt system, not in C# string literals.
* Add explicit placeholders to the owning planning prompt only where the prompt should receive generated section text.
* Keep selection logic close to roadmap prompt rendering in `src/LoopRelay.Roadmap.Cli/Services/Prompts`.
* Use `AllowAuxiliaryNonImplementationFiles` to select strict section injection versus omitted strict sections.
* Keep `AllowHitlRequestedNonImplementationFiles` behavior isolated to the legacy composer path.
* Record prompt-policy identity with prompt source hash, policy branch value, section mode, and active section source hashes.
* Preserve behavior by preserving the prompt's reasoning model and artifact contract, not by copying legacy composer text.

Do not introduce a generic registry or shared prompt-policy framework before repeated migrations prove that the duplication is harmful. Prompt-specific selectors are the default.

## Migration Order

1. `RealignEpic`
2. `ReimagineEpic`
3. `GenerateMilestoneDeepDivesForEpic`
4. `SplitEpic`
5. Retirement checkpoint and regression hardening

This order minimizes risk. `RealignEpic` is the narrowest active-epic rewrite and shares the `.agents/epic.md` promotion path with `CreateNewEpic`. `ReimagineEpic` validates the broader active-epic replacement case after the smaller rewrite case is proven. `GenerateMilestoneDeepDivesForEpic` then resolves the strongest semantic tension with the legacy composer while using a simpler transition than split handling. `SplitEpic` comes last because it has the most complex bundle interpretation, split lineage persistence, HITL capture, and selected-child promotion path.

## Milestone 1: Migrate `RealignEpic`

### Goal

Make `RealignEpic` prompt-owned for implementation-first guidance while preserving audit-driven minimal patch behavior for `.agents/epic.md`.

### Implementation

* Add prompt-owned sections:
  * `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`
  * `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`
* Add placeholders to `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`.
* Create a prompt-specific selector in `src/LoopRelay.Roadmap.Cli/Services/Prompts`, following `CreateNewEpicPromptSections`.
* Update `RoadmapPromptCatalog.RenderRuntime()` to render `RealignEpic` through a prompt-specific render helper that injects selected sections.
* Update `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` so `RealignEpic` skips the legacy composer.
* Update `RoadmapRuntimePromptPolicy.CreateIdentity("RealignEpic")` with mode `realign-epic-prompt-owned-v1`.
* Keep `ActiveEpicRewriteTransition`, `ActiveEpicPromotionCoordinator`, `EpicAuthoringOutputClassifier`, and `EpicArtifactValidator` unchanged unless a test exposes an existing bug unrelated to policy ownership.

### Section Semantics

`RealignEpic` guidance must be specific to audit-driven minimal realignment:

* Updating `.agents/epic.md` is the sanctioned implementation-bearing artifact.
* Preserving epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content is implementation-first behavior.
* Audit findings may be compressed into scope, constraints, dependencies, acceptance criteria, risks, and follow-up only when they affect implemented capability.
* Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents remain invalid auxiliary artifacts.
* Unaffected epic content must not be rewritten for style or completeness.
* The blocked response `# Epic Realignment Blocked` does not authorize additional explanatory artifacts.

### Tests

Add focused tests beside `CreateNewEpicPromptPolicyTests`:

* Strict selection injects `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
* Allowed auxiliary mode omits strict sections and removes placeholders.
* Rendered strict prompt still sanctions `.agents/epic.md` and existing milestone roadmap preservation.
* `RoadmapPromptRunner` does not append `ImplementationFirstPromptPolicyComposer.SectionHeading` for `RealignEpic`.
* A legacy out-of-scope prompt still receives the composer.
* Transition prompt-policy identity changes between strict and allowed auxiliary branches and includes active section source hashes in strict mode.
* Section body text is not hard-coded in C# files.

### Acceptance

`RealignEpic` renders and runs without the legacy composer, transition snapshots identify `realign-epic-prompt-owned-v1`, and all artifact contracts, validators, promotion behavior, and transition flow remain unchanged.

## Milestone 2: Migrate `ReimagineEpic`

### Goal

Make `ReimagineEpic` prompt-owned while preserving audit-grounded full replacement behavior for `.agents/epic.md`.

### Implementation

* Add prompt-owned sections:
  * `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`
  * `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`
* Add placeholders to `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`.
* Add a prompt-specific selector and render helper.
* Extend `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` for `ReimagineEpic`.
* Add `RoadmapRuntimePromptPolicy.CreateIdentity("ReimagineEpic")` with mode `reimagine-epic-prompt-owned-v1`.
* Preserve the existing active epic rewrite transition, promotion, blocked output, and validator path.

If the `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic` identity branches create obvious mechanical duplication, a small helper may be introduced for constructing prompt-owned policy identity from the current prompt's source hash and active section hashes. Do not introduce a registry for unmigrated prompts, shared section bodies, or a new prompt-policy abstraction.

### Section Semantics

`ReimagineEpic` guidance must distinguish replacement from free-form design:

* Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
* The strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
* Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
* Every material redesign decision should trace to the audit, the projection, or repository-grounded capability needs.
* Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files remain invalid auxiliary artifacts.
* The blocked response `# Epic Reimagination Blocked` does not authorize additional explanatory artifacts.

### Tests

Add the same ownership and provenance test shape as `RealignEpic`, plus prompt-specific assertions:

* Strict render sanctions a replacement `.agents/epic.md` and coherent replacement milestone roadmap.
* Strict render does not imply side design reports or raw audit appendices.
* Runtime prompt contains no legacy composer heading.
* Transition identity mode and source hashes are prompt-specific.

### Acceptance

`ReimagineEpic` renders and runs without the legacy composer, transition snapshots identify `reimagine-epic-prompt-owned-v1`, and full epic replacement remains audit-grounded through the existing transition and promotion path.

## Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

### Goal

Make `GenerateMilestoneDeepDivesForEpic` prompt-owned while making `.agents/specs/*.md` explicitly sanctioned as implementation-bearing planning artifacts consumed by later execution preparation.

### Implementation

* Add prompt-owned sections:
  * `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`
  * `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`
* Add placeholders to `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`.
* Add a prompt-specific selector and render helper.
* Extend `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` for `GenerateMilestoneDeepDivesForEpic`.
* Add `RoadmapRuntimePromptPolicy.CreateIdentity("GenerateMilestoneDeepDivesForEpic")` with mode `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
* Preserve `GenerateMilestoneDeepDivesTransition`, bundle extraction, manifest writing, specs-ready marking, HITL evidence capture, execution preparation provenance, and roadmap invariant validation.

### Section Semantics

This prompt needs the clearest distinction between sanctioned planning and invalid auxiliary planning:

* The primary output bundle under `.agents/specs/*.md` is not auxiliary. It is the contracted operational artifact consumed by later implementation stages.
* Strict auxiliary-artifact policy must never suppress or weaken the requirement to generate one spec per milestone.
* Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution.
* Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
* Allowed auxiliary mode must not weaken the contracted spec bundle or permit the prompt to replace specs with narrative.
* Blocking or partial generation must use the prompt's existing output protocol and must not create side-channel explanations.

### Tests

Add focused tests:

* Strict render includes prompt-owned section markers and still requires `.agents/specs/*.md`.
* Allowed auxiliary mode omits strict sections and removes placeholders without weakening the spec bundle contract.
* Runtime prompt contains no legacy composer heading.
* Transition identity changes between strict and allowed auxiliary branches and includes active section source hashes in strict mode.
* Regression coverage confirms `.agents/specs/*.md` is treated as primary contracted output, not auxiliary output.
* Existing bundle extraction, manifest, execution-preparation provenance, and invariant tests remain passing.

### Acceptance

`GenerateMilestoneDeepDivesForEpic` renders and runs without the legacy composer, transition snapshots identify `generate-milestone-deep-dives-for-epic-prompt-owned-v1`, and milestone spec generation remains a required contracted output in both strict and allowed auxiliary modes.

## Milestone 4: Migrate `SplitEpic`

### Goal

Make `SplitEpic` prompt-owned while preserving split bundle semantics, child epic authoring quality, split lineage persistence, and selected-child promotion.

### Implementation

* Add prompt-owned sections:
  * `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`
  * `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`
* Add placeholders to `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`.
* Add a prompt-specific selector and render helper.
* Extend `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy()` for `SplitEpic`.
* Add `RoadmapRuntimePromptPolicy.CreateIdentity("SplitEpic")` with mode `split-epic-prompt-owned-v1`.
* Preserve `SplitEpicTransition`, `BundleFileExtractor`, `SplitEpicBundleInterpreter`, `SplitFamilyStore`, HITL artifact evidence capture, selected-child promotion, and `EpicArtifactValidator`.

### Section Semantics

`SplitEpic` guidance must be bundle-specific:

* The sanctioned output boundary is the split bundle consumed by the split transition, including the split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
* The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
* Child epics should follow the `CreateNewEpic` authoring standard at the concept level, but `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
* Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
* Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices remain invalid auxiliary artifacts unless explicitly requested or required by a machine-consumed contract.
* The blocked response `# Split Epic Blocked` does not authorize additional explanatory artifacts.

### Tests

Add focused tests:

* Strict render includes prompt-owned section markers and sanctions the split bundle, split plan, and child epic file sections.
* Strict render does not treat child epic files as invalid auxiliary artifacts.
* Allowed auxiliary mode omits strict sections and removes placeholders without weakening bundle requirements.
* Runtime prompt contains no legacy composer heading.
* Transition identity changes between strict and allowed auxiliary branches and includes active section source hashes in strict mode.
* Existing prompt contract tests continue to assert writer and parser boundaries for `SplitEpic`.
* Existing bundle interpreter, split family persistence, HITL capture, and promotion tests remain passing.

### Acceptance

`SplitEpic` renders and runs without the legacy composer, transition snapshots identify `split-epic-prompt-owned-v1`, and the split bundle remains the sole sanctioned operational output boundary for the transition.

## Milestone 5: Retirement Checkpoint and Regression Hardening

### Goal

Prove that roadmap runtime artifact-authoring prompts no longer depend on `ImplementationFirstPromptPolicyComposer`, while preserving composer behavior for all out-of-scope consumers.

### Implementation

* Add or update a single regression test that runs all in-scope artifact-authoring prompts through `RoadmapPromptRunner` in strict auxiliary mode and asserts none contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.
* Keep a companion assertion that an out-of-scope roadmap runtime prompt still receives the legacy composer until it is intentionally migrated under a separate scope.
* Add or update policy identity tests for all migrated prompts:
  * `create-new-epic-prompt-owned-v1`
  * `realign-epic-prompt-owned-v1`
  * `reimagine-epic-prompt-owned-v1`
  * `generate-milestone-deep-dives-for-epic-prompt-owned-v1`
  * `split-epic-prompt-owned-v1`
* Confirm prompt source hashes and active section source hashes participate in transition snapshots for each migrated prompt.
* Confirm `AllowAuxiliaryNonImplementationFiles` changes prompt-policy identity for each migrated prompt.
* Confirm `AllowHitlRequestedNonImplementationFiles` remains a legacy composer concern and is not repurposed for prompt-owned roadmap sections.
* Update comments, test names, or narrow ownership terminology that still describes roadmap artifact-authoring prompt policy as centralized, but do not rename or delete public concepts used by non-roadmap consumers.
* Leave all non-roadmap composer tests intact.

### Acceptance

The migration is complete when:

* `RealignEpic`, `ReimagineEpic`, `GenerateMilestoneDeepDivesForEpic`, and `SplitEpic` own prompt-specific implementation-first guidance through generated prompt sections.
* `RoadmapPromptRunner` does not append `ImplementationFirstPromptPolicyComposer` to `CreateNewEpic` or any of the four newly migrated prompts.
* Transition provenance accurately records prompt-owned policy selection for every migrated prompt.
* Strict auxiliary mode injects prompt-owned sections and records active section source hashes.
* Allowed auxiliary mode is represented explicitly in prompt-policy identity and does not weaken primary artifact contracts.
* Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
* `ImplementationFirstPromptPolicyComposer` remains available and tested for non-roadmap consumers and any out-of-scope legacy prompt paths.

## Cross-Milestone Rules

* Do not copy `CreateNewEpic` section text unchanged into another prompt.
* Do not use `InvalidContent.prompt` unchanged for these migrations.
* Do not move section bodies into C#.
* Do not let auxiliary-artifact policy suppress contracted primary outputs.
* Do not create infrastructure for prompts that have not been migrated.
* Do not change projection freshness tracking; runtime section provenance belongs in transition prompt-policy identity.
* Do not change artifact authorization, promotion, validators, parser boundaries, or repository write semantics unless an existing bug blocks the policy migration.
* Do not include `AdversarialPlanReview` in the completion count for this roadmap.

## Verification Matrix

Each migrated prompt must have coverage for:

| Prompt | Strict Section Markers | Allowed Branch Placeholder Removal | No Legacy Composer | Policy Identity Branch Hash | Primary Artifact Still Sanctioned | Existing Transition Path Unchanged |
|---|---|---|---|---|---|---|
| `RealignEpic` | Required | Required | Required | Required | `.agents/epic.md` minimal realignment | Required |
| `ReimagineEpic` | Required | Required | Required | Required | `.agents/epic.md` audit-grounded replacement | Required |
| `GenerateMilestoneDeepDivesForEpic` | Required | Required | Required | Required | `.agents/specs/*.md` milestone specs | Required |
| `SplitEpic` | Required | Required | Required | Required | Split bundle and child epic files | Required |

## Done Definition

This roadmap is done when the only roadmap runtime artifact-authoring prompt already migrated before the roadmap, `CreateNewEpic`, and the four prompts migrated by this roadmap are all prompt-owned for implementation-first policy, all five skip the legacy composer at runtime, and transition snapshots identify the selected prompt-owned policy branch with source-hash provenance.

The broader codebase is not done with composer retirement at that point. The composer remains intentional transitional technical debt for Plan CLI, Loop CLI, completion, decision, execution, and any other non-roadmap or out-of-scope consumers.
