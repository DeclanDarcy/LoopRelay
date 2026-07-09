# Milestone 2: Migrate `ReimagineEpic`

## Goal

- [x] Make `ReimagineEpic` prompt-owned for implementation-first policy while preserving audit-grounded full replacement behavior for `.agents/epic.md`.

## Extracted Details

### Gap Filled

- [x] Preserve the deep-dive guardrail that full replacement is valid only when audit-grounded, not as a free-form design exercise.

## Code Changes

### Add

- [x] `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`
- [x] `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/ReimagineEpicPromptSections.cs`

### Modify

- [x] `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [x] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Guardrails

- [x] Keep the existing active epic rewrite transition path unchanged.
- [x] Keep the existing promotion path unchanged.
- [x] Keep the existing blocked output path unchanged.
- [x] Keep the existing validation path unchanged.
- [x] Keep the existing prompt contract path unchanged.

### Prompt Identity Inputs

- [x] Use mode `reimagine-epic-prompt-owned-v1`.
- [x] Include prompt source hash key `reimagineEpicSourceHash`.
- [x] In strict mode, include `sectionMode=strict`.
- [x] In strict mode, include active section source hash `ReimagineEpicImplementationFirstGuidance`.
- [x] In strict mode, include active section source hash `ReimagineEpicAuxiliaryArtifactLimits`.
- [x] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [x] Use `{reimagineEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [x] Use `{reimagineEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [x] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [x] Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
- [x] The strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
- [x] Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
- [x] Every material redesign decision should trace to audit findings, projection context, or repository-grounded capability needs already supplied to the prompt.
- [x] Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files are invalid auxiliary artifacts.
- [x] `# Epic Reimagination Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [x] Runtime prompt remains the audit-grounded full replacement prompt for `.agents/epic.md`.
- [x] Blocked response remains `# Epic Reimagination Blocked`.
- [x] Strict render contains `# ReimagineEpic Implementation-First Guidance` and `# ReimagineEpic Auxiliary Artifact Limits`.
- [x] Allowed auxiliary render omits strict sections without removing the required `.agents/epic.md` replacement output.
- [x] `RoadmapPromptRunner` sends `ReimagineEpic` without the legacy composer heading.
- [x] Prompt contract registry remains unchanged:
  - input `.agents/epic.md`
  - output `.agents/epic.md`
  - decision `Reimagine`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

## Tests

Add focused tests covering:

- [x] `ReimagineEpicPromptSections` strict and allowed behavior.
- [x] Strict section selection markers.
- [x] Allowed branch placeholder removal.
- [x] Strict render sanctions replacement `.agents/epic.md`.
- [x] Strict render preserves coherent replacement milestone roadmap language.
- [x] Strict render does not imply side design reports.
- [x] Strict render does not imply raw audit appendices.
- [x] Runtime prompt contains no legacy composer heading.
- [x] A legacy control prompt still receives the composer.
- [x] Transition identity mode is `reimagine-epic-prompt-owned-v1`.
- [x] Strict identity includes prompt and section source hashes.
- [x] Strict and allowed identities differ.
- [x] Section body text is not hard-coded in C#.
- [x] Existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests still pass.

## Fixtures

- [x] Project context plus audit input requiring full epic replacement.
- [x] Allowed auxiliary fixture with the same inputs.
- [x] Invalid auxiliary terms: companion design reports, rationale appendices, research notes, architecture proposals, governance notes, raw audit-detail files.
- [x] Blocked output: `# Epic Reimagination Blocked`.
- [x] Legacy control prompt: an unmigrated roadmap runtime prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~ReimagineEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Acceptance

- [x] `ReimagineEpic` renders and runs without the legacy composer.
- [x] Transition snapshots identify `reimagine-epic-prompt-owned-v1`.
- [x] Full epic replacement remains audit-grounded through the existing transition and promotion path.
