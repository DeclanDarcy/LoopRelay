# Roadmap Plan Gap Details

This file supplements `.agents/plan.md`. It captures implementation details from `.agents/specs/roadmap.md`, the milestone deep dives in `.agents/specs/`, and the milestone checklist files where they confirmed the same requirements.

It is not a replacement plan and does not add scope. Its purpose is to fill the meaningful gaps in `.agents/plan.md`: prompt-specific section semantics, preserved contracts, identity inputs, test fixtures, failure modes, and unchanged component boundaries.

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

## Meaningful Gaps Filled

- Per-prompt section semantics: what each implementation-first guidance section and auxiliary-artifact limit section must say.
- Per-prompt public contracts: primary inputs, outputs, blocked responses, writer/parser boundaries, and primary artifact status.
- Internal render/selector/identity contracts: strict versus allowed auxiliary behavior, source-hash keys, and no fallback to the legacy composer.
- File and module impact: files to add or modify and components that must remain unchanged.
- Focused tests, fixtures, and regression targets for every milestone.
- Failure modes and earliest detection points.
- Final checkpoint matrix for all migrated prompts, including `CreateNewEpic`.

## Common Migration Contracts

### Runtime Selection

- `RoadmapPromptCatalog.RenderRuntime(...)` must route every migrated prompt through a prompt-specific private render helper.
- `RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(...)` is the single runner decision point for whether `RoadmapPromptRunner` appends `ImplementationFirstPromptPolicyComposer`.
- `RoadmapPromptRunner` must skip the legacy composer for:
  - `CreateNewEpic`
  - `RealignEpic`
  - `ReimagineEpic`
  - `GenerateMilestoneDeepDivesForEpic`
  - `SplitEpic`
- Out-of-scope prompts, such as `SelectNextEpic`, must still receive the legacy composer until separately migrated.
- Prompt-owned render or identity failures must fail before agent execution. They must not fall back to appending the legacy composer.

### Prompt-Specific Selector Shape

Each newly migrated prompt gets a selector in `src/LoopRelay.Roadmap.Cli/Services/Prompts` with the same conceptual shape as `CreateNewEpicPromptSections`:

- An internal sealed record containing:
  - selected implementation-first guidance text
  - selected auxiliary-artifact limits text
  - `IReadOnlyDictionary<string, string> ActiveSectionSourceHashes`
- `ForAuxiliaryArtifactPolicy(bool allowAuxiliaryNonImplementationFiles)`
- Strict mode, with `allowAuxiliaryNonImplementationFiles=false`, returns both generated section texts and both section `SourceHash` values.
- Allowed auxiliary mode, with `allowAuxiliaryNonImplementationFiles=true`, returns empty section strings and an empty active source-hash map.
- Selection uses only `AllowAuxiliaryNonImplementationFiles`.
- `AllowHitlRequestedNonImplementationFiles` must not select or omit prompt-owned sections. It remains a legacy composer concern.

### Identity Inputs

`RoadmapRuntimePromptPolicy.CreateIdentity(promptName)` must use section selection equivalent to rendering.

For each prompt-owned branch:

- `Mode` is the prompt-specific mode string.
- Inputs include `allowAuxiliaryNonImplementationFiles`.
- Inputs include the generated planning prompt source hash under the prompt-specific key.
- Inputs include `sectionMode=strict` when section text is active.
- Inputs include `sectionMode=omitted` when allowed auxiliary mode omits strict sections.
- Strict mode includes `section.{SectionName}.sourceHash` entries for both active sections.
- Allowed mode includes no active `section.*.sourceHash` entries.
- Input keys must be deterministic and sorted by the identity implementation.

Legacy identity remains `legacy-implementation-first-composer-v1` and includes the composed legacy policy text hash, referred to in tests as `legacyImplementationFirstPromptPolicyHash`.

