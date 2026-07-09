# Milestone 4: Migrate `SplitEpic`

## Goal

- [ ] Make `SplitEpic` prompt-owned while preserving split bundle semantics, child epic authoring quality, split lineage persistence, HITL capture, and selected-child promotion.

## Extracted Details

### Gap Filled

- [ ] Preserve the deep-dive clarification that child epic files are primary contracted output inside the split bundle, not auxiliary artifacts.

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

### Prompt Identity Inputs

- [ ] Use mode `split-epic-prompt-owned-v1`.
- [ ] Include prompt source hash key `splitEpicSourceHash`.
- [ ] In strict mode, include `sectionMode=strict`.
- [ ] In strict mode, include active section source hash `SplitEpicImplementationFirstGuidance`.
- [ ] In strict mode, include active section source hash `SplitEpicAuxiliaryArtifactLimits`.
- [ ] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [ ] Use `{splitEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [ ] Use `{splitEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [ ] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [ ] The sanctioned output boundary is the split bundle consumed by the split transition.
- [ ] The split bundle includes a split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
- [ ] Child epic files are primary contracted outputs, not invalid auxiliary artifacts.
- [ ] The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
- [ ] Child epics should follow the `CreateNewEpic` authoring standard at the concept level.
- [ ] `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
- [ ] Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
- [ ] Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices are invalid auxiliary artifacts unless explicitly requested or required by an existing machine-consumed contract.
- [ ] `# Split Epic Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [ ] Runtime prompt remains the split proposal decomposition prompt.
- [ ] Required input remains `.agents/selection` through roadmap prompt context.
- [ ] Required outputs remain split family state and selected active epic as declared by prompt contracts.
- [ ] Output remains a multi-file Markdown bundle consumed by `BundleFileExtractor` and `SplitEpicBundleInterpreter`.
- [ ] Split plan and `# FILE: .agents/epic-N.md` child epic sections are primary contracted outputs.
- [ ] Blocked response remains `# Split Epic Blocked`.
- [ ] Strict render contains `# SplitEpic Implementation-First Guidance` and `# SplitEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary render omits strict sections and leaves no placeholders.
- [ ] Prompt contract registry remains unchanged:
  - writer boundary `SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService`
  - parser boundary `BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator`

## Tests

### Focused Tests

Add focused tests covering:

- [ ] `SplitEpicPromptSections` strict and allowed behavior.
- [ ] Strict render includes `# SplitEpic Implementation-First Guidance`.
- [ ] Strict render includes `# SplitEpic Auxiliary Artifact Limits`.
- [ ] Strict render sanctions the split bundle, split plan, and child epic file sections.
- [ ] Strict render does not treat child epic files as invalid auxiliary artifacts.
- [ ] Allowed render omits strict sections and leaves no placeholders.
- [ ] Allowed render does not weaken bundle requirements.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] A legacy control prompt still receives the composer.
- [ ] Transition identity mode is `split-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and active section source hashes.
- [ ] Strict and allowed identities differ.
- [ ] Section body text is not hard-coded in C#.
- [ ] Prompt does not encourage task, file-area, phase, or layer splits.

### Regression Tests

Regression tests must keep passing for:

- [ ] Prompt contract writer/parser boundaries.
- [ ] Bundle extraction.
- [ ] Split bundle interpretation.
- [ ] Invalid bundle rejection.
- [ ] Overlap detection.
- [ ] Missing split plan or missing child detection.
- [ ] Invalid child path rejection.
- [ ] Split family persistence.
- [ ] HITL capture.
- [ ] HITL child artifact evidence capture.
- [ ] Selected-child promotion.
- [ ] Epic validation.

## Fixtures

- [ ] Project context plus split proposal.
- [ ] Valid split bundle with split plan and at least two `# FILE: .agents/epic-N.md` child epic sections.
- [ ] Invalid task split fixture: files, phases, layers, or work items rather than capability partitions.
- [ ] Invalid auxiliary fixture: companion inventory, RFC, governance note, research document, design report, or explanatory appendix outside the contracted bundle.
- [ ] Blocked output: `# Split Epic Blocked`.
- [ ] Split lineage fixture with valid child epics, stable order, and selected child.
- [ ] Strict and allowed policy fixtures.
- [ ] Legacy control prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~SplitEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~SplitEpicBundleInterpreterTests|FullyQualifiedName~SplitFamilyStoreTests"
```

## Acceptance

- [ ] `SplitEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `split-epic-prompt-owned-v1`.
- [ ] The split bundle remains the sole sanctioned operational output boundary for the transition.
