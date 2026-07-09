# Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

## Goal

- [x] Make `GenerateMilestoneDeepDivesForEpic` prompt-owned while preserving `.agents/specs/*.md` as the required primary machine-consumed implementation-planning output bundle.

## Extracted Details

### Gap Filled

- [x] Preserve the semantic distinction that `.agents/specs/*.md` files are planning artifacts but also primary machine-consumed implementation-preparation outputs, not prohibited auxiliary artifacts.

## Code Changes

### Add

- [x] `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`
- [x] `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/GenerateMilestoneDeepDivesForEpicPromptSections.cs`

### Modify

- [x] `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Preserve Existing Behavior

- [x] `GenerateMilestoneDeepDivesTransition`
- [x] `BundleFileExtractor`
- [x] `BundleManifestWriter`
- [x] Execution preparation provenance services
- [x] HITL evidence capture
- [x] `InvariantValidator`
- [x] Prompt contract registry semantics

### Prompt Identity Inputs

- [x] Use mode `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [x] Include prompt source hash key `generateMilestoneDeepDivesForEpicSourceHash`.
- [x] In strict mode, include `sectionMode=strict`.
- [x] In strict mode, include active section source hash `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`.
- [x] In strict mode, include active section source hash `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits`.
- [x] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [x] Use `{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [x] Use `{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [x] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [x] The primary output bundle under `.agents/specs/*.md` is not auxiliary output.
- [x] One deep-dive spec per epic milestone remains required in strict and allowed auxiliary modes.
- [x] Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution stages.
- [x] Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
- [x] Allowed auxiliary mode must not permit narrative output to replace the contracted spec bundle.
- [x] Blocking or partial generation must use the existing output protocol and must not create side-channel explanations.

## Public Contract

- [x] Runtime prompt remains the prompt for generating milestone specs from the active epic.
- [x] Required input remains `.agents/epic.md`.
- [x] Required output remains `.agents/specs`.
- [x] Output bundle still uses `# FILE:` sections parsed by `BundleFileExtractor`.
- [x] Both strict and allowed renders require `.agents/specs/*.md` as primary contracted output.
- [x] Strict render contains `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance` and `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- [x] Allowed auxiliary render omits strict sections and leaves no placeholders.
- [x] Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/specs`
  - decision `Generate Specs`
  - writer `SpecBundleWriter`
  - parser `BundleFileExtractor`

## Tests

### Focused Tests

Add focused tests covering:

- [x] `GenerateMilestoneDeepDivesForEpicPromptSections` strict and allowed behavior.
- [x] Strict render includes `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance`.
- [x] Strict render includes `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- [x] Strict and allowed renders both require `.agents/specs/*.md`.
- [x] Allowed render omits strict sections and leaves no placeholders.
- [x] Runtime prompt contains no legacy composer heading.
- [x] A legacy control prompt still receives the composer.
- [x] Transition identity mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [x] Strict identity includes prompt and active section source hashes.
- [x] Strict and allowed identities differ.
- [x] `.agents/specs/*.md` is treated as a primary contracted output, not auxiliary output.
- [x] Prompt contract registry still declares `.agents/specs` and `BundleFileExtractor`.
- [x] No `# Invalid Content` legacy injection appears in the prompt-owned render.
- [x] Section body text is not hard-coded in C#.

### Regression Tests

Regression tests must keep passing for:

- [x] Bundle extraction.
- [x] Path validation.
- [x] Bundle manifest writing.
- [x] Spec materialization.
- [x] Execution preparation provenance.
- [x] HITL artifact capture.
- [x] Invariant validation.
- [x] Malformed bundle failure.
- [x] Invariant failure not marking specs ready.
- [x] Roadmap state transition to milestone specs ready.

## Fixtures

- [x] Minimal valid active epic context with two milestones and ordered dependencies.
- [x] Valid output bundle with two `# FILE: .agents/specs/*.md` sections.
- [x] Invalid narrative-only output with no `# FILE:` sections.
- [x] Invalid auxiliary output containing design essay, code-edit script, execution prompt, governance report, research note, or companion planning document outside the bundle.
- [x] Invariant mismatch fixture where generated spec names or contents do not match the active epic.
- [x] Strict and allowed policy fixtures.
- [x] Legacy control prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~GenerateMilestoneDeepDivesForEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~BundleFileExtractorTests|FullyQualifiedName~InvariantValidatorTests"
```

## Acceptance

- [x] `GenerateMilestoneDeepDivesForEpic` renders and runs without the legacy composer.
- [x] Transition snapshots identify `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [x] `.agents/specs/*.md` generation remains required and machine-consumed in both strict and allowed auxiliary modes.