| Runtime Prompt | Mode | Prompt Source Hash Key | Strict Section Source Hash Keys |
|---|---|---|---|
| `CreateNewEpic` | `create-new-epic-prompt-owned-v1` | existing `CreateNewEpic` key | existing `CreateNewEpic` section keys |
| `RealignEpic` | `realign-epic-prompt-owned-v1` | `realignEpicSourceHash` | `RealignEpicImplementationFirstGuidance`, `RealignEpicAuxiliaryArtifactLimits` |
| `ReimagineEpic` | `reimagine-epic-prompt-owned-v1` | `reimagineEpicSourceHash` | `ReimagineEpicImplementationFirstGuidance`, `ReimagineEpicAuxiliaryArtifactLimits` |
| `GenerateMilestoneDeepDivesForEpic` | `generate-milestone-deep-dives-for-epic-prompt-owned-v1` | `generateMilestoneDeepDivesForEpicSourceHash` | `GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance`, `GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits` |
| `SplitEpic` | `split-epic-prompt-owned-v1` | `splitEpicSourceHash` | `SplitEpicImplementationFirstGuidance`, `SplitEpicAuxiliaryArtifactLimits` |

### Placeholder Names

Use explicit placeholders in the owning planning prompt only where selected generated section text should appear.

| Runtime Prompt | Guidance Placeholder | Auxiliary Limits Placeholder |
|---|---|---|
| `RealignEpic` | `{realignEpicImplementationFirstGuidance}` | `{realignEpicAuxiliaryArtifactLimits}` |
| `ReimagineEpic` | `{reimagineEpicImplementationFirstGuidance}` | `{reimagineEpicAuxiliaryArtifactLimits}` |
| `GenerateMilestoneDeepDivesForEpic` | `{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}` | `{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}` |
| `SplitEpic` | `{splitEpicImplementationFirstGuidance}` | `{splitEpicAuxiliaryArtifactLimits}` |

Strict and allowed renders must leave no raw placeholder tokens.

### Section Text Ownership

- Section body text must live in `.prompt` files under `src/LoopRelay.Core/Prompts/NonImplementation`.
- Do not move section body text into C# string literals.
- Do not copy `CreateNewEpic` section text unchanged into another prompt.
- Do not reuse `InvalidContent.prompt` unchanged for these prompt-specific migrations.
- Section text should preserve the prompt's reasoning model and artifact contract, not replicate the legacy composer generically.

### Primary Artifact Rule

Strict auxiliary-artifact policy must never suppress or weaken a prompt's primary contracted output:

- `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic`: `.agents/epic.md`
- `GenerateMilestoneDeepDivesForEpic`: `.agents/specs/*.md`
- `SplitEpic`: split bundle, split plan, child epic `# FILE: .agents/epic-N.md` sections, split family state, and selected-child promotion

Allowed auxiliary mode omits strict prompt-owned policy sections, but it does not weaken primary output requirements or permit narrative substitutes.

## Milestone 1 Details: `RealignEpic`

### Gap Filled

`.agents/plan.md` lists the migration mechanics. The missing details are the precise minimal-realignment semantics, unchanged active-epic contracts, and test fixtures.

### File Impact

Add:

- `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicImplementationFirstGuidance.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/RealignEpicAuxiliaryArtifactLimits.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RealignEpicPromptSections.cs`

Modify:

- `src/LoopRelay.Core/Prompts/Planning/RealignEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

Do not change unless a blocking pre-existing bug is exposed:

- `ActiveEpicRewriteTransition`
- `ActiveEpicPromotionCoordinator`
- `EpicAuthoringOutputClassifier`
- `EpicArtifactValidator`
- `PromptContractRegistry`

### Required Section Semantics

The strict guidance must establish:

- Updating `.agents/epic.md` is the sanctioned implementation-bearing artifact.
- Realignment is a minimal strategic patch, not a replacement epic.
- Epic identity, strategic purpose, intended capability, and unaffected milestone roadmap content must be preserved.
- Audit findings may be compressed into scope, constraints, dependencies, assumptions, acceptance criteria, risks, and follow-up only when they affect implemented capability.
- Unaffected sections must not be rewritten for style or completeness.
- Repository re-audit reports, side-channel audit summaries, rationale appendices, governance notes, research notes, and companion design documents are invalid auxiliary artifacts.
- `# Epic Realignment Blocked` does not authorize extra explanatory artifacts.

