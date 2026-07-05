# Split Promotion Can Overwrite Active Epic With Non-Epic Artifact

## Finding

Verified against the current codebase on 2026-07-05.

`SplitEpicAsync` can write non-epic bundle content to `.agents/epic.md`, bypassing the active-epic promotion boundary used by `CreateNewEpic`, `RealignEpic`, and `ReimagineEpic`.

The split path currently:

1. Runs `SplitEpic`.
2. Extracts all `# FILE:` bundle files through the generic `BundleFileExtractor`.
3. Writes all extracted files before split-specific semantic validation.
4. Chooses `selectedChild` from every extracted path by lexicographic order.
5. Writes `selectedChild` content directly to `.agents/epic.md`.

Relevant code:

| Evidence | Current behavior |
|---|---|
| `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:339-345` | `SplitEpicAsync` extracts the bundle, writes extracted files, writes the manifest, then chooses the first path from `bundle.Files`. |
| `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:347-354` | The selected path is passed to `ValidateSplitChildPromotionAsync`, then copied directly into `.agents/epic.md` and marked `Ready`. |
| `src/CommandCenter.Roadmap.CLI/BundleFileExtractor.cs:37-47` | `WriteExtractedFilesAsync` writes every extracted file before split child validation. |
| `src/CommandCenter.Roadmap.CLI/BundleFileExtractor.cs:59-62` | The generic bundle allowlist permits `.agents/specs/*.md`, `.agents/epic-*.md`, and `.agents/epic.md`. |
| `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs:93-100` | `ValidateSplitChildPromotionAsync` returns valid for any path that is not `.agents/epic-*`. |
| `src/CommandCenter.Roadmap.CLI/ArtifactPromotion.cs:74-100` | The reusable promotion service classifies and validates candidate content before writing the target artifact. Split promotion does not use it. |
| `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs:670-706` | Other active-epic promotion paths route through `ArtifactPromotionService` and only save `ActiveEpicReady` after promotion succeeds. |

The existing issue statement said a spec can sort before a child epic. That is not true for the current allowed path shapes under `StringComparer.Ordinal`: `.agents/epic-*` sorts before `.agents/specs/*`. The confirmed bug is narrower and still serious:

- if the split bundle contains only `.agents/specs/*.md`, the spec becomes `selectedChild`
- if the split bundle contains `.agents/epic.md`, the active epic is written during extraction before validation
- if the split bundle contains both `.agents/epic.md` and a malformed `.agents/epic-*`, the malformed child can block final promotion while the direct active target has already overwritten `.agents/epic.md`

## Verified Failure Paths

### Spec-only bundle

If `SplitEpic` outputs only:

```markdown
# FILE: .agents/specs/foo.md
...
```

then:

1. `BundleFileExtractor` accepts the path because specs are allowed.
2. `WriteExtractedFilesAsync` writes `.agents/specs/foo.md`.
3. `selectedChild` becomes `.agents/specs/foo.md`.
4. `ValidateSplitChildPromotionAsync` returns valid because the path does not start with `.agents/epic-`.
5. `SplitEpicAsync` writes the spec content to `.agents/epic.md`.
6. `.agents/epic.md` is marked `Ready`.

The same run then advances to milestone generation. `RunSelectionAndFollowingAsync` calls `GenerateMilestoneSpecsAsync` immediately after split promotion, and `BuildMilestoneContextAsync` reads the corrupted active epic into the next prompt context before the later `MilestoneSpecsReady` invariant runs.

### Direct active-epic bundle target

If `SplitEpic` outputs:

```markdown
# FILE: .agents/epic.md
...
```

then:

1. `BundleFileExtractor` accepts the active epic path.
2. `WriteExtractedFilesAsync` writes `.agents/epic.md` immediately.
3. If no `.agents/epic-*` child exists, `selectedChild` becomes `.agents/epic.md`.
4. `ValidateSplitChildPromotionAsync` returns valid because `.agents/epic.md` is not `.agents/epic-*`.
5. The direct write has already replaced the authoritative active epic, even before the explicit line 353 promotion write.

This is the highest-risk path because it can corrupt an existing active epic even when a later split-child validation fails.

### Malformed child epic

If the bundle contains a malformed `.agents/epic-1.md` and no direct `.agents/epic.md` target, the selected child validation fails before the explicit active-epic copy. That preserves the active epic, but the malformed child file, bundle manifest, and split-family artifact have already been written. This is less severe than the direct active target path, but it still violates the desired "validate before write" boundary.

## Impact

