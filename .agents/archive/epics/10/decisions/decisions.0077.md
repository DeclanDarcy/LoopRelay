# Decisions

## Newly Authorized

- Treat the completed broad Execution explainability migration as a meaningful Milestone 8 checkpoint and the fourth major domain validated on the shared explainability layer.
- Continue preserving the backend/UI authority split for Milestone 8:
  - adapters remain presentation-only,
  - backend projections remain authoritative for semantic facts,
  - React may reorganize authoritative facts for rendering but must not derive domain outcomes.
- Stop further Execution-density refinement during Milestone 8; any `ExecutionHistoryPanel` density tuning belongs in Milestone 9 product cohesion.
- Move next to Reasoning as the next Milestone 8 domain because it is the richest stress test for evidence, uncertainty, provenance, reachability, and diagnostics.
- Migrate Reasoning explainability in this order:
  - confidence rationale,
  - missing or unreachable evidence,
  - reconstruction scope,
  - provenance,
  - reachability,
  - diagnostics.
- Add Reasoning adapter characterization tests that verify preservation of:
  - evidence,
  - provenance,
  - confidence rationale,
  - reconstruction scope,
  - diagnostics,
  - uncertainty,
  - reachability.
- Continue prohibiting Reasoning adapters from deriving:
  - reasoning confidence,
  - materialization state,
  - reconstruction success,
  - evidence reachability,
  - lifecycle risk,
  - authority-boundary conclusions.
- After Reasoning migrates successfully while remaining presentation-only, continue to remaining Continuity surfaces before transitioning to Milestone 9.
