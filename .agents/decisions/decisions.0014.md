# Decisions

## Newly Authorized

- Continue treating Workstream 0.5 as structural decomposition without moving workflow authority.
- Treat extracted artifact category, formatting, and dirty-path helpers as valid utility extractions because they remain meaningful independent of workflow logic.
- Continue using the M0 decomposition pattern of characterize, move, then verify.
- Proceed next with extracting markdown rendering into `src/lib/markdown.tsx`.
- Constrain `src/lib/markdown.tsx` to markdown rendering, markdown component mapping, and markdown formatting helpers only.
- Do not let markdown extraction own artifact loading, proposal loading, continuity interpretation, or operational-context semantics.
- Add markdown rendering equivalence characterization before or alongside extraction.
- Markdown characterization should protect current behavior for headings, lists, tables, links, code blocks, inline code, block quotes, and any existing custom markdown behavior.
- Continue avoiding extraction of commit preparation, proposal review, generated handoff review, and promotion workflow until a future milestone explicitly targets workflow decomposition.
- Treat M0 as having completed authority foundations, authority certification, navigation foundations, and projection foundations, with structural decomposition still in progress.
- Treat remaining M0 work as primarily organization, readability, and maintainability work rather than architectural correction.

## Next Authorized Slice

Continue Workstream 0.5 by extracting `renderMarkdown` into `src/lib/markdown.tsx` under the constraints above, with focused rendering equivalence characterization and no workflow authority migration.