### Public Contract

- Runtime prompt remains the audit-driven `.agents/epic.md` minimal realignment prompt.
- Blocked response remains `# Epic Realignment Blocked`.
- Strict render contains `# RealignEpic Implementation-First Guidance` and `# RealignEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits both strict sections and leaves no placeholders.
- `RoadmapPromptRunner` sends `RealignEpic` without the legacy composer heading.
- Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/epic.md`
  - decision `Realign`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

### Tests and Fixtures

Focused tests should cover:

- `RealignEpicPromptSections` strict and allowed behavior.
- Strict render markers.
- `.agents/epic.md` sanctioning.
- milestone roadmap preservation wording.
- placeholder removal in allowed mode.
- absence of `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- legacy control prompt still receiving the composer.
- no `# Invalid Content` legacy injection.
- section body text not hard-coded in C#.
- prompt contract registry unchanged.
- identity mode `realign-epic-prompt-owned-v1`.
- strict and allowed identities differ.
- strict identity contains prompt source hash and both active section hashes.
- allowed identity records omitted section mode and no active section hashes.

Fixtures:

- Project context: `"project context"`.
- Secondary audit input: a small audit requiring minimal `.agents/epic.md` correction.
- Strict policy: `AllowAuxiliaryNonImplementationFiles=false`.
- Allowed policy: same inputs with `AllowAuxiliaryNonImplementationFiles=true`.
- Invalid side-artifact terms: repository re-audit reports, raw audit summaries, rationale appendices, governance notes, research notes, companion design documents.
- Blocked output: `# Epic Realignment Blocked`.
- Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~RealignEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Milestone 2 Details: `ReimagineEpic`

### Gap Filled

The plan states that `ReimagineEpic` should be prompt-owned. The deep dive adds the key guardrail: full replacement is valid only when audit-grounded, not a free-form design exercise.

### File Impact

Add:

- `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicImplementationFirstGuidance.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/ReimagineEpicAuxiliaryArtifactLimits.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/ReimagineEpicPromptSections.cs`

Modify:

- `src/LoopRelay.Core/Prompts/Planning/ReimagineEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

Keep unchanged:

- active epic rewrite transition path
- active epic promotion path
- blocked output classification
- validation path
- prompt contract path

### Required Section Semantics

The strict guidance must establish:

- Replacing `.agents/epic.md` is sanctioned only because the audit disposition requires a better implementation-bearing epic.
- Strategic need and desired capability remain authoritative unless the audit explicitly justifies boundary changes.
- Full replacement may change title, framing, milestone roadmap, dependencies, assumptions, acceptance criteria, risks, non-goals, and capability boundaries when audit-supported.
- Every material redesign decision should trace to audit findings, projection context, or repository-grounded capability needs already supplied to the prompt.
- Companion design reports, rationale appendices, research notes, architecture proposals, governance notes, and raw audit-detail files are invalid auxiliary artifacts.
- `# Epic Reimagination Blocked` does not authorize extra explanatory artifacts.

### Public Contract

