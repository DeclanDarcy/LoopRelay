# Decisions

## Newly Authorized

- Taxonomy classification basis must be implemented before any Milestone 7 UI work.
- `DecisionAnalysisService` must assign structured taxonomy basis for every decision signal, including matched rules, matched evidence, heuristic fallback, fallback reason, and diagnostics.
- Taxonomy basis must be embedded within `DecisionAssimilationRecord` so consumers can answer why a decision was classified and how that classification affected assimilation without joining multiple projections.
- Taxonomy basis is immutable generation-time evidence. Historical proposals must retain the basis that justified their original classification even if taxonomy rules evolve later.
- Backend regression tests for this slice must cover rule-based classification, heuristic fallback, ambiguous classification, excluded classifications, and omitted-by-limit records retaining taxonomy basis.
- This slice remains backend-only; do not introduce UI until classification semantics stabilize.
