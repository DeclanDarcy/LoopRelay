# Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

## Goal

- [ ] Make `GenerateMilestoneDeepDivesForEpic` prompt-owned while preserving `.agents/specs/*.md` as the required primary machine-consumed implementation-planning output bundle.

## Extracted Details

### Gap Filled

- [ ] Preserve the semantic distinction that `.agents/specs/*.md` files are planning artifacts but also primary machine-consumed implementation-preparation outputs, not prohibited auxiliary artifacts.

## Code Changes

### Add

- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`
- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/GenerateMilestoneDeepDivesForEpicPromptSections.cs`

### Modify

- [ ] `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Preserve Existing Behavior

- [ ] `GenerateMilestoneDeepDivesTransition`
- [ ] `BundleFileExtractor`
- [ ] `BundleManifestWriter`
- [ ] Execution preparation provenance services
- [ ] HITL evidence capture
- [ ] `InvariantValidator`
- [ ] Prompt contract registry semantics

### Prompt Identity Inputs

- [ ] Use mode `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] Include prompt source hash key `generateMilestoneDeepDivesForEpicSourceHash`.
- [ ] In strict mode, include `sectionMode=strict`.
- [ ] In strict mode, include active section source hash `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`.
- [ ] In strict mode, include active section source hash `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits`.
- [ ] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [ ] Use `{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [ ] Use `{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [ ] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [ ] The primary output bundle under `.agents/specs/*.md` is not auxiliary output.
- [ ] One deep-dive spec per epic milestone remains required in strict and allowed auxiliary modes.
- [ ] Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution stages.
- [ ] Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
- [ ] Allowed auxiliary mode must not permit narrative output to replace the contracted spec bundle.
- [ ] Blocking or partial generation must use the existing output protocol and must not create side-channel explanations.

## Public Contract

- [ ] Runtime prompt remains the prompt for generating milestone specs from the active epic.
- [ ] Required input remains `.agents/epic.md`.
- [ ] Required output remains `.agents/specs`.
- [ ] Output bundle still uses `# FILE:` sections parsed by `BundleFileExtractor`.
- [ ] Both strict and allowed renders require `.agents/specs/*.md` as primary contracted output.
- [ ] Strict render contains `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance` and `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary render omits strict sections and leaves no placeholders.
- [ ] Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/specs`
  - decision `Generate Specs`
  - writer `SpecBundleWriter`
  - parser `BundleFileExtractor`

## Tests

### Focused Tests

Add focused tests covering:

- [ ] `GenerateMilestoneDeepDivesForEpicPromptSections` strict and allowed behavior.
- [ ] Strict render includes `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance`.
- [ ] Strict render includes `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- [ ] Strict and allowed renders both require `.agents/specs/*.md`.
- [ ] Allowed render omits strict sections and leaves no placeholders.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] A legacy control prompt still receives the composer.
- [ ] Transition identity mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and active section source hashes.
- [ ] Strict and allowed identities differ.
- [ ] `.agents/specs/*.md` is treated as a primary contracted output, not auxiliary output.
- [ ] Prompt contract registry still declares `.agents/specs` and `BundleFileExtractor`.
- [ ] No `# Invalid Content` legacy injection appears in the prompt-owned render.
- [ ] Section body text is not hard-coded in C#.

### Regression Tests

Regression tests must keep passing for:

- [ ] Bundle extraction.
- [ ] Path validation.
- [ ] Bundle manifest writing.
- [ ] Spec materialization.
- [ ] Execution preparation provenance.
- [ ] HITL artifact capture.
- [ ] Invariant validation.
- [ ] Malformed bundle failure.
- [ ] Invariant failure not marking specs ready.
- [ ] Roadmap state transition to milestone specs ready.

## Fixtures

- [ ] Minimal valid active epic context with two milestones and ordered dependencies.
- [ ] Valid output bundle with two `# FILE: .agents/specs/*.md` sections.
- [ ] Invalid narrative-only output with no `# FILE:` sections.
- [ ] Invalid auxiliary output containing design essay, code-edit script, execution prompt, governance report, research note, or companion planning document outside the bundle.
- [ ] Invariant mismatch fixture where generated spec names or contents do not match the active epic.
- [ ] Strict and allowed policy fixtures.
- [ ] Legacy control prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~GenerateMilestoneDeepDivesForEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~BundleFileExtractorTests|FullyQualifiedName~InvariantValidatorTests"
```

## Acceptance

- [ ] `GenerateMilestoneDeepDivesForEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] `.agents/specs/*.md` generation remains required and machine-consumed in both strict and allowed auxiliary modes.