- Runtime prompt remains the audit-grounded full replacement prompt for `.agents/epic.md`.
- Blocked response remains `# Epic Reimagination Blocked`.
- Strict render contains `# ReimagineEpic Implementation-First Guidance` and `# ReimagineEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections without removing the required `.agents/epic.md` replacement output.
- `RoadmapPromptRunner` sends `ReimagineEpic` without the legacy composer heading.
- Prompt contract registry remains unchanged:
  - input `.agents/epic.md`
  - output `.agents/epic.md`
  - decision `Reimagine`
  - writer `ArtifactPromotionService`
  - parser `EpicAuthoringOutputClassifier+EpicArtifactValidator`

### Tests and Fixtures

Focused tests should cover:

- `ReimagineEpicPromptSections` strict and allowed behavior.
- strict render markers.
- replacement `.agents/epic.md` sanctioning.
- coherent replacement milestone roadmap language.
- no side design report implication.
- no raw audit appendix implication.
- placeholder removal.
- no legacy composer in runtime prompt.
- legacy control prompt still receiving the composer.
- identity mode `reimagine-epic-prompt-owned-v1`.
- prompt source hash and active section hashes in strict identity.
- strict and allowed identities differ.
- section text not hard-coded in C#.
- existing active epic rewrite, promotion, blocked-output, classifier, validator, and prompt contract tests still passing.

Fixtures:

- Project context plus audit input requiring full epic replacement.
- Allowed auxiliary fixture with the same inputs.
- Invalid auxiliary terms: companion design reports, rationale appendices, research notes, architecture proposals, governance notes, raw audit-detail files.
- Blocked output: `# Epic Reimagination Blocked`.
- Legacy control prompt: an unmigrated roadmap runtime prompt.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~ReimagineEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests"
```

## Milestone 3 Details: `GenerateMilestoneDeepDivesForEpic`

### Gap Filled

The main plan identifies this as a migrated prompt but does not fully spell out the semantic tension: `.agents/specs/*.md` are planning artifacts, but they are still primary machine-consumed implementation-preparation outputs and must not be prohibited as auxiliary artifacts.

### File Impact

Add:

- `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/GenerateMilestoneDeepDivesForEpicPromptSections.cs`

Modify:

- `src/LoopRelay.Core/Prompts/Planning/GenerateMilestoneDeepDivesForEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

Keep unchanged:

- `GenerateMilestoneDeepDivesTransition`
- `BundleFileExtractor`
- `BundleManifestWriter`
- execution preparation manifest/provenance services
- HITL evidence capture
- `InvariantValidator`
- prompt contract registry output semantics
- roadmap state transition to milestone specs ready

### Required Section Semantics

The strict guidance must establish:

- The primary output bundle under `.agents/specs/*.md` is not auxiliary output.
- One deep-dive spec per epic milestone remains required in strict and allowed auxiliary modes.
- Specs are valid when they preserve epic architecture, milestone intent, dependencies, constraints, validation strategy, completion evidence, and implementation-readiness information for later execution stages.
- Specs are invalid when they become standalone documentation, design essays, code-edit scripts, execution prompts, governance reports, research notes, or companion planning documents outside the contracted bundle.
- Allowed auxiliary mode must not permit narrative output to replace the contracted spec bundle.
- Blocking or partial generation must use the existing output protocol and must not create side-channel explanations.

### Public Contract

- Runtime prompt remains the prompt for generating milestone specs from the active epic.
- Required input remains `.agents/epic.md`.
- Required output remains `.agents/specs`.
- Output bundle still uses `# FILE:` sections parsed by `BundleFileExtractor`.
- Both strict and allowed renders require `.agents/specs/*.md` as primary contracted output.
- Strict render contains `# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance` and `# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections and leaves no placeholders.
- Prompt contract registry remains unchanged:
  - required input `.agents/epic.md`
  - required output `.agents/specs`
  - decision `Generate Specs`
  - writer `SpecBundleWriter`
  - parser `BundleFileExtractor`

### Tests and Fixtures

Focused tests should cover:

- `GenerateMilestoneDeepDivesForEpicPromptSections` strict and allowed behavior.
- strict render includes both section markers.
- allowed render omits strict sections and leaves no placeholders.
- strict and allowed renders both require `.agents/specs/*.md`.
- no legacy composer heading.
- legacy control prompt still receiving the composer.
- identity mode `generate-milestone-deep-dives-for-epic-prompt-owned-v1`.
- prompt source hash and active section hashes in strict identity.
- strict and allowed identities differ.
- `.agents/specs/*.md` is primary contracted output, not auxiliary output.
- prompt contract registry still declares `.agents/specs` and `BundleFileExtractor`.
- no `# Invalid Content` legacy injection.
- section body text not hard-coded in C#.

Regression suites must keep passing for:

- bundle extraction
- path validation
- bundle manifest writing
- spec materialization
- HITL artifact capture
- execution preparation provenance
- invariant validation
- malformed bundle failure
- invariant failure not marking specs ready
- roadmap state transition to `MilestoneSpecsReady`

Fixtures:

- Minimal valid active epic context with two milestones and ordered dependencies.
- Valid output bundle with two `# FILE: .agents/specs/*.md` sections.
- Invalid narrative-only output with no `# FILE:` sections.
- Invalid auxiliary output containing design essay, code-edit script, execution prompt, governance report, research note, or companion planning document outside the bundle.
- Invariant mismatch fixture where generated spec names or contents do not match the active epic.
- Strict and allowed policy fixtures.
- Legacy control prompt.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~GenerateMilestoneDeepDivesForEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~BundleFileExtractorTests|FullyQualifiedName~InvariantValidatorTests"
```

## Milestone 4 Details: `SplitEpic`

### Gap Filled

The plan says `SplitEpic` should be prompt-owned but does not fully capture that child epic files are primary contracted output inside the split bundle, not auxiliary artifacts.

### File Impact

Add:

- `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicImplementationFirstGuidance.prompt`
- `src/LoopRelay.Core/Prompts/NonImplementation/SplitEpicAuxiliaryArtifactLimits.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/SplitEpicPromptSections.cs`

Modify:

- `src/LoopRelay.Core/Prompts/Planning/SplitEpic.prompt`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapPromptCatalog.cs`
- `src/LoopRelay.Roadmap.Cli/Services/Prompts/RoadmapRuntimePromptPolicy.cs`

Keep unchanged:

- `SplitEpicTransition`
- `BundleFileExtractor`
- `SplitEpicBundleInterpreter`
- `SplitFamilyStore`
- `HitlArtifactCapture`
- `ActiveEpicPromotionCoordinator`
- `EpicArtifactValidator`
- prompt contract writer/parser boundaries

### Required Section Semantics

The strict guidance must establish:

- The sanctioned output boundary is the split bundle consumed by the split transition.
- The split bundle includes a split plan and `# FILE: .agents/epic-N.md` child epic sections required by the prompt contract.
- Child epic files are primary contracted outputs, not invalid auxiliary artifacts.
- The bundle is valid because it partitions capability into implementation-bearing child epics and supplies machine-consumed lineage and promotion inputs.
- Child epics should follow the `CreateNewEpic` authoring standard at the concept level.
- `SplitEpic` owns capability partitioning, coverage proof, non-overlap, sibling dependencies, and selected-child promotion semantics.
- Splits are invalid when they are merely planning phases, file-area groupings, implementation layers, task lists, or work breakdowns.
- Companion inventories, RFCs, governance notes, research documents, design reports, and explanatory appendices are invalid auxiliary artifacts unless explicitly requested or required by an existing machine-consumed contract.
- `# Split Epic Blocked` does not authorize extra explanatory artifacts.

### Public Contract

- Runtime prompt remains the split proposal decomposition prompt.
- Required input remains `.agents/selection` through roadmap prompt context.
- Required outputs remain split family state and selected active epic as declared by prompt contracts.
- Output remains a multi-file Markdown bundle consumed by `BundleFileExtractor` and `SplitEpicBundleInterpreter`.
- Split plan and `# FILE: .agents/epic-N.md` child epic sections are primary contracted outputs.
- Blocked response remains `# Split Epic Blocked`.
- Strict render contains `# SplitEpic Implementation-First Guidance` and `# SplitEpic Auxiliary Artifact Limits`.
- Allowed auxiliary render omits strict sections and leaves no placeholders.
- Prompt contract registry remains unchanged:
  - writer boundary `SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService`
  - parser boundary `BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator`

### Tests and Fixtures

Focused tests should cover:

- `SplitEpicPromptSections` strict and allowed behavior.
- strict render includes both section markers.
- split bundle sanctioning.
- split plan sanctioning.
- `# FILE: .agents/epic-N.md` child sections treated as valid primary outputs.
- child epic files not treated as invalid auxiliary artifacts.
- allowed mode placeholder removal.
- allowed mode does not weaken bundle requirements.
- no legacy composer heading.
- legacy control prompt still receiving the composer.
- identity mode `split-epic-prompt-owned-v1`.
- prompt source hash and active section hashes in strict identity.
- strict and allowed identities differ.
- section body text not hard-coded in C#.
- prompt does not encourage task, file-area, phase, or layer splits.

Regression suites must keep passing for:

- prompt contract writer/parser boundaries
- bundle extraction
- split bundle interpretation
- invalid bundle rejection
- overlap detection
- missing split plan or missing child detection
- invalid child path rejection
- split family persistence
- HITL child artifact evidence capture
- selected-child promotion
- epic validation

Fixtures:

- Project context plus split proposal.
- Valid split bundle with split plan and at least two `# FILE: .agents/epic-N.md` child epic sections.
- Invalid task split fixture: files, phases, layers, or work items rather than capability partitions.
- Invalid auxiliary fixture: companion inventory, RFC, governance note, research document, design report, or explanatory appendix outside the contracted bundle.
- Blocked output: `# Split Epic Blocked`.
- Split lineage fixture with valid child epics, stable order, and selected child.
- Strict and allowed policy fixtures.
- Legacy control prompt.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~SplitEpicPromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~SplitEpicBundleInterpreterTests|FullyQualifiedName~SplitFamilyStoreTests"
```

## Milestone 5 Details: Retirement Checkpoint

### Gap Filled

The plan has the done definition but not the full aggregate certification matrix and settings-separation coverage.

### Aggregate Prompt Set

All aggregate tests and identity matrix tests must explicitly cover:

- `CreateNewEpic`
- `RealignEpic`
- `ReimagineEpic`
- `GenerateMilestoneDeepDivesForEpic`
- `SplitEpic`

### Aggregate Runner Tests

Use production `RoadmapPromptRunner` or production catalog paths, not manually concatenated prompt strings.

Tests must:

- Run all five migrated prompts in strict auxiliary mode.
- Assert none of the five captured prompts contain `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- Run a legacy control prompt, such as `SelectNextEpic`.
- Assert the legacy control prompt contains `ImplementationFirstPromptPolicyComposer.SectionHeading`.
- Assert primary artifact contracts are still visible in rendered prompts:
  - `CreateNewEpic`: `.agents/epic.md`
  - `RealignEpic`: `.agents/epic.md`
  - `ReimagineEpic`: `.agents/epic.md`
  - `GenerateMilestoneDeepDivesForEpic`: `.agents/specs/*.md`
  - `SplitEpic`: split bundle and child epic file sections

### Identity Matrix Tests

Tests must:

- Cover all five prompt-owned mode strings.
- Verify strict and allowed auxiliary identities differ for every migrated prompt.
- Verify strict identity includes planning prompt source hash and active section source hashes.
- Verify allowed identity records `sectionMode=omitted`.
- Verify allowed identity has no active `section.*.sourceHash` entries.
- Verify `AllowAuxiliaryNonImplementationFiles` affects prompt-owned identity.
- Verify toggling `AllowHitlRequestedNonImplementationFiles` does not affect prompt-owned section selection.
- Verify legacy prompts still use `legacy-implementation-first-composer-v1`.
- Verify legacy identity records `legacyImplementationFirstPromptPolicyHash`.

### Settings Fixtures

- Strict prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=false`, `AllowHitlRequestedNonImplementationFiles=false`.
- Allowed prompt-owned fixture: `AllowAuxiliaryNonImplementationFiles=true`, `AllowHitlRequestedNonImplementationFiles=false`.
- HITL separation fixture: policy variants that toggle `AllowHitlRequestedNonImplementationFiles` while prompt-owned selector behavior remains unchanged.

### Prompt Input Fixture Map

- `CreateNewEpic`: project context plus proposal.
- `RealignEpic`: project context plus audit input.
- `ReimagineEpic`: project context plus audit input.
- `GenerateMilestoneDeepDivesForEpic`: project context and empty secondary input.
- `SplitEpic`: project context plus split proposal.
- Legacy control prompt: `SelectNextEpic` or another unmigrated roadmap runtime prompt.

### Source-Adjacent Cleanup

Allowed:

- Test names and comments that would otherwise falsely describe migrated roadmap artifact-authoring prompt policy as centralized in the legacy composer.
- Narrow internal comments clarifying that the composer remains transitional infrastructure for out-of-scope consumers.

Not allowed:

- Renaming or deleting public concepts used by out-of-scope consumers.
- Administrative migration reports, readiness reports, governance notes, ADRs, RFCs, or manual certification documents as implementation outputs.
- Deleting `ImplementationFirstPromptPolicyComposer`.
- Removing non-roadmap composer tests.

### Checkpoint Exit Criteria

Milestone 5 is complete only when:

- Aggregate strict-mode runner tests prove all five migrated prompts skip the legacy composer.
- A companion assertion proves an out-of-scope prompt still receives the legacy composer.
- Identity tests cover all five prompt-owned modes and required source-hash inputs.
- Strict and allowed auxiliary branches change identity for every migrated prompt.
- `AllowHitlRequestedNonImplementationFiles` remains isolated to legacy composer behavior.
- Existing artifact contracts, parser boundaries, promotion behavior, validators, and transition semantics remain intact.
- Existing non-roadmap composer tests remain passing.
- `ImplementationFirstPromptPolicyComposer` remains available and tested.
- No future, out-of-scope migration or composer deletion is falsely claimed.

Verification command:

```powershell
dotnet test LoopRelay.slnx --filter "FullyQualifiedName~PromptPolicyTests|FullyQualifiedName~TransitionInputResolverTests|FullyQualifiedName~PromptContractRegistryTests|FullyQualifiedName~NonImplementationReview"
```

## Cross-Milestone Failure Modes

These are the recurring failure modes that should drive test names and diagnostics:

- Strict sections are not injected.
- Allowed mode leaves raw placeholders.
- Legacy composer is appended to a prompt-owned prompt.
- A legacy control prompt stops receiving the composer.
- Identity still uses the legacy mode for a migrated prompt.
- Strict identity omits prompt source hash or active section source hashes.
- Allowed identity equals strict identity.
- `AllowHitlRequestedNonImplementationFiles` affects prompt-owned section selection.
- Section body text is hard-coded in C#.
- `CreateNewEpic` or `InvalidContent.prompt` text is copied unchanged.
- Primary contracted artifacts are treated as auxiliary artifacts.
- Allowed auxiliary mode weakens primary artifact contracts.
- Existing writer/parser/promotion/validator boundaries drift.
- Bundle extraction, manifest writing, execution preparation provenance, split lineage, HITL capture, or selected-child promotion regresses.

## Regression Surface By Prompt

| Prompt | Primary Artifact Contract | Key Regression Surface |
|---|---|---|
| `CreateNewEpic` | `.agents/epic.md` | Existing prompt-owned reference behavior, runner skip, identity branch |
| `RealignEpic` | `.agents/epic.md` minimal realignment | Active epic rewrite, promotion, classifier, validator, blocked output |
| `ReimagineEpic` | `.agents/epic.md` audit-grounded replacement | Active epic rewrite, promotion, classifier, validator, blocked output |
| `GenerateMilestoneDeepDivesForEpic` | `.agents/specs/*.md` one spec per milestone | Bundle extraction, manifest writing, specs-ready state, HITL evidence, execution preparation provenance, invariants |
| `SplitEpic` | split bundle, split plan, child epic files, selected-child promotion | Bundle extraction, split interpreter, split family store, HITL capture, selected-child promotion, epic validation |

## Final Verification

Run focused tests after each milestone, then run the aggregate checkpoint command. Before declaring the roadmap complete, run:

```powershell
dotnet test LoopRelay.slnx
```

The roadmap is complete only for the in-scope roadmap artifact-authoring prompts. The broader codebase still intentionally carries `ImplementationFirstPromptPolicyComposer` for Plan CLI, Loop CLI, completion, decision, execution, `AdversarialPlanReview`, and any other out-of-scope consumers.
