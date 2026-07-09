# Milestone 2: Migrate `ReimagineEpic`

## Goal

- [ ] Make `ReimagineEpic` prompt-owned for implementation-first policy while preserving audit-grounded full replacement behavior for `.agents/epic.md`.

## Extracted Details

### Gap Filled

- [ ] Preserve the deep-dive guardrail that full replacement is valid only when audit-grounded, not as a free-form design exercise.

## Code Changes

### Add

- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`
- [ ] `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/ReimagineEpicPromptSections.cs`

### Modify

- [ ] `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- [ ] `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

### Guardrails

- [ ] Keep the existing active epic rewrite transition path unchanged.
- [ ] Keep the existing promotion path unchanged.
- [ ] Keep the existing blocked output path unchanged.
- [ ] Keep the existing validation path unchanged.
- [ ] Keep the existing prompt contract path unchanged.

### Prompt Identity Inputs

- [ ] Use mode `reimagine-epic-prompt-owned-v1`.
- [ ] Include prompt source hash key `reimagineEpicSourceHash`.
- [ ] In strict mode, include `sectionMode=strict`.
- [ ] In strict mode, include active section source hash `ReimagineEpicImplementationFirstGuidance`.
- [ ] In strict mode, include active section source hash `ReimagineEpicAuxiliaryArtifactLimits`.
- [ ] In allowed auxiliary mode, include `sectionMode=omitted` and no active `section.*.sourceHash` entries.

### Planning Prompt Placeholders

- [ ] Use `{reimagineEpicImplementationFirstGuidance}` for selected implementation-first guidance.
- [ ] Use `{reimagineEpicAuxiliaryArtifactLimits}` for selected auxiliary artifact limits.
- [ ] Verify strict and allowed renders leave no raw placeholder tokens.

## Section Semantics

The strict guidance must state that:

- [ ] Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
- [ ] The strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
- [ ] Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
- [ ] Every material redesign decision should trace to audit findings, projection context, or repository-grounded capability needs already supplied to the prompt.
- [ ] Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files are invalid auxiliary artifacts.
- [ ] `# Epic Reimagination Blocked` does not authorize extra explanatory artifacts.

## Public Contract

- [ ] Runtime prompt remains the audit-grounded full replacement prompt for `.agents/epic.md`.
- [ ] Blocked response remains `# Epic Reimagination Blocked`.
- [ ] Strict render contains `# ReimagineEpic Implementation-First Guidance` and `# ReimagineEpic Auxiliary Artifact Limits`.
- [ ] Allowed auxiliary render omits strict sections without removing the required `.agents/epic.md` replacement output.
- [ ] `RoadmapPromptRunner` sends `ReimagineEpic` without the legacy composer heading.
- [ ] Prompt contract registry remains unchanged:
  - input `.agents/epic.md`
  - output `.agents/epic.md`
  - decision `Reimagine`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

## Tests

Add focused tests covering:

- [ ] `ReimagineEpicPromptSections` strict and allowed behavior.
- [ ] Strict section selection markers.
- [ ] Allowed branch placeholder removal.
- [ ] Strict render sanctions replacement `.agents/epic.md`.
- [ ] Strict render preserves coherent replacement milestone roadmap language.
- [ ] Strict render does not imply side design reports.
- [ ] Strict render does not imply raw audit appendices.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] A legacy control prompt still receives the composer.
- [ ] Transition identity mode is `reimagine-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and section source hashes.
- [ ] Strict and allowed identities differ.
- [ ] Section body text is not hard-coded in C#.
- [ ] Existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests still pass.

## Fixtures

- [ ] Project context plus audit input requiring full epic replacement.
- [ ] Allowed auxiliary fixture with the same inputs.
- [ ] Invalid auxiliary terms: companion design reports, rationale appendices, research notes, architecture proposals, governance notes, raw audit-detail files.
- [ ] Blocked output: `# Epic Reimagination Blocked`.
- [ ] Legacy control prompt: an unmigrated roadmap runtime prompt.

## Verification Command

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~ReimagineEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Acceptance

- [ ] `ReimagineEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `reimagine-epic-prompt-owned-v1`.
- [ ] Full epic replacement remains audit-grounded through the existing transition and promotion path.
