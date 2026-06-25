# Decisions

## Newly Authorized

- Treat the core Execution explainability migration as a solid Milestone 8 increment that preserves Milestone 5 authority boundaries.
- Continue using the established Milestone 8 migration pattern for Execution:
  - adapters remain presentation-only,
  - backend execution projections remain authoritative,
  - shared explainability components provide a common rendering vocabulary only.
- Recognize Milestone 8 as now exercised across four major domains:
  - Workflow,
  - Governance,
  - Decisions,
  - Core Execution.
- Continue the remaining Execution migration before beginning Reasoning, in this order:
  - artifact diagnostics and context-threshold explanations,
  - execution event consequences,
  - execution history and session-failure evidence,
  - generated handoff validation and review evidence.
- Continue extending Execution adapter preservation tests to verify forwarding of:
  - evidence,
  - diagnostics,
  - validation findings,
  - consequences,
  - generated review artifacts,
  - actions.
- Continue prohibiting Execution adapters from deriving:
  - execution success or failure,
  - retry decisions,
  - recovery state,
  - repository validity,
  - prompt composition,
  - conflict resolution.
- After remaining Execution surfaces are complete, move to Reasoning as the next major proving ground for evidence, uncertainty, provenance, reachability, and diagnostics while keeping the shared layer presentation-only.
