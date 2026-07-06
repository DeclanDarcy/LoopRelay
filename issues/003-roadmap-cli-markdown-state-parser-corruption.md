# Roadmap CLI Markdown state parser corruption

## Severity

High

Confidence: High.

## Audit status

Verified against the current codebase.

The issue is real for persisted roadmap state, projection manifest metadata, and
artifact lifecycle metadata. All three stores use Markdown tables as machine-readable
state and split rows on every `|` before they have recognized escaped delimiters.

Scope correction: the current state document only escapes selected fields. Transition
intent values and retired-epic fields are escaped, but active artifact rows, last
transition paths, blockers, and summary fields are interpolated directly. That means
some values are corrupted even without the misleading `\|` escape story: the literal
pipe is written into the table and becomes a real delimiter on reload. Projection
manifest rows do escape pipes, but the loader still splits first, so escaped pipes
also corrupt that store. Artifact lifecycle rows do not escape pipes at all.

The same raw split idiom also appears in `RoadmapResumePlanner.FindDeclaredEpicPath`
and `InvariantValidator.FindDeclaredEpicPath` when reading generated milestone spec
tables. Those sites are not the authoritative persisted state stores covered by this
issue, but they should be cleaned up if a shared Markdown table reader is fixed.

## Finding

Roadmap CLI persists machine-readable state as Markdown tables, escapes pipe characters as `\|`, and then reparses rows by splitting on every pipe before unescaping.

