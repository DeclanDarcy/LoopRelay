# Milestone 4: Migrate `SplitEpic`

## Goal

- [x] Make `SplitEpic` prompt-owned while preserving split bundle semantics, child epic authoring quality, split lineage persistence, HITL capture, and selected-child promotion.

## Extracted Details

### Gap Filled

- [x] Preserve the deep-dive clarification that child epic files are primary contracted output inside the split bundle, not auxiliary artifacts.

## Code Changes

### Add

- [x] `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`
- [x] `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/SplitEpicPromptSections.cs`

### Modify

- [x] `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Preserve Existing Behavior

- [x] `SplitEpicTransition`
- [x] `BundleFileExtractor`
- [x] `SplitEpicBundleInterpreter`
- [x] `SplitFamilyStore`
- [x] `HitlArtifactCapture`
- [x] `ActiveEpicPromotionCoordinator`
- [x] `EpicArtifactValidator`
- [x] Prompt contract writer/parser boundaries

### Prompt Identity Inputs

- [x] Use mode `split-epic-prompt-owned-v1`.
- [x] Include prompt source hash key `splitEpicSourceHash`.
- [x] In strict mode, include `sectionMode=strict`.
- [x] In strict mode, include active section source hash `SplitEpicImplementationFirstGuidance`.
- [x] In strict mode, include active section source hash `SplitEpicAuxiliaryArtifactLimits`.
- [x] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [x] Use `{splitEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [x] Use `{splitEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [x] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [x] The sanctioned output boundary is the split bundle consumed by the split transition.
- [x] The split bundle includes a split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
- [x] Child epic files are primary contracted outputs, not invalid auxiliary artifacts.
- [x] The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
- [x] Child epics should follow the `CreateNewEpic` authoring standard at the concept level.
- [x] `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
- [x] Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
- [x] Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices are invalid auxiliary artifacts unless explicitly requested or required by an existing machine-consumed contract.
- [x] `# Split Epic Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [x] Runtime prompt remains the split proposal decomposition prompt.
- [x] Required input remains `.agents/selection` through roadmap prompt context.
- [x] Required outputs remain split family state and selected active epic as declared by prompt contracts.
- [x] Output remains a multi-file Markdown bundle consumed by `BundleFileExtractor` and `SplitEpicBundleInterpreter`.
- [x] Split plan and `# FILE: .agents/epic-N.md` child epic sections are primary contracted outputs.
- [x] Blocked response remains `# Split Epic Blocked`.
- [x] Strict render contains `# SplitEpic Implementation-First Guidance` and `# SplitEpic Auxiliary Artifact Limits`.
- [x] Allowed auxiliary render omits strict sections and leaves no placeholders.
- [x] Prompt contract registry remains unchanged:
  - writer boundary `SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService`
  - parser boundary `BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator`

## Tests

### Focused Tests

Add focused tests covering:

- [x] `SplitEpicPromptSections` strict and allowed behavior.
- [x] Strict render includes `# SplitEpic Implementation-First Guidance`.
- [x] Strict render includes `# SplitEpic Auxiliary Artifact Limits`.
- [x] Strict render sanctions the split bundle, split plan, and child epic file sections.
- [x] Strict render does not treat child epic files as invalid auxiliary artifacts.
- [x] Allowed render omits strict sections and leaves no placeholders.
- [x] Allowed render does not weaken bundle requirements.
- [x] Runtime prompt contains no legacy composer heading.
- [x] A legacy control prompt still receives the composer.
- [x] Transition identity mode is `split-epic-prompt-owned-v1`.
- [x] Strict identity includes prompt and active section source hashes.
- [x] Strict and allowed identities differ.
- [x] Section body text is not hard-coded in C#.
- [x] Prompt does not encourage task, file-area, phase, or layer splits.

### Regression Tests

Regression tests must keep passing for:

- [x] Prompt contract writer/parser boundaries.
- [x] Bundle extraction.
- [x] Split bundle interpretation.
- [x] Invalid bundle rejection.
- [x] Overlap detection.
- [x] Missing split plan or missing child detection.
- [x] Invalid child path rejection.
- [x] Split family persistence.
- [x] HITL capture.
- [x] HITL child artifact evidence capture.
- [x] Selected-child promotion.
- [x] Epic validation.

## Fixtures

- [x] Project context plus split proposal.
- [x] Valid split bundle with split plan and at least two `# FILE: .agents/epic-N.md` child epic sections.
- [x] Invalid task split fixture: files, phases, layers, or work items rather than capability partitions.
- [x] Invalid auxiliary fixture: companion inventory, RFC, governance note, research document, design report, or explanatory appendix outside the contracted bundle.
- [x] Blocked output: `# Split Epic Blocked`.
- [x] Split lineage fixture with valid child epics, stable order, and selected child.
- [x] Strict and allowed policy fixtures.
- [x] Legacy control prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~SplitEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~SplitEpicBundleInterpreterTests|FullyQualifiedName~SplitFamilyStoreTests"
```

## Acceptance

- [x] `SplitEpic` renders and runs without the legacy composer.
- [x] Transition snapshots identify `split-epic-prompt-owned-v1`.
- [x] The split bundle remains the sole sanctioned operational output boundary for the transition.
