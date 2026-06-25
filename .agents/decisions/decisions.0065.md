# Decisions

## Newly Authorized

- Treat the typed semantic-change slice as an architectural improvement because it moves semantic evolution away from compatibility projections and toward explicit backend-owned domain concepts.
- Preserve `UnderstandingDiffService` as the single authority for deterministic modification identity and typed semantic change mapping.
- Keep React presentation-only for semantic evolution: it may group backend-provided semantic values directly and may retain compatibility fallback grouping for legacy data, but must not infer semantic authority.
- Continue Milestone 7 with compression explanation work before expanding the evolution UI.
- Use `compressionSummary.itemOutcomes` as the authoritative source for compression outcome, reason category, governing rule, threshold, rationale, and supporting evidence.
- Build `OperationalContextCompressionExplanation` as a pure renderer of backend-projected compression outcome fields.
- Add backend tests proving compression outcome categories are emitted correctly for retained, compressed, removed, duplicate removed, transient removed, resolved question, and retired risk.
- Add UI characterization tests proving React renders backend compression reason categories and evidence verbatim without synthesizing severity, classifications, or outcome interpretation.
- After compression explanation work, proceed to Milestone 7 projection-gap reconciliation and formal exit audit before starting Milestone 8 shared explainability work.