The CLI can corrupt the authoritative active epic with a milestone spec, a direct active-epic bundle file, or other content that never passed `EpicAuthoringOutputClassifier` and `EpicArtifactValidator`.

The blast radius is larger than a normal failed promotion:

- `.agents/epic.md` can be overwritten before the state machine blocks.
- The corrupted active epic can be passed into `GenerateMilestoneDeepDivesForEpic` during the same run.
- A rerun starts with a damaged `.agents/epic.md`; the resume planner validates it and can block, but the human still has to repair or restore the active epic.
- Split-family metadata can record non-child files as `ChildEpicPaths`, making recovery evidence misleading.
- The active-epic promotion journal does not show `ArtifactPromotionService`, so the state history does not prove that the promoted content was classified and validated.

## Root Cause

The split transition conflates generic bundle extraction with the domain model for split epic promotion.

`BundleFileExtractor` is intentionally broad because it is reused by roadmap bundle-producing prompts. `SplitEpicAsync` then treats the raw `BundleExtractionResult` as if every extracted file were a child epic. That assumption is false for the extractor's allowlist.

The split transition needs a narrower contract:

- Split output must contain one or more child epic files.
- A child epic path must be a valid `.agents/epic-*.md` path.
- `.agents/epic.md` must never be accepted as a split bundle output target.
- The selected child must come from the validated child-epic set.
- Child epic content must pass the same classifier and validator used by active-epic promotion.
- Non-child bundle outputs must not be eligible for active-epic promotion.
- Files should not be written until split-specific validation succeeds.

The prompt also supports this narrower interpretation. `SplitEpic.prompt` documents blocking output as a normal `# Split Epic Blocked` document without file markers, and its file-marker examples are `.agents/epic-1.md`, `.agents/epic-2.md`, and `.agents/epic-N.md`.

## Solution Options

### Option 1: Split-specific bundle interpreter, then promote through the existing boundary

Add a split-specific interpretation step before any writes:

```csharp
internal sealed record SplitEpicBundle(
    IReadOnlyList<ExtractedBundleFile> ChildEpics,
    IReadOnlyList<ExtractedBundleFile> SupportingFiles,
    ExtractedBundleFile SelectedChild);

internal sealed class SplitEpicBundleValidator
{
    public SplitEpicBundle Validate(BundleExtractionResult bundle);
}
```

Validation rules:

1. Reject blocked or empty bundles.
2. Reject `.agents/epic.md` anywhere in split output.
3. Partition files into child epics and supporting files.
4. Require at least one valid child epic path.
5. Validate child paths with a stricter predicate than `StartsWith(".agents/epic-")`; for example, reject nested paths and require a non-empty filename stem.
6. Run `EpicAuthoringOutputClassifier` and `EpicArtifactValidator` on every child epic.
7. Select the child only from the validated child-epic list.
8. Write bundle files and split-family metadata only after validation succeeds.
9. Promote the selected child's content through `ArtifactPromotionService` or a shared helper with identical classification and validation semantics.

Pros:

- Preserves the generic extractor while making split semantics explicit.
- Reuses the promotion boundary already proven by promotion tests.
- Gives the cleanest domain model for split-family metadata.

Cons:

- Adds a new component and tests.
- Requires deciding whether supporting files are allowed at all for `SplitEpic`.

Recommended as the primary fix.

### Option 2: Make bundle extraction prompt-contract aware

Replace the hard-coded `BundleFileExtractor` allowlist with target policies supplied by the prompt contract:

```csharp
internal sealed record BundleTargetPolicy(
    bool AllowSpecs,
    bool AllowSplitChildEpics,
    bool AllowActiveEpic);
```

Example policies:

- `SplitEpic`: allow `.agents/epic-*.md` only
- `GenerateMilestoneDeepDivesForEpic`: allow `.agents/specs/*.md` only
- no current bundle prompt should need direct `.agents/epic.md`

Pros:

- Removes the shared allowlist that created this bug.
- Makes prompt contracts more truthful: `SplitEpic` is a split-family writer, not a spec writer or active-epic writer.
- Also reduces risk for future bundle prompts.

Cons:

- Does not by itself validate child epic content before promotion.
- Still needs Option 1 or Option 4 to prevent direct active-epic copy bypasses.

Good companion fix. Not sufficient alone.

### Option 3: Stage bundle writes and commit only after validation

Change split handling so extracted files remain in memory or in a staging area until all split validation passes. After validation:

