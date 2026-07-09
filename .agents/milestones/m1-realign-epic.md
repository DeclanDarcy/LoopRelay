# Milestone 1: Migrate `RealignEpic`

## Goal

- [ ] Make `RealignEpic` prompt-owned for implementation-first policy while preserving audit-driven minimal patch behavior for `.agents/epic.md`.

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

## Section Semantics

The strict guidance must state that:

- [ ] Updating `.agents/epic.md` is the sanctioned implementation-bearing output.
- [ ] Realignment is a minimal strategic patch, not a replacement epic.
- [ ] Epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content must be preserved.
- [ ] Audit findings may be compressed into scope, constraints, dependencies, assumptions, acceptance criteria, risks, and follow-up only when they affect implemented capability.
- [ ] Unaffected sections must not be rewritten for style or completeness.
- [ ] Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents are invalid auxiliary artifacts.
- [ ] `# Epic Realignment Blocked` does not authorize extra explanatory artifacts.

## Tests

### Prompt Tests

Add tests under `tests/LoopRelay.Roadmap.Cli.Tests/Services/Prompts`, beside `CreateNewEpicPromptPolicyTests`, covering:

- [ ] Strict selection includes `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary mode omits both strict sections and leaves no placeholders.
- [ ] Strict render sanctions `.agents/epic.md` and preserving existing milestone roadmap content.
- [ ] Runtime runner does not append `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- [ ] A legacy control prompt still receives the composer.
- [ ] Section body text is not hard-coded in C# files.

### Transition Identity Tests

Extend transition identity coverage under `tests/LoopRelay.Roadmap.Cli.Tests/Services/TransitionState`:

- [ ] Strict and allowed identities differ.
- [ ] Strict mode is `realign-epic-prompt-owned-v1`.
- [ ] Strict identity includes the `RealignEpic` prompt source hash and active section source hashes.
- [ ] Allowed identity records omitted section mode and no active section hashes.

## Acceptance

- [ ] `RealignEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `realign-epic-prompt-owned-v1`.
- [ ] Existing active-epic rewrite, promotion, validation, blocked-output, and prompt contract tests remain passing.
