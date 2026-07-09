# Milestone 1: Migrate `RealignEpic`

## Goal

- [ ] Make `RealignEpic` prompt-owned for implementation-first policy while preserving audit-driven minimal patch behavior for `.agents/epic.md`.

## Extracted Details

### Gap Filled

- [ ] Preserve the precise minimal-realignment semantics, unchanged active-epic contracts, and focused test fixtures from the roadmap deep dive.

## Code Changes

### Add

- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`
- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RealignEpicPromptSections.cs`

### Modify

- [ ] `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Guardrails

- [ ] Verify no changes are needed to `ActiveEpicRewriteTransition` unless a blocking bug is exposed.
- [ ] Verify no changes are needed to `ActiveEpicPromotionCoordinator` unless a blocking bug is exposed.
- [ ] Verify no changes are needed to `EpicAuthoringOutputClassifier` unless a blocking bug is exposed.
- [ ] Verify no changes are needed to `EpicArtifactValidator` unless a blocking bug is exposed.
- [ ] Verify no changes are needed to `PromptContractRegistry` unless a blocking bug is exposed.

### Prompt Identity Inputs

- [ ] Use mode `realign-epic-prompt-owned-v1`.
- [ ] Include prompt source hash key `realignEpicSourceHash`.
- [ ] In strict mode, include `sectionMode=strict`.
- [ ] In strict mode, include active section source hash `RealignEpicImplementationFirstGuidance`.
- [ ] In strict mode, include active section source hash `RealignEpicAuxiliaryArtifactLimits`.
- [ ] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [ ] Use `{realignEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [ ] Use `{realignEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [ ] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [ ] Updating `.agents/epic.md` is the sanctioned implementation-bearing output.
- [ ] Realignment is a minimal strategic patch, not a replacement epic.
- [ ] Epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content must be preserved.
- [ ] Audit findings may be compressed into scope, constraints, dependencies, assumptions, acceptance criteria, risks, and follow-up only when they affect implemented capability.
- [ ] Unaffected sections must not be rewritten for style or completeness.
- [ ] Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents are invalid auxiliary artifacts.
- [ ] `# Epic Realignment Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [ ] Runtime prompt remains the audit-driven `.agents/epic.md` minimal realignment prompt.
- [ ] Blocked response remains `# Epic Realignment Blocked`.
- [ ] Strict render contains `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary render omits both strict sections and leaves no placeholders.
- [ ] `RoadmapPromptRunner` sends `RealignEpic` without the legacy composer heading.
- [ ] Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/epic.md`
  - decision `Realign`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

## Tests

### Prompt Tests

Add tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`, beside `CreateNewEpicPromptPolicyTests`, covering:

- [ ] `RealignEpicPromptSections` strict and allowed behavior.
- [ ] Strict selection includes `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary mode omits both strict sections and leaves no placeholders.
- [ ] Strict render sanctions `.agents/epic.md`.
- [ ] Strict render preserves existing milestone roadmap content.
- [ ] Runtime runner does not append `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] A legacy control prompt still receives the composer.
- [ ] No `# Invalid Content` legacy injection appears in the prompt-owned render.
- [ ] Section body text is not hard-coded in C# files.

### Transition Identity Tests

Extend transition identity coverage under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`:

- [ ] Strict and allowed identities differ.
- [ ] Strict mode is `realign-epic-prompt-owned-v1`.
- [ ] Strict identity includes the `RealignEpic` prompt source hash and active section source hashes.
- [ ] Allowed identity records omitted section mode and no active section hashes.

### Contract And Regression Tests

- [ ] Prompt contract registry still declares required input `.agents/epic.md`.
- [ ] Prompt contract registry still declares required output `.agents/epic.md`.
- [ ] Prompt contract registry still declares decision `Realign`.
- [ ] Prompt contract registry still declares writer `ArtifactPromotionService`.
- [ ] Prompt contract registry still declares parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`.
- [ ] Existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests still pass.

## Fixtures

- [ ] Project context: `"project context"`.
- [ ] Secondary audit input: a small audit requiring minimal `.agents/epic.md` correction.
- [ ] Strict policy: `AllowAuxiliaryNonImplementationFiles=false`.
- [ ] Allowed policy: same inputs with `AllowAuxiliaryNonImplementationFiles=true`.
- [ ] Invalid side-artifact terms: repository re-audit reports, raw audit summaries, rationale appendices, governance notes, research notes, companion design documents.
- [ ] Blocked output: `# Epic Realignment Blocked`.
- [ ] Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~RealignEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Acceptance

- [ ] `RealignEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `realign-epic-prompt-owned-v1`.
- [ ] Existing active-epic rewrite, promotion, validation, blocked-output, and prompt contract tests remain passing.