1. write validated child epic files
2. write supporting files, if allowed
3. write the split-family artifact
4. promote the selected child to `.agents/epic.md`

Pros:

- Gives atomic behavior without relying on rollback.
- Prevents stale malformed child files and misleading split-family artifacts from being persisted.

Cons:

- Requires either a new staging abstraction or careful sequencing in `SplitEpicAsync`.
- Does not define split semantics by itself; it should be paired with Option 1.

Useful for implementation hygiene, especially if supporting files remain allowed.

### Option 4: Minimal patch inside `SplitEpicAsync`

For a smaller tactical fix:

1. After extraction, reject `bundle.IsBlocked` and empty files before writing.
2. Reject any file whose path is `.agents/epic.md`.
3. Filter `bundle.Files` to `.agents/epic-*.md` child candidates.
4. Require at least one child candidate.
5. Validate child candidates.
6. Choose `selectedChild` from child candidates only.
7. Replace the direct `.agents/epic.md` write with `ArtifactPromotionService`.
8. Move `WriteExtractedFilesAsync` after the validation steps.

Pros:

- Lowest implementation cost.
- Directly closes the known corruption paths.

Cons:

- Leaves split bundle semantics embedded in the state machine.
- Easier for future changes to reintroduce drift because there is no named split-domain abstraction.

Acceptable as a short-term fix if the broader state machine refactor is not desired.

### Option 5: Strengthen invariants as defense in depth

Change `ValidateSplitChildPromotionAsync` so non-`.agents/epic-*` paths fail instead of returning valid, and run active-epic validation before milestone generation.

Pros:

- Catches accidental non-child promotion attempts.
- Improves the resume and same-run safety net.

Cons:

- Not sufficient as the primary fix because `.agents/epic.md` can already be overwritten during `WriteExtractedFilesAsync`.
- Still does not make split-family metadata accurate.

Use as defense in depth, not as the only change.

### Option 6: Prompt-only tightening

Tighten `SplitEpic.prompt` to say the only permitted file markers are `.agents/epic-*.md`, and explicitly forbid `.agents/specs/*` and `.agents/epic.md`.

Pros:

- Cheap and useful as model guidance.

Cons:

- Not enforceable.
- Does not protect against malformed, adversarial, or drifted output.

Use only as a secondary guard after code-level enforcement.

## Recommended Direction

Implement Option 1 plus the relevant target-policy piece of Option 2. If time is limited, implement Option 4 first, but structure it so the validation logic can be extracted into `SplitEpicBundleValidator` later.

The important ordering is:

1. extract to memory
2. reject invalid target paths for the `SplitEpic` contract
3. validate child epic content
4. choose a child only from validated children
5. write non-authoritative bundle artifacts
6. promote the selected child through the active-epic promotion boundary
7. save `ActiveEpicReady` only after promotion succeeds

## Acceptance Criteria

- A split bundle containing only `.agents/specs/foo.md` blocks before writing `.agents/epic.md`.
- A split bundle containing `.agents/epic.md` is rejected before extraction writes any file.
- A split bundle containing malformed `.agents/epic-1.md` blocks and preserves the existing active epic.
- A split bundle containing both `.agents/epic.md` and malformed `.agents/epic-1.md` preserves the existing active epic.
- A split bundle with multiple valid `.agents/epic-*.md` children records only validated child epic paths as child epics.
- The selected child path in the split-family artifact is always one of the validated child epic paths.
- `SplitEpicAsync` does not write `.agents/epic.md` directly; selected-child promotion uses `ArtifactPromotionService` or a shared equivalent.
- No split transition saves `ActiveEpicReady` until selected-child validation and promotion both succeed.
- No invalid split output is passed into `GenerateMilestoneDeepDivesForEpic` as the active epic.
- Direct active-epic bundle targets are not allowed by the split bundle target policy.

## Suggested Tests

- `SplitEpic_rejects_bundle_without_child_epics`
- `SplitEpic_rejects_spec_only_bundle_before_active_epic_write`
- `SplitEpic_rejects_direct_active_epic_target_before_any_write`
- `SplitEpic_rejects_malformed_child_before_active_epic_write`
- `SplitEpic_rejects_direct_active_target_even_when_child_is_malformed`
- `SplitEpic_promotes_valid_child_through_promotion_boundary`
- `SplitEpic_selected_child_is_from_validated_child_set`
- `SplitEpic_split_family_records_only_child_epic_paths`
- `SplitEpic_does_not_run_milestone_generation_after_invalid_split`
- `BundleFileExtractor_applies_split_specific_target_policy`
