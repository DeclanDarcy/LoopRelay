# Projection Manifest Source Hash Is Prompt Name Hash

## Status

Verified against the current codebase on 2026-07-05.

The issue is real. `ProjectionPromptSourceHash` is populated with a SHA-256 hash of the projection prompt name, not the generated prompt template's `SourceHash`, and no stale-projection path compares the stored source hash with the current generated prompt source hash.

## Finding

`ProjectionCache.EnsureAsync` writes:

```csharp
RoadmapHash.Sha256(projection.ProjectionPromptName)
```

into the manifest column named `Projection Prompt Source Hash`.

That value is only a stable hash of identifiers such as `ProjectionForSelectNextEpic`. It is not the generated catalog value:

```csharp
CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash
```

Relevant code:

- `src/CommandCenter.Roadmap.CLI/ProjectionCache.cs`
  - writes `RoadmapHash.Sha256(projection.ProjectionPromptName)`
  - marks a projection stale only when `previous.ProjectContextHash != projectContext.Hash`
- `src/CommandCenter.Roadmap.CLI/ProjectionManifest.cs`
  - stores `ProjectionPromptSourceHash`, but has no prompt type or provenance version field
- `src/CommandCenter.Roadmap.CLI/ProjectionManifestStore.cs`
  - renders and loads the `Projection Prompt Source Hash` column
  - already round-trips the field, but the current test only asserts `ProjectionHash`
- `src/CommandCenter.Roadmap.CLI/RoadmapPromptCatalog.cs`
  - calls the generated projection prompt classes to render prompts
  - does not expose generated prompt metadata
- `src/CommandCenter.Roadmap.CLI/ProjectionRegistry.cs`
  - maps runtime prompt names to projection prompt names and paths only
- `src/CommandCenter.Roadmap.CLI/RoadmapResumePlanner.cs`
  - blocks stale projections by manifest stale status or Project Context hash drift only
- `src/CommandCenter.Roadmap.CLI/InvariantValidator.cs`
  - requires an existing projection file to have a manifest entry and not be marked invalid
  - does not validate prompt source provenance
- `docs/prompt-architecture.md`
  - documents `SourceHash` as the generated SHA-256 hash of raw `.prompt` template text
- `dotnet-libraries/Lib.Prompts/src/Lib.Prompts/PromptSourceGenerator.cs`
  - emits `public const string SourceHash` and `public const string Template` for each `.prompt`

## Current Mechanics

Projection freshness currently works like this:

1. `ProjectionRegistry` resolves the runtime prompt to a projection prompt name and projection path.
2. `ProjectionCache` reads the existing projection file.
3. If the projection file is missing or blank, the projection prompt is rendered and executed.
4. If the projection file already exists, the projection prompt is not executed.
5. The projection content is validated and hashed.
6. The existing manifest row is loaded, if any.
7. The projection is marked stale only when the file already existed, a previous manifest row exists, and the stored Project Context hash differs from the current Project Context hash.
8. The manifest entry is upserted with:
   - runtime prompt name
   - projection prompt name
   - projection path
   - `SHA256(projection prompt name)`
   - Project Context files
   - Project Context hash
   - projection content hash
   - generated timestamp
   - validation status
   - stale status
   - validation error
9. For `StaleProjectionPolicy.Block`, stale status blocks only after the manifest has been written.

Two important edge cases fall out of this flow:

- Prompt template edits are invisible when the Project Context hash is unchanged, because the stored prompt source hash never changes.
- If a projection file exists but has no manifest row, `ProjectionCache` treats the file as fresh, writes a new manifest row with the current Project Context hash, and does not regenerate the projection. `InvariantValidator` can catch projection-without-manifest later, but `ProjectionCache` itself can legitimize an unproven projection as fresh.

## Impact

Projection provenance is misleading. A manifest row appears to pin a projection to the exact prompt source that produced it, but it only pins the projection prompt identifier.

This has several practical consequences:

