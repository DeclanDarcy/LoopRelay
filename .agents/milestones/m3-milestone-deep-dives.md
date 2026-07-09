# Milestone 3: Migrate `GenerateMilestoneDeepDivesForEpic`

## Goal

- [ ] Make `GenerateMilestoneDeepDivesForEpic` prompt-owned while preserving `.agents/specs/*.md` as the required primary machine-consumed implementation-planning output bundle.

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

## Section Semantics

The strict guidance must state that:

- [ ] The primary output bundle under `.agents/specs/*.md` is not auxiliary output.
- [ ] One deep-dive spec per epic milestone remains required in strict and allowed auxiliary modes.
- [ ] Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution stages.
- [ ] Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
- [ ] Allowed auxiliary mode must not permit narrative output to replace the contracted spec bundle.
- [ ] Blocking or partial generation must use the existing output protocol and must not create side-channel explanations.

## Tests

### Focused Tests

Add focused tests covering:

- [ ] Strict render includes `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance`.
- [ ] Strict render includes `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- [ ] Strict and allowed renders both require `.agents/specs/*.md`.
- [ ] Allowed render omits strict sections and leaves no placeholders.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] Transition identity mode is `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and active section source hashes.
- [ ] `.agents/specs/*.md` is treated as a primary contracted output, not auxiliary output.

### Regression Tests

Regression tests must keep passing for:

- [ ] Bundle extraction.
- [ ] Bundle manifest writing.
- [ ] Execution preparation provenance.
- [ ] HITL artifact capture.
- [ ] Invariant validation.
- [ ] Roadmap state transition to milestone specs ready.

## Acceptance

- [ ] `GenerateMilestoneDeepDivesForEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- [ ] `.agents/specs/*.md` generation remains required and machine-consumed in both strict and allowed auxiliary modes.
