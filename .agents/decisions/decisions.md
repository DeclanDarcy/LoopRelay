# Decisions

## Newly Authorized

- Treat Stage 2A as complete.
- Treat Stage 2B as ready to begin.
- Preserve the Stage 2B dependency direction: economics consumes metrics; metrics must not consume economics.
- Implement Stage 2B economics primarily from `DecisionSessionMetrics`, `DecisionSessionStatistics`, and `DecisionSessionCacheMetrics` plus configurable assumptions.
- Avoid direct repository crawling from economics where possible; use the completed Stage 2A analysis layer as the economics input boundary.
- Design `DecisionSessionEconomicsDiagnostics` before implementing transfer value and reuse value scoring so economics remains explainable and certifiable.
- Include economics models, diagnostics, snapshot persistence, and deterministic scoring behavior in the next Stage 2B slice.
- No roadmap changes are warranted at this point.
