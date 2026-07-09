# Milestone 1: Migrate `RealignEpic`

## Goal

- [x] Make `RealignEpic` prompt-owned for implementation-first policy while preserving audit-driven minimal patch behavior for `.agents/epic.md`.

## Extracted Details

### Gap Filled

- [x] Preserve the precise minimal-realignment semantics, unchanged active-epic contracts, and focused test fixtures from the roadmap deep dive.

## Code Changes

### Add

- [x] `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`
- [x] `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RealignEpicPromptSections.cs`

### Modify

- [x] `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Guardrails

- [x] Verify no changes are needed to `ActiveEpicRewriteTransition` unless a blocking bug is exposed.
- [x] Verify no changes are needed to `ActiveEpicPromotionCoordinator` unless a blocking bug is exposed.
- [x] Verify no changes are needed to `EpicAuthoringOutputClassifier` unless a blocking bug is exposed.
- [x] Verify no changes are needed to `EpicArtifactValidator` unless a blocking bug is exposed.
- [x] Verify no changes are needed to `PromptContractRegistry` unless a blocking bug is exposed.

### Prompt Identity Inputs

- [x] Use mode `realign-epic-prompt-owned-v1`.
- [x] Include prompt source hash key `realignEpicSourceHash`.
- [x] In strict mode, include `sectionMode=strict`.
- [x] In strict mode, include active section source hash `RealignEpicImplementationFirstGuidance`.
- [x] In strict mode, include active section source hash `RealignEpicAuxiliaryArtifactLimits`.
- [x] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [x] Use `{realignEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [x] Use `{realignEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [x] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [x] Updating `.agents/epic.md` is the sanctioned implementation-bearing output.
- [x] Realignment is a minimal strategic patch, not a replacement epic.
- [x] Epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content must be preserved.
- [x] Audit findings may be compressed into scope, constraints, dependencies, assumptions, acceptance criteria, risks, and follow-up only when they affect implemented capability.
- [x] Unaffected sections must not be rewritten for style or completeness.
- [x] Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents are invalid auxiliary artifacts.
- [x] `# Epic Realignment Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [x] Runtime prompt remains the audit-driven `.agents/epic.md` minimal realignment prompt.
- [x] Blocked response remains `# Epic Realignment Blocked`.
- [x] Strict render contains `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- [x] Allowed auxiliary render omits both strict sections and leaves no placeholders.
- [x] `RoadmapPromptRunner` sends `RealignEpic` without the legacy composer heading.
- [x] Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/epic.md`
  - decision `Realign`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

## Tests

### Prompt Tests

Add tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`, beside `CreateNewEpicPromptPolicyTests`, covering:

- [x] `RealignEpicPromptSections` strict and allowed behavior.
- [x] Strict selection includes `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- [x] Allowed auxiliary mode omits both strict sections and leaves no placeholders.
- [x] Strict render sanctions `.agents/epic.md`.
- [x] Strict render preserves existing milestone roadmap content.
- [x] Runtime runner does not append `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [x] A legacy control prompt still receives the composer.
- [x] No `# Invalid Content` legacy injection appears in the prompt-owned render.
- [x] Section body text is not hard-coded in C# files.

### Transition Identity Tests

Extend transition identity coverage under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`:

- [x] Strict and allowed identities differ.
- [x] Strict mode is `realign-epic-prompt-owned-v1`.
- [x] Strict identity includes the `RealignEpic` prompt source hash and active section source hashes.
- [x] Allowed identity records omitted section mode and no active section hashes.

### Contract And Regression Tests

- [x] Prompt contract registry still declares required input `.agents/epic.md`.
- [x] Prompt contract registry still declares required output `.agents/epic.md`.
- [x] Prompt contract registry still declares decision `Realign`.
- [x] Prompt contract registry still declares writer `ArtifactPromotionService`.
- [x] Prompt contract registry still declares parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`.
- [x] Existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests still pass.

## Fixtures

- [x] Project context: `"project context"`.
- [x] Secondary audit input: a small audit requiring minimal `.agents/epic.md` correction.
- [x] Strict policy: `AllowAuxiliaryNonImplementationFiles=false`.
- [x] Allowed policy: same inputs with `AllowAuxiliaryNonImplementationFiles=true`.
- [x] Invalid side-artifact terms: repository re-audit reports, raw audit summaries, rationale appendices, governance notes, research notes, companion design documents.
- [x] Blocked output: `# Epic Realignment Blocked`.
- [x] Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~RealignEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Acceptance

- [x] `RealignEpic` renders and runs without the legacy composer.
- [x] Transition snapshots identify `realign-epic-prompt-owned-v1`.
- [x] Existing active-epic rewrite, promotion, validation, blocked-output, and prompt contract tests remain passing.