- A changed projection prompt template does not stale existing projection files when Project Context is unchanged.
- `StaleProjectionPolicy.Block` cannot block prompt-source drift, because prompt-source drift is never detected.
- Audit and recovery workflows cannot prove which prompt template version produced a projection.
- A projection generated under older instructions can be reused while the manifest reports a fresh current row.
- Existing projection files with missing manifest rows can be re-certified as fresh without regeneration.

The highest-risk path is a projection prompt edit that changes interpretation rules or required output structure while the source Project Context remains byte-identical. The CLI may keep using an old projection against a new prompt contract and show a fresh manifest.

## Root Cause

The roadmap CLI has a string-only projection prompt model.

`RoadmapPromptCatalog` knows how to render generated prompt classes, but it exposes only rendered prompt text. `ProjectionRegistry` stores only:

- runtime prompt name
- projection prompt name
- projection output path

`ProjectionCache` therefore has no typed access to:

- generated prompt type
- generated prompt source hash
- raw template text

The manifest schema already has a source-hash field, but the writer fills it with a placeholder that looks valid because both the placeholder and the real `SourceHash` are 64-character SHA-256 hex strings.

## Solution Options

### Option A: Prompt metadata bridge in `RoadmapPromptCatalog` (recommended)

Add typed metadata methods next to the existing render methods:

```csharp
internal sealed record PromptTemplateMetadata(
    string PromptName,
    string PromptType,
    string SourceHash);
```

Suggested methods:

```csharp
internal static PromptTemplateMetadata GetProjectionMetadata(string projectionPromptName);
internal static PromptTemplateMetadata GetRuntimeMetadata(string runtimePromptName);
```

Each projection mapping should return the generated type and `SourceHash`, for example:

```csharp
"ProjectionForSelectNextEpic" =>
    new(
        nameof(CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic),
        typeof(CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic).FullName!,
        CommandCenter.Core.Prompts.Projections.ProjectionForSelectNextEpic.SourceHash)
```

Then `ProjectionCache` should compute current provenance from `GetProjectionMetadata(projection.ProjectionPromptName)` and write `metadata.SourceHash` to the manifest.

Staleness should compare both:

- stored Project Context hash vs current Project Context hash
- stored projection prompt source hash vs current projection prompt source hash

Pros:

- Uses the generated prompt authority directly.
- No runtime file I/O.
- Keeps the change local to the roadmap CLI.
- Aligns with the existing `RoadmapPromptCatalog` switch style.

Cons:

- Adds a second switch next to `RenderProjection` unless render and metadata are consolidated.
- Does not by itself make metadata injectable for tests that need arbitrary source-hash changes.

### Option B: Move metadata into `ProjectionRegistry`

Extend `ProjectionDefinition` to include prompt provenance:

```csharp
internal sealed record ProjectionDefinition(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPromptType,
    string ProjectionPromptSourceHash,
    string ProjectionPath);
```

The registry would become the single projection contract source for prompt name, generated type, source hash, and output path.

Pros:

- Puts all runtime-to-projection contract details in one place.
- Makes `ProjectionCache` simpler.
- A registry coverage test can assert every registered projection has metadata.

Cons:

- Duplicates generated prompt references already present in `RoadmapPromptCatalog`.
- Still needs render logic elsewhere, so name/type/hash/render can drift unless consolidated.
- Less useful for runtime prompt metadata unless a similar registry is introduced.

### Option C: Inject a prompt metadata provider

Introduce an interface:

```csharp
internal interface IPromptTemplateMetadataProvider
{
    PromptTemplateMetadata GetProjection(string projectionPromptName);
    PromptTemplateMetadata GetRuntime(string runtimePromptName);
}
```

Production implementation can delegate to `RoadmapPromptCatalog`; tests can provide fixed source hashes.

Pros:

- Makes stale-source regression tests straightforward.
- Keeps `ProjectionCache` independent of static generated prompt classes.
- Useful if other roadmap components later need prompt provenance.

Cons:

- More plumbing for a small CLI.
- Adds abstraction before there is more than one production metadata source.

