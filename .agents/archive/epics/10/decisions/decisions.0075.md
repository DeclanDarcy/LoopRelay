# Decisions

## Newly Authorized

- Treat the completed decision-domain explainability migration as an important Milestone 8 checkpoint.
- Preserve the Milestone 8 contract for subsequent slices:
  - adapters remain presentation-only,
  - adapters forward backend facts, scores, rules, reasons, evidence, findings, diagnostics, constraints, uncertainty, and actions,
  - adapters must not derive recommendation, lifecycle, governance, certification, prompt composition, recovery state, git eligibility, retry decisions, conflict resolution, or execution outcome.
- Defer product cohesion and visual density optimization to Milestone 9 after the shared presentation abstraction is fully migrated.
- Continue Milestone 8 with the remaining domains after this checkpoint:
  - Execution,
  - Reasoning,
  - Continuity.
- Execute the next slice against Execution transparency surfaces in this order:
  - prompt metadata and manifest explanation,
  - repository snapshot and execution context evidence,
  - commit/push retry evidence,
  - structured conflict diagnostics,
  - recovery findings,
  - execution diagnostics and monitoring.
- Continue adapter preservation tests and domain panel tests for each migrated Execution surface.
