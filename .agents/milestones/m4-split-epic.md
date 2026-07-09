# Milestone 4: Migrate `SplitEpic`

## Goal

- [ ] Make `SplitEpic` prompt-owned while preserving split bundle semantics, child epic authoring quality, split lineage persistence, HITL capture, and selected-child promotion.

## Code Changes

### Add

- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`
- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/SplitEpicPromptSections.cs`

### Modify

- [ ] `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Preserve Existing Behavior

- [ ] `SplitEpicTransition`
- [ ] `BundleFileExtractor`
- [ ] `SplitEpicBundleInterpreter`
- [ ] `SplitFamilyStore`
- [ ] `HitlArtifactCapture`
- [ ] `ActiveEpicPromotionCoordinator`
- [ ] `EpicArtifactValidator`
- [ ] Prompt contract writer/parser boundaries

## Section Semantics

The strict guidance must state that:

- [ ] The sanctioned output boundary is the split bundle consumed by the split transition.
- [ ] The split bundle includes a split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
- [ ] Child epic files are primary contracted outputs, not invalid auxiliary artifacts.
- [ ] The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
- [ ] Child epics should follow the `CreateNewEpic` authoring standard at the concept level, while `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
- [ ] Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
- [ ] Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices are invalid auxiliary artifacts unless explicitly requested or required by an existing machine-consumed contract.
- [ ] `# Split Epic Blocked` does not authorize extra explanatory artifacts.

## Tests

### Focused Tests

Add focused tests covering:

- [ ] Strict render includes `# SplitEpic Implementation-First Guidance`.
- [ ] Strict render includes `# SplitEpic Auxiliary Artifact Limits`.
- [ ] Strict render sanctions the split bundle, split plan, and child epic file sections.
- [ ] Strict render does not treat child epic files as invalid auxiliary artifacts.
- [ ] Allowed render omits strict sections and leaves no placeholders.
- [ ] Allowed render does not weaken bundle requirements.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] Transition identity mode is `split-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and active section source hashes.

### Regression Tests

Regression tests must keep passing for:

- [ ] Prompt contract writer/parser boundaries.
- [ ] Bundle extraction.
- [ ] Split bundle interpretation.
- [ ] Split family persistence.
- [ ] HITL capture.
- [ ] Selected-child promotion.
- [ ] Epic validation.

## Acceptance

- [ ] `SplitEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `split-epic-prompt-owned-v1`.
- [ ] The split bundle remains the sole sanctioned operational output boundary for the transition.
