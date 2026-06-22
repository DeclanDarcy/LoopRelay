# Milestone 8: Execution Consumption

## Goal

expose governed resolved decisions to execution as constraints and directives without leaking unresolved reasoning into execution context.

## Backend Work

- [x] Add `ExecutionDecisionProjection`, `ExecutionConstraint`, `ExecutionDirective`, `ExecutionDecisionConflict`, and consumption diagnostics.
- [x] Implement `IDecisionProjectionService`.
- [x] Filter only resolved, execution-relevant decisions without blocking governance findings.
- [ ] Project architectural constraints, implementation directives, technology choices, workflow policies, and repository conventions.
- [x] Exclude rejected proposals, deferred decisions, review notes, draft proposals, unresolved candidates, stale proposals, historical analysis, and decisions with blocking governance findings.
- [x] Extend `ExecutionContext` with governed decision projections and diagnostics.
- [x] Update `ExecutionContextService` to inject `IDecisionProjectionService`.
- [x] Update `ExecutionPromptBuilder` to include governed decision constraints and directives in a stable section.
- [x] Preserve raw `CurrentDecisions` artifact inclusion only as backward-compatible context until structured projection is certified.
- [x] Add conflict detection when an execution request or milestone contradicts governed decision constraints.

## Tests

- [x] Governed resolved-only filtering tests.
- [x] Blocking governance exclusion tests.
- [x] Constraint projection tests.
- [x] Directive projection tests.
- [x] Execution context integration tests.
- [x] Prompt ordering tests.
- [x] Conflict detection tests.
- [x] Tests proving unresolved proposals are not included.

## Exit Criteria

- [ ] Execution consumes governed authoritative decisions.
- [ ] Execution cannot mutate, govern, or resolve decisions.
- [ ] Decision influence is traceable from execution context back to `DEC-*`.
- [ ] Governance problems are discovered before projection into execution.
