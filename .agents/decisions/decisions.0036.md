# Decisions

## Newly Authorized

- Execution consumes `DecisionProjection`, not raw decision artifacts, as its authoritative decision input.
- Raw `.agents/decisions/decisions.md` remains compatibility context, human context, and a migration safety net during M8.
- `DecisionProjection` is now the execution-facing authority boundary for decision consumption.
- Removal of raw current-decision markdown from execution context should wait until projection completeness is proven, likely during M9 certification rather than M8.
