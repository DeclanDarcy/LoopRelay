# Decisions

## Newly Authorized

- Treat explicit compression outcomes such as `ResolvedQuestion` and `RetiredRisk` as backend-owned continuity semantics because they explain why information changed, not just that it changed.
- Keep `OperationalContextCompressionSummaryPanel` focused on aggregate counts, warnings, and overview facts.
- Keep `OperationalContextCompressionExplanation` focused on per-item outcome, governing rule, threshold, rationale, and supporting evidence.
- Preserve the authority boundary where the backend determines compression outcome, rule, threshold, rationale, and evidence, while React renders those fields without synthesizing meaning.
- Prioritize Milestone 7 projection-gap reconciliation and exit audit next.
- During the exit audit, verify every backend continuity projection has a corresponding UI consumer and that compatibility-string surfaces are not the primary representation where typed projections exist.
- Reconcile the existing grouped diagnostics implementation with the Milestone 7 checklist before marking grouped diagnostics complete.
- Complete compression taxonomy only where the backend truly distinguishes operations; add `Merged`, `NoiseRemoved`, or equivalents only when they correspond to distinct backend semantics.
- Build `OperationalContextEvolutionTimeline` after the semantic change taxonomy is complete enough for the timeline to consume typed backend events rather than reconstructing meaning from strings.
