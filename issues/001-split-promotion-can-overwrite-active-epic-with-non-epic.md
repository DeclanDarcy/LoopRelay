# Split Promotion Can Overwrite Active Epic With Non-Epic Artifact

## Finding

`SplitEpicAsync` can promote a non-epic bundle file into `.agents/epic.md`.

The split path currently:

1. Runs `SplitEpic`.
2. Extracts all bundle files with `BundleFileExtractor`.
3. Writes the extracted files.
4. Picks the lexicographically first extracted file as `selectedChild`.
5. Writes `selectedChild` content directly to `.agents/epic.md`.

Relevant code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateMachine.cs`
- `SplitEpicAsync`
- `src/CommandCenter.Roadmap.CLI/BundleFileExtractor.cs`
- `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs`

`BundleFileExtractor` allows three target classes:

- `.agents/specs/*.md`
- `.agents/epic-*.md`
- `.agents/epic.md`

But `SplitEpicAsync` treats every extracted file as a split child. If the bundle contains only a spec, or if a spec sorts before the child epic, that file becomes `selectedChild`. `ValidateSplitChildPromotionAsync` returns valid for non-`.agents/epic-*` paths, so the direct overwrite is allowed until a later invariant detects that `.agents/epic.md` is malformed.

## Impact

The CLI can destroy or corrupt the authoritative active epic with a milestone spec or other allowed bundle output. This is worse than a failed promotion because the invalid content is written to the canonical path before the run blocks. A rerun then starts from a damaged active epic and requires manual repair.

The existing `ArtifactPromotionService` correctly classifies blocked, ambiguous, and structurally invalid epic authoring output before writing the target artifact. The split path bypasses that service.

## Root Cause

The bundle extractor has a broad allowlist for all roadmap bundle-producing prompts, but the split transition needs a narrower contract:

- Split output must include one or more child epic files.
- Child epic files must be under `.agents/epic-*.md`.
- The selected child must be one of those epic files.
- The selected child content must pass the same epic classifier and validator used by other active-epic promotion paths.
- Non-child outputs, such as specs, must never be eligible for active-epic promotion.

## Proposal

Introduce a split-specific bundle interpreter instead of reusing raw `BundleExtractionResult` as the domain model.

Suggested shape:

```csharp
internal sealed record SplitEpicBundle(
    IReadOnlyList<ExtractedBundleFile> ChildEpics,
    IReadOnlyList<ExtractedBundleFile> SupportingFiles);

internal sealed class SplitEpicBundleValidator
{
    public SplitEpicBundle Validate(BundleExtractionResult bundle);
}
```

Validation rules:

1. Reject blocked bundles and empty bundles before writing anything.
2. Partition extracted files into child epic files and supporting files.
3. Require at least one `.agents/epic-*.md` child.
4. Reject `.agents/epic.md` as a split output target; promotion to active epic should happen only through the state machine.
5. Run `EpicAuthoringOutputClassifier` and `EpicArtifactValidator` on every child epic.
6. Choose `selectedChild` only from validated child epic files.
7. Write extracted files only after the entire split bundle is validated.
8. Promote the selected child through `ArtifactPromotionService` or a shared helper that validates before writing `.agents/epic.md`.

This keeps extraction generic while making split promotion domain-specific and atomic: either a valid split family exists and a validated child is promoted, or no authoritative active epic is touched.

## Acceptance Criteria

- A split bundle containing only `.agents/specs/foo.md` blocks before writing `.agents/epic.md`.
- A split bundle containing malformed `.agents/epic-1.md` blocks and preserves the existing active epic.
- A split bundle containing `.agents/epic.md` is rejected as an invalid split target.
- A split bundle with multiple valid `.agents/epic-*.md` children records the family and promotes only the selected child.
- The selected child path in the split-family artifact is always one of the validated child epic paths.
- No split transition writes `.agents/epic.md` before child validation succeeds.

## Suggested Tests

- `SplitEpic_rejects_bundle_without_child_epics`
- `SplitEpic_rejects_malformed_child_before_active_epic_write`
- `SplitEpic_rejects_direct_active_epic_target`
- `SplitEpic_promotes_valid_child_through_promotion_boundary`
- `SplitEpic_preserves_existing_active_epic_on_blocked_bundle`
