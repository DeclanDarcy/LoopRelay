# Milestone 9: Decision Consumption Integration

## Goal

make accepted resolved decisions direct execution with explicit influence traceability.

## Work

- [ ] Extend or wrap `ExecutionDecisionProjection` into `ExecutionDecisionContext` with:
  - [ ] directives
  - [ ] constraints
  - [ ] priorities
  - [ ] architecture rules
  - [ ] conflicts
  - [ ] diagnostics
- [ ] Keep compatibility with existing `ExecutionConstraint` and `ExecutionDirective` prompt rendering.
- [ ] Add `ExecutionDecisionPriority`.
- [ ] Add `ExecutionArchitectureRule`.
- [ ] Add projection rules:
  - [ ] include accepted resolved decisions
  - [ ] include active architectural direction
  - [ ] include active constraints and priorities
  - [ ] exclude open decisions, rejected decisions, deferred decisions, archived decisions, superseded decisions, unresolved proposals, and blocked decisions
  - [ ] expose only the replacement decision when supersession exists
- [ ] Strengthen conflict detection:
  - [ ] contradictory positive/negative directives
  - [ ] mutually exclusive architecture rules
  - [ ] superseded authority still projecting
  - [ ] execution request/milestone contradicting active decision
- [ ] Persist projection diagnostics:
  - [ ] included decisions
  - [ ] excluded decisions
  - [ ] superseded decisions
  - [ ] projected statements
  - [ ] conflicts
- [ ] Add influence traces per execution session:
  - [ ] decision id
  - [ ] projected directive/constraint/priority/rule
  - [ ] prompt section
  - [ ] execution session id
  - [ ] adherence observation when available
- [ ] Extend execution UI to show influencing decisions and directive source details.
- [ ] Update prompt builder to render priorities and architecture rules separately while preserving constraints/directives.

## Tests

- [ ] Accepted resolved decisions project.
- [ ] Unresolved proposals never project.
- [ ] Rejected, archived, deferred, and superseded decisions do not project.
- [ ] Supersession projects only the active replacement.
- [ ] Conflicting directives fail validation or block launch.
- [ ] Execution prompt includes constraints, directives, priorities, and architecture rules.
- [ ] Influence trace can answer which decisions affected an execution session.

## Exit Criteria

- [ ] Every execution session can explain which decisions directed it and why.
- [ ] Execution receives no unresolved decision authority.
- [ ] A generated recommendation can be human-resolved, projected to execution, and measured for human authoring burden before Tier 1 hardening work begins.
