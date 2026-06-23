# Decisions

## Newly Authorized

- Pause before opening Milestone 6 to certify M5 readiness.
- Treat the smoke-test fix as the correct resolution pattern because it used explicit accessibility labels and scoped queries rather than weakened assertions.
- Certify the M5 invariant that Materialization Review remains advisory only.
- Before M6 implementation, verify that materialization-review recommendations can be ignored without changing repository correctness.
- The M6 pre-check must confirm that ignoring every materialization-review recommendation leaves these functioning identically:
  - Reasoning Graph
  - Reasoning Queries
  - Reasoning Reconstruction
  - Decision Lifecycle
  - Operational Context
  - Governance
  - Execution
- Materialization-review output may affect human understanding only.
- Materialization-review output must not affect behavior, persistence, projection, reconstruction, or authority.
- Treat any dependency from graph generation, reconstruction quality, or reasoning storage mutation on review recommendations as authority leakage.
- If the advisory-independence check passes, start Milestone 6 from the green baseline.
- Evaluate Milestone 6 primarily on whether existing boundaries remain intact:
  - no graph authority
  - no narrative authority
  - no materialized hypotheses
  - no materialized alternatives
  - no materialized directions
  - no materialized contradictions