Affected code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs`
- `src/CommandCenter.Roadmap.CLI/MarkdownTableParser.cs`
- `src/CommandCenter.Roadmap.CLI/ProjectionManifestStore.cs`
- `src/CommandCenter.Roadmap.CLI/ArtifactLifecycleStore.cs`

`RoadmapStateStore.Escape` writes `\|`, but `MarkdownTableParser.SplitRow` uses
`Split('|')` first. Any escaped pipe inside a table cell is still treated as a
delimiter, shifting later columns. Projection manifest parsing has the same bug in a
local loader. Artifact lifecycle parsing uses raw split without an escape pass.

## Verified behavior

### Shared Markdown table parser

`MarkdownTableParser.ParseTables` identifies a header/separator pair, then parses
every row with:

- `line.Trim('|')`
- `.Split('|')`
- `.Replace("\\|", "|")` on each already-split cell

This cannot preserve escaped pipes because the delimiter split happens before
unescaping. A value rendered as `A\|B` is parsed as two cells, `A\` and `B`, not one
cell containing `A|B`. Later cells shift right and may be ignored, depending on the
consumer.

All parsers using `MarkdownTableParser.ParseFieldTable` or `ParseTables` inherit this
behavior, including selection/audit/completion output parsers. This issue focuses on
persisted state because corruption there survives process restarts and drives resume
decisions.

### Roadmap state store

`RoadmapStateStore.SaveAsync` writes `.agents/state.md` as the only authoritative
state document. `LoadAsync` reads that same Markdown document and reconstructs
`RoadmapStateDocument`.

Confirmed corruption paths:

- `TransitionIntent.Intent` is escaped on save. If it ever contains `|`, reload
  truncates it before the pipe because `ParseFieldTable` maps `Value` to the second
  split cell.
- `TransitionIntent.EvidencePaths` joins escaped values with `<br>`. A path
  containing `|` is split before unescaping, so the loaded evidence list loses the
  suffix after the pipe. The `<br>` delimiter is also overloaded with cell text, so a
  literal `<br>` inside an evidence path cannot round trip.
- `RetiredEpic` fields are escaped on save. An epic ID or name containing `|` shifts
  `Retired At`, `Audit Evidence`, and `Primary Reason`; the malformed timestamp then
  falls back to `DateTimeOffset.MinValue`, and the row can represent the wrong stable
  identity.
- `BlockerRow` fields are not escaped on save. A blocker reason or required next step
  containing `|` immediately creates extra cells and reloads as truncated text.
- `ActiveArtifacts` and `LastTransition` fields are not escaped on save. Path/output
  values containing `|` can truncate `LastTransition.Output`, which is later used by
  resume safety checks for incomplete transitions.

The current tests only cover simple values:

- `RoadmapStateStoreTests.Writes_required_sections_and_round_trips_state`
- `RoadmapStateStoreTests.Loads_legacy_retired_exclusions_as_retired_epics_but_ignores_workflow_commands`

They do not cover `|`, `\`, `<br>`, malformed rows, or delimiter-bearing blocker and
evidence text.

### Projection manifest store

`ProjectionManifestStore.Render` writes `.agents/projections/manifest.md` with
`EscapeCell`, which replaces backslashes with `\\`, pipes with `\|`, and newlines with
spaces. `LoadAsync` then parses each row with:

- `trimmed.Trim('|').Split('|')`
- `Select(UnescapeCell)`

Because splitting happens first, escaped pipes in any field create phantom columns.
This can corrupt:

- `RuntimePromptName`, causing `ProjectionManifest.Find(runtimePrompt)` to miss an
  existing entry.
- `ProjectionPath`, causing readiness checks to reason about the wrong artifact.
- `CausalInputs`, which are JSON embedded inside a Markdown table cell; a pipe inside
  serialized input data makes JSON deserialization fail and silently returns an empty
  causal input list.
- `ProjectContextFiles`, because the manifest uses `;` as a list delimiter with no
  reversible escaping.
- `LastValidationError`, because validation details are free-form text and likely to
  contain punctuation or snippets copied from Markdown.

The versioned row shape makes the risk sharper: the loader accepts `cells.Length >= 16`
for the current shape and then indexes fixed positions. Any inserted phantom cell before
column 15 can keep the row "valid" while silently assigning later fields from the wrong
columns.

### Artifact lifecycle store

`ArtifactLifecycleStore.SaveAsync` writes `.agents/artifacts/lifecycle.md` with raw
interpolation:

- `Path`
- `State`
- `UpdatedAt`
- `Notes.Replace('\n', ' ')`

`LoadAsync` parses with `trimmed.Trim('|').Split('|')`. There is no pipe escaping or
unescaping. Lifecycle notes are populated from promotion/evidence failure reasons and
are free-form enough to contain `|`. A pipe in the path or notes shifts fields. A pipe
before the `State` cell can make `Enum.TryParse` fail and mark an artifact `Missing`;
a pipe in notes truncates diagnostic detail.

Lifecycle state feeds `RoadmapArtifactSnapshot` and `InvariantValidator`. Corruption can
make a ready artifact look missing, let a blocked/superseded artifact look usable, or
hide duplicate active-epic lifecycle records.

## Related fragile parser sites

The following code uses the same raw table-row split pattern outside the three
persisted stores:

- `RoadmapResumePlanner.FindDeclaredEpicPath`
- `InvariantValidator.FindDeclaredEpicPath`

Both read an `| Epic Path | ... |` row from milestone spec content. A delimiter-bearing
epic path can cause the resume planner or invariant validator to compare the wrong
declared epic path. This should be fixed with the same table parser if one remains, but
it is secondary to the persisted-state corruption.

The LLM-output parsers (`SelectionParser`, `EpicPreparationAuditParser`,
`CompletionEvaluationParser`, `RoadmapExecutionOutcomeInterpreter`, and
`EpicArtifactPromotion`) also use `MarkdownTableParser`. They can misparse escaped pipes
in model-authored tables, but those failures usually happen in the current transition
rather than silently poisoning future reloads. They still need regression coverage if
the shared parser changes.

## Impact

Any LLM-authored or user-authored value containing `|` can corrupt persisted workflow state on reload. Examples include blocker reasons, retired epic names, lifecycle notes, validation errors, and decision text.

This can lead to:

- Lost or truncated evidence paths.
- Incorrect transition intent.
- Corrupted retired epic identity, allowing a retired epic to be selected again.
- Invalid or stale projection metadata being misread.
- Resume planning from the wrong state or with incomplete safety data.
- Lifecycle readiness drift, where ready/executing/blocked/superseded artifacts are
  classified incorrectly.
- Silent loss of projection causal inputs and stale-reason evidence.
- Recovery plans that omit or misroute the evidence artifact the operator needs to
  inspect.

The failure is not limited to exotic user input. Markdown table text, exception
messages, copied command output, code snippets, and validation details commonly contain
`|`. Backslashes also have ambiguous behavior because only `ProjectionManifestStore`
escapes them; `RoadmapStateStore` escapes only pipes, and `ArtifactLifecycleStore` does
not escape either. The current `<br>` joiner for evidence paths is not reversible if
literal path text contains `<br>`.

## Meaningful solution options

### Option 1: Make JSON the authoritative state and keep Markdown as a projection

Move machine state out of Markdown tables and keep Markdown only as a human-readable projection.

The robust design is:

- Add a canonical JSON state file, for example `.agents/state.json`.
- Make `RoadmapStateStore.SaveAsync` write both:
  - `.agents/state.json` as the authoritative machine-readable document.
  - `.agents/state.md` as a rendered view for humans.
- Make `RoadmapStateStore.LoadAsync` prefer JSON and fall back to Markdown only for legacy state migration.
- Apply the same pattern to projection manifest and lifecycle, for example
  `.agents/projections/manifest.json` and `.agents/artifacts/lifecycle.json`.
- Keep Markdown rendering strictly one-way. Do not parse it during normal operation.
- Add schema/version fields to the JSON documents so future migrations are explicit.
- Preserve deterministic ordering and stable formatting so diffs remain readable.

Pros:

- Removes delimiter parsing from the correctness path.
- Gives evidence paths, lists, timestamps, enums, and JSON causal inputs native
  structure.
- Makes future migrations and compatibility checks explicit.
- Lets Markdown remain useful to humans without pretending to be lossless storage.

Cons:

- Larger change touching three stores, path constants, tests, and any scripts that read
  `.agents/*.md` directly.
- Requires a migration/fallback path for existing Markdown-only repos.
- Needs careful dual-write semantics so JSON and Markdown do not diverge during the
  transition.

This is the recommended long-term fix.

### Option 2: Add canonical JSON only for `.agents/state.md` first

Introduce `.agents/state.json` for `RoadmapStateStore` now, but leave projection
manifest and lifecycle as Markdown until follow-up work.

Suggested implementation:

- Add a new `RoadmapArtifactPaths.StateJson` constant.
- Serialize `RoadmapStateDocument` with explicit DTOs rather than relying on record
  constructor names as an accidental contract.
- Save JSON first, then render Markdown from the same in-memory document.
- Load JSON when present.
- If JSON is absent, load the legacy Markdown state, then write JSON on the next save.
- Keep `.agents/state.md` visually unchanged except for an optional "authoritative
  source: state.json" note.

Pros:

- Fixes the most dangerous resume-state corruption with a smaller first slice.
- Preserves current human-facing state file.
- Builds migration infrastructure before changing every artifact store.

Cons:

- Projection manifest and lifecycle remain vulnerable.
- Resume planning still depends on corrupted manifest/lifecycle reads.
- Creates a temporary mixed authority model that must be documented clearly.

This is a good incremental slice if the full JSON migration is too large.

### Option 3: Replace Markdown table parsing with an escaped-cell scanner

Keep the Markdown files as the only machine-readable storage, but replace every raw
`Split('|')` with a parser that scans the row character by character and treats `\|`
as literal cell content.

Minimum parser behavior:

- Ignore leading and trailing table-boundary pipes.
- Split only on unescaped pipes.
- Preserve escaped backslashes unambiguously.
- Normalize or reject malformed rows with too few or too many cells, instead of silently
  shifting columns.
- Share the same parser between `MarkdownTableParser`, `ProjectionManifestStore`,
  `ArtifactLifecycleStore`, `RoadmapResumePlanner`, and `InvariantValidator`.
- Add one shared table-cell encoder/decoder and use it consistently in all renderers.

If Markdown parsing must remain, replace the splitter with a parser that scans cell text and ignores escaped pipes, and add tests for `\|`, backslashes, newlines, and malformed rows. JSON is still preferable because the state is not naturally tabular.

Pros:

- Smaller storage-format change.
- Can be applied quickly to all existing Markdown readers.
- Keeps compatibility with current artifact paths.

Cons:

- Markdown tables still do not naturally model nested lists, embedded JSON, multiline
  text, or arbitrary user/model content.
- Backward compatibility with already-corrupted rows is hard to define.
- The codebase would still rely on a custom Markdown subset for state integrity.

This is an acceptable short-term hardening step, not the best final design.

### Option 4: Use fenced JSON blocks inside Markdown files

Keep the file names and human-readable Markdown, but embed authoritative JSON in fenced
blocks, for example:

````markdown
## Machine State

```json
{ "...": "..." }
```
````

The loader reads only the fenced JSON block. The tables become rendered summaries.

Pros:

- Preserves single-file ergonomics for operators who expect `.agents/state.md`.
- Avoids table delimiter parsing while keeping Markdown as the artifact extension.
- Can be introduced per store without changing artifact paths.

Cons:

- Editing the Markdown file by hand becomes ambiguous unless the JSON block is clearly
  marked authoritative.
- Fenced block extraction needs its own strict parser and migration tests.
- Still mixes human projection and machine state in one file, so merge conflicts and
  partial edits remain riskier than separate JSON.

This is a compromise when changing artifact paths is too disruptive.

### Option 5: Base64 or JSON-encode each table cell

Keep the existing table shapes, but encode every value cell as a reversible scalar
format such as base64url or JSON string literals.

Pros:

- Minimal row/column migration.
- Avoids delimiter collision if every cell is encoded before rendering.

Cons:

- Makes human-readable tables much less readable.
- Requires all legacy readers and manual tooling to understand the encoding.
- Still leaves malformed-row handling and versioning weak.

This is not recommended except as an emergency compatibility bridge.

### Option 6: Fail closed on unsafe Markdown table content

As an interim guard, refuse to persist or load unsafe Markdown table content:

- Throw before saving any table cell containing `|`, `<br>`, or unsupported backslash
  escape sequences.
- On load, reject rows with an unexpected cell count instead of silently accepting shifted
  columns.
- Persist a blocker/evidence artifact that tells the operator which field is unsafe.

Pros:

- Prevents silent corruption before the storage migration lands.
- Smallest possible protective change.

Cons:

- Does not support legitimate values containing delimiters.
- Can block workflows because free-form validation or runtime text includes a pipe.
- Requires user-facing recovery guidance for already-written files.

This can be a temporary guard paired with Option 1, 2, or 3.

## Recommended approach

Use Option 1 as the target design and Option 2 as the first implementation slice if
delivery needs to be incremental.

Do not rely solely on an escaped Markdown parser for the long term. The state document,
projection manifest, and lifecycle file are structured machine data with nested lists,
enums, timestamps, hashes, causal inputs, and free-form diagnostics. JSON fits those
contracts directly; Markdown tables are only suitable as a rendered view.

## Implementation details to preserve compatibility

For a full JSON migration:

- Add new path constants for JSON artifacts rather than overloading Markdown path
  constants.
- Introduce DTOs with an explicit `schemaVersion`.
- Preserve current Markdown load fallback for at least one migration window.
- On load, prefer JSON. If JSON is absent, load Markdown, validate the reconstructed
  document, and continue. Do not write JSON from a corrupted Markdown document unless
  validation passes.
- On save, write the canonical JSON document and then render Markdown from the same DTO.
- Treat Markdown tables as display-only after JSON exists. A hand edit to Markdown alone
  should not affect machine behavior.
- For projection manifest causal inputs, store the list as JSON array properties rather
  than JSON serialized into a Markdown cell.
- For lifecycle, store one record per path with notes as plain JSON string data.
- Add malformed-legacy-state diagnostics that name the file, section, row, expected
  column count, and actual column count.

For a parser hardening slice:

- Add a shared `MarkdownTableCellCodec` or equivalent.
- Use it in `MarkdownTableParser`, `ProjectionManifestStore`, and
  `ArtifactLifecycleStore`; remove all local `Trim('|').Split('|')` parsing.
- Escape the currently unescaped `RoadmapStateStore` fields or route all state rendering
  through the shared encoder.
- Escape lifecycle fields on save.
- Reject or explicitly preserve extra cells. Silent column shifting should be treated as
  corruption.
- Add tests around exact round trips and malformed rows before changing behavior.

## Acceptance Criteria

- State reload preserves values containing `|`, `\`, `<br>`, semicolons, commas, and
  Markdown table syntax without column shifting.
- Transition intent evidence paths survive save/load round trips exactly, including
  multiple paths and delimiter-bearing path text.
- Retired epic ID, name, audit evidence path, primary reason, and retired timestamp
  survive save/load round trips exactly.
- Blocker reasons and required next steps survive save/load round trips exactly.
- `LastTransition.Output` survives save/load round trips exactly for single and
  multi-output transitions.
- Projection manifest entries with `|` in validation errors, causal input values,
  context file names, hashes, or paths do not corrupt any manifest column.
- Projection manifest causal inputs do not silently become empty because a Markdown cell
  split made the embedded JSON invalid.
- Artifact lifecycle paths and notes containing `|` do not corrupt path, state,
  timestamp, or notes.
- Malformed legacy Markdown rows fail closed with diagnostics instead of silently
  shifting columns.
- Legacy `.agents/state.md` can still be read when `.agents/state.json` is absent, but
  JSON is preferred when present.
- Tests cover `RoadmapStateStore`, `ProjectionManifestStore`, `ArtifactLifecycleStore`,
  the shared Markdown parser if retained, and the two `Epic Path` reader sites in resume
  and invariant validation.
