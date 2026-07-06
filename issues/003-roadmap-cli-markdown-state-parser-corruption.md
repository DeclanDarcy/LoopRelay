# Roadmap CLI Markdown state parser corruption

## Severity

High

## Finding

Roadmap CLI persists machine-readable state as Markdown tables, escapes pipe characters as `\|`, and then reparses rows by splitting on every pipe before unescaping.

Affected code:

- `src/CommandCenter.Roadmap.CLI/RoadmapStateStore.cs`
- `src/CommandCenter.Roadmap.CLI/MarkdownTableParser.cs`
- `src/CommandCenter.Roadmap.CLI/ProjectionManifestStore.cs`
- `src/CommandCenter.Roadmap.CLI/ArtifactLifecycleStore.cs`

`RoadmapStateStore.Escape` writes `\|`, but `MarkdownTableParser.SplitRow` uses `Split('|')` first. Any escaped pipe inside a table cell is still treated as a delimiter, shifting later columns. The same pattern appears in projection manifest and lifecycle parsing.

## Impact

Any LLM-authored or user-authored value containing `|` can corrupt persisted workflow state on reload. Examples include blocker reasons, retired epic names, lifecycle notes, validation errors, and decision text.

This can lead to:

- Lost or truncated evidence paths.
- Incorrect transition intent.
- Corrupted retired epic identity, allowing a retired epic to be selected again.
- Invalid or stale projection metadata being misread.
- Resume planning from the wrong state or with incomplete safety data.

## Proposal

Move machine state out of Markdown tables and keep Markdown only as a human-readable projection.

The robust design is:

- Add a canonical JSON state file, for example `.agents/state.json`.
- Make `RoadmapStateStore.SaveAsync` write both:
  - `.agents/state.json` as the authoritative machine-readable document.
  - `.agents/state.md` as a rendered view for humans.
- Make `RoadmapStateStore.LoadAsync` prefer JSON and fall back to Markdown only for legacy state migration.
- Apply the same pattern to projection manifest and lifecycle if they remain machine inputs.
- Keep Markdown rendering strictly one-way. Do not parse it during normal operation.

If Markdown parsing must remain, replace the splitter with a parser that scans cell text and ignores escaped pipes, and add tests for `\|`, backslashes, newlines, and malformed rows. JSON is still preferable because the state is not naturally tabular.

## Acceptance Criteria

- State reload preserves cell values containing `|`, `\`, `<br>`, and commas.
- Transition intent evidence paths survive save/load round trips exactly.
- Retired epic identity survives save/load round trips exactly.
- Projection manifest validation errors containing `|` do not corrupt manifest columns.
- Legacy `.agents/state.md` can still be read when `.agents/state.json` is absent.
