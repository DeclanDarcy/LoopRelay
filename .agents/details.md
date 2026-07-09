# Roadmap Plan Gap Details

This file supplements `.agents/plan.md`. It captures only implementation details that are shared across the roadmap prompt-owned migration work.

Milestone-specific prompt contracts, placeholder names, mode strings, source-hash keys, fixtures, regression surfaces, and verification commands belong in `.agents/milestones/m*.md`. Duplicate details across milestone files when a requirement applies to more than one milestone; do not keep non-universal detail here just to avoid duplication.

## Source Notes

Reviewed sources:

- `.agents/plan.md`
- `.agents/specs/roadmap.md`
- `.agents/specs/milestone-deep-dives-index.md`
- `.agents/specs/m1-migrate-realign-epic-deep-dive.md`
- `.agents/specs/m2-migrate-reimagine-epic-deep-dive.md`
- `.agents/specs/m3-migrate-generate-milestone-deep-dives-for-epic-deep-dive.md`
- `.agents/specs/m4-migrate-split-epic-deep-dive.md`
- `.agents/specs/m5-retirement-checkpoint-and-regression-hardening-deep-dive.md`

Unavailable source:

- The deep dives cite `.agents/specs/audit.md`, but that file is not present in the current workspace. Audit-specific observations beyond what the roadmap and deep dives quote cannot be filled here.

## Universal Gaps Filled

- Shared runtime policy selection rules for prompt-owned roadmap artifact-authoring prompts.
- Shared strict versus allowed auxiliary-artifact behavior.
- Shared render, selector, identity, source-hash, and no-fallback invariants.
- Shared section text ownership and placeholder-handling rules.
- Shared primary artifact rule: contracted outputs are never invalid auxiliary artifacts.
- Shared failure modes that should drive tests and diagnostics across migrated prompts.

## Common Migration Contracts

### Runtime Selection

- `RoadmapPromptCatalog.RenderRuntime(...)` must route each migrated prompt through a prompt-specific private render helper.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(...)` is the single runner decision point for whether `RoadmapPromptRunner` appends `ImplementationFirstPromptPolicyComposer`.
- Prompt-owned roadmap artifact-authoring prompts must run without the legacy composer.
- Out-of-scope prompts must still receive the legacy composer until separately migrated.
- Prompt-owned render or identity failures must fail before agent execution. They must not fall back to appending the legacy composer.

### Prompt-Specific Selector Shape

Each newly migrated prompt gets a selector in `src/LoopRelay.Roadmap.Cli/Services/Prompts` with the same conceptual shape as `CreateNewEpicPromptSections`:

- An internal sealed record containing selected implementation-first guidance text, selected auxiliary-artifact limits text, and `IReadOnlyDictionary<string, string> ActiveSectionSourceHashes`.
- `ForAuxiliaryArtifactPolicy(bool allowAuxiliaryNonImplementationFiles)`.
- Strict mode, with `allowAuxiliaryNonImplementationFiles=false`, returns both generated section texts and both section `SourceHash` values.
- Allowed auxiliary mode, with `allowAuxiliaryNonImplementationFiles=true`, returns empty section strings and an empty active source-hash map.
- Selection uses only `AllowAuxiliaryNonImplementationFiles`.
- `AllowHitlRequestedNonImplementationFiles` must not select or omit prompt-owned sections. It remains a legacy composer concern.

### Identity Inputs

`RoadmapRuntimePromptPolicy.CreateIdentity(promptName)` must use section selection equivalent to rendering.

For each prompt-owned branch:

- `Mode` is the prompt-specific mode string named by the owning milestone.
- Inputs include `allowAuxiliaryNonImplementationFiles`.
- Inputs include the generated planning prompt source hash under the prompt-specific key named by the owning milestone.
- Inputs include `sectionMode=strict` when section text is active.
- Inputs include `sectionMode=omitted` when allowed auxiliary mode omits strict sections.
- Strict mode includes `section.{SectionName}.sourceHash` entries for both active sections named by the owning milestone.
- Allowed mode includes no active `section.*.sourceHash` entries.
- Input keys must be deterministic and sorted by the identity implementation.

Legacy identity remains `legacy-implementation-first-composer-v1` and includes the composed legacy policy text hash, referred to in tests as `legacyImplementationFirstPromptPolicyHash`.

### Placeholder Handling

- Use explicit placeholders in the owning planning prompt only where selected generated section text should appear.
- Prompt-specific placeholder names are owned by the milestone that migrates that prompt.
- Strict and allowed renders must leave no raw placeholder tokens.

### Section Text Ownership

- Section body text must live in `.prompt` files under `src/LoopRelay.Core/Prompts/NonImplementation`.
- Do not move section body text into C# string literals.
- Do not copy `CreateNewEpic` section text unchanged into another prompt.
- Do not reuse `InvalidContent.prompt` unchanged for prompt-specific migrations.
- Section text should preserve the owning prompt's reasoning model and artifact contract, not replicate the legacy composer generically.

### Primary Artifact Rule

- Strict auxiliary-artifact policy must never suppress or weaken a prompt's primary contracted output.
- Each milestone file names the primary contracted output for its prompt.
- Allowed auxiliary mode omits strict prompt-owned policy sections, but it does not weaken primary output requirements or permit narrative substitutes.
- Primary contracted outputs are never invalid auxiliary artifacts, even when the files are planning-oriented or non-code artifacts.

### Component Boundaries

- Preserve existing writer, parser, promotion, validation, transition, and prompt contract boundaries unless a blocking pre-existing bug is exposed.
- Milestone files name the concrete boundaries that must remain unchanged for each migrated prompt.
- Keep `ImplementationFirstPromptPolicyComposer` available for out-of-scope consumers.

## Cross-Milestone Failure Modes

These recurring failure modes should drive test names and diagnostics:

- Strict sections are not injected.
- Allowed mode leaves raw placeholders.
- Legacy composer is appended to a prompt-owned prompt.
- A legacy control prompt stops receiving the composer.
- Identity still uses the legacy mode for a migrated prompt.
- Strict identity omits the prompt source hash or active section source hashes.
- Allowed identity equals strict identity.
- `AllowHitlRequestedNonImplementationFiles` affects prompt-owned section selection.
- Section body text is hard-coded in C#.
- `CreateNewEpic` or `InvalidContent.prompt` text is copied unchanged.
- Primary contracted artifacts are treated as auxiliary artifacts.
- Allowed auxiliary mode weakens primary artifact contracts.
- Existing writer, parser, promotion, validator, transition, or prompt contract boundaries drift.

## Final Verification

Run the focused command named by each milestone after that milestone is implemented, then run the aggregate checkpoint command named by Milestone 5.

Before declaring the roadmap complete, run:

```powershell
dotnet test LoopRelay.slnx
```

The roadmap is complete only for the in-scope roadmap artifact-authoring prompts. The broader codebase still intentionally carries `ImplementationFirstPromptPolicyComposer` for out-of-scope consumers.
