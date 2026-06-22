# Milestone 8: Execution Consumption

## Goal

expose governed resolved decisions to execution as constraints and directives without leaking unresolved reasoning into execution context.

## Backend Work

- [ ] Add `ExecutionDecisionProjection`, `ExecutionConstraint`, `ExecutionDirective`, `ExecutionDecisionConflict`, and consumption diagnostics.
- [ ] Implement `IDecisionProjectionService`.
- [ ] Filter only resolved, execution-relevant decisions without blocking governance findings.
- [ ] Project architectural constraints, implementation directives, technology choices, workflow policies, and repository conventions.
- [ ] Exclude rejected proposals, deferred decisions, review notes, draft proposals, unresolved candidates, stale proposals, historical analysis, and decisions with blocking governance findings.
- [ ] Extend `ExecutionContext` with governed decision projections and diagnostics.
- [ ] Update `ExecutionContextService` to inject `IDecisionProjectionService`.
- [ ] Update `ExecutionPromptBuilder` to include governed decision constraints and directives in a stable section.
- [ ] Preserve raw `CurrentDecisions` artifact inclusion only as backward-compatible context until structured projection is certified.
- [ ] Add conflict detection when an execution request or milestone contradicts governed decision constraints.

## Tests

- [ ] Governed resolved-only filtering tests.
- [ ] Blocking governance exclusion tests.
- [ ] Constraint projection tests.
- [ ] Directive projection tests.
- [ ] Execution context integration tests.
- [ ] Prompt ordering tests.
- [ ] Conflict detection tests.
- [ ] Tests proving unresolved proposals are not included.

## Exit Criteria

- [ ] Execution consumes governed authoritative decisions.
- [ ] Execution cannot mutate, govern, or resolve decisions.
- [ ] Decision influence is traceable from execution context back to `DEC-*`.
- [ ] Governance problems are discovered before projection into execution.
