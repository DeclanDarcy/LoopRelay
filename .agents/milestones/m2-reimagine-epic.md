# Milestone 2: Migrate `ReimagineEpic`

## Goal

- [ ] Make `ReimagineEpic` prompt-owned for implementation-first policy while preserving audit-grounded full replacement behavior for `.agents/epic.md`.

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

## Section Semantics

The strict guidance must state that:

- [ ] Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
- [ ] The strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
- [ ] Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
- [ ] Every material redesign decision should trace to audit findings, projection context, or repository-grounded capability needs already supplied to the prompt.
- [ ] Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files are invalid auxiliary artifacts.
- [ ] `# Epic Reimagination Blocked` does not authorize extra explanatory artifacts.

## Tests

Add focused tests covering:

- [ ] Strict section selection markers.
- [ ] Allowed branch placeholder removal.
- [ ] Strict render sanctions replacement `.agents/epic.md` and a coherent replacement milestone roadmap.
- [ ] Strict render does not imply side design reports or raw audit appendices.
- [ ] Runtime prompt contains no legacy composer heading.
- [ ] Transition identity mode is `reimagine-epic-prompt-owned-v1`.
- [ ] Strict identity includes prompt and section source hashes.
- [ ] Section body text is not hard-coded in C#.

## Acceptance

- [ ] `ReimagineEpic` renders and runs without the legacy composer.
- [ ] Transition snapshots identify `reimagine-epic-prompt-owned-v1`.
- [ ] Full epic replacement remains audit-grounded through the existing transition and promotion path.