Best use: combine with Option A only if tests or future provenance consumers need the seam.

### Option D: Hash the `.prompt` files at runtime

Read `src/CommandCenter.Core/Prompts/Projections/*.prompt` and hash the raw file content.

Pros:

- Avoids adding generated-type metadata mappings.
- Detects local source edits during development.

Cons:

- Not reliable for published binaries where source `.prompt` files may not exist.
- Can disagree with the compiled generated prompt class that is actually running.
- Undercuts the documented generated prompt authority.

This is not recommended.

### Option E: Hash rendered projection prompt text

Hash `projection.RenderPrompt(projectContext.Content)` and write that to the manifest.

Pros:

- Easy to implement from the current API.
- Detects some prompt text changes.

Cons:

- It is not the documented `SourceHash`.
- It also changes when Project Context changes, duplicating the Project Context hash signal.
- It cannot answer which raw template version produced the projection.

This may be useful as an additional rendered-input hash someday, but it should not replace `ProjectionPromptSourceHash`.

## Recommended Resolution

Use Option A as the core fix. Add Option C only if the tests need a clean way to simulate source-hash drift without editing prompt templates.

Implementation shape:

1. Add `PromptTemplateMetadata`.
2. Add projection metadata lookup to `RoadmapPromptCatalog`.
3. Optionally add runtime metadata lookup for symmetry and future provenance work.
4. Change `ProjectionCache` to write the generated projection prompt `SourceHash`.
5. Change stale detection to mark a projection stale when either Project Context hash or projection prompt source hash differs.
6. Include stale details in the blocked artifact so users can tell whether the block came from Project Context drift, prompt source drift, or both.
7. Decide how to treat legacy rows whose hash equals `SHA256(ProjectionPromptName)`.

Recommended legacy policy:

- Do not silently treat legacy name-hash rows as fresh.
- Treat them as stale or unknown provenance for `StaleProjectionPolicy.Block`.
- Tell the user to delete/regenerate the projection so the manifest can record real generated prompt provenance.

## Manifest Schema Considerations

The minimal fix can keep the existing manifest schema and only correct the value written to `Projection Prompt Source Hash`.

A fuller provenance fix can add `Projection Prompt Type`, but that is a schema migration:

- Append the column rather than inserting it before existing columns, or update the parser to support both layouts.
- Preserve loading of existing 11-column manifests.
- For old rows without prompt type, use an empty or `Unknown` prompt type and still evaluate source-hash freshness.

Adding prompt type improves auditability, but the stale-detection bug can be fixed without it.

## Acceptance Criteria

- `ProjectionPromptSourceHash` equals the generated projection prompt class `SourceHash`, not a hash of the prompt name.
- Existing Project Context hash staleness still works.
- Existing projection prompt source hash drift marks the projection stale even when Project Context is unchanged.
- `StaleProjectionPolicy.Block` blocks prompt-source stale projections.
- Blocked evidence identifies prompt source drift distinctly from Project Context drift.
- Legacy name-hash manifest rows are not silently treated as fresh.
- A projection file without a manifest row is not silently certified as fresh unless that behavior is explicitly retained and documented.
- Manifest round-trip tests assert `ProjectionPromptSourceHash`, not only `ProjectionHash`.
- Metadata coverage tests assert every `ProjectionRegistry.All` entry has generated projection prompt metadata.

## Suggested Tests

- `ProjectionCache_records_generated_projection_prompt_source_hash`
- `ProjectionCache_marks_projection_stale_when_prompt_source_hash_changes`
- `ProjectionCache_blocks_prompt_source_stale_projection_for_block_policy`
- `ProjectionCache_preserves_project_context_stale_detection`
- `ProjectionCache_does_not_silently_freshen_legacy_name_hash_manifest_row`
- `ProjectionCache_handles_existing_projection_without_manifest_as_unproven`
- `ProjectionManifest_round_trips_projection_prompt_source_hash`
- `RoadmapPromptCatalog_exposes_metadata_for_all_registered_projection_prompts`
