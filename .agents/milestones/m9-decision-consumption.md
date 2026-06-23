# Milestone 9: Decision Consumption Integration

## Goal

make accepted resolved decisions direct execution with explicit influence traceability.

## Work

- [x] Extend or wrap `ExecutionDecisionProjection` into `ExecutionDecisionContext` with:
  - [x] directives
  - [x] constraints
  - [x] priorities
  - [x] architecture rules
  - [x] conflicts
  - [x] diagnostics
- [x] Keep compatibility with existing `ExecutionConstraint` and `ExecutionDirective` prompt rendering.
- [x] Add `ExecutionDecisionPriority`.
- [x] Add `ExecutionArchitectureRule`.
- [x] Add projection rules:
  - [x] include accepted resolved decisions
  - [x] include active architectural direction
  - [x] include active constraints and priorities
  - [x] exclude open decisions, rejected decisions, deferred decisions, archived decisions, superseded decisions, unresolved proposals, and blocked decisions
  - [x] expose only the replacement decision when supersession exists
- [x] Strengthen conflict detection:
  - [x] contradictory positive/negative directives
  - [x] mutually exclusive architecture rules
  - [x] superseded authority still projecting
  - [x] execution request/milestone contradicting active decision
- [x] Persist projection diagnostics:
  - [x] included decisions
  - [x] excluded decisions
  - [x] superseded decisions
  - [x] projected statements
  - [x] conflicts
- [ ] Add influence traces per execution session:
  - [x] decision id
  - [x] projected directive/constraint/priority/rule
  - [x] prompt section
  - [x] execution session id
  - [x] retrieval by execution session id
  - [x] retrieval by decision id
  - [ ] adherence observation when available
- [x] Add backend endpoints for execution and decision influence lookup.
- [x] Add Tauri bridge commands for execution and decision influence lookup.
- [x] Extend execution UI to show influencing decisions and directive source details.
- [x] Update prompt builder to render priorities and architecture rules separately while preserving constraints/directives.

## Tests

- [x] Accepted resolved decisions project.
- [x] Unresolved proposals never project.
- [x] Rejected, archived, deferred, and superseded decisions do not project.
- [x] Supersession projects only the active replacement.
- [x] Conflicting directives fail validation or block launch.
- [x] Execution prompt includes constraints, directives, priorities, and architecture rules.
- [x] Influence trace can answer which decisions affected an execution session.
- [x] Influence endpoints return persisted execution and decision traces.

## Exit Criteria

- [x] Every execution session can explain which decisions directed it and why.
- [x] Execution receives no unresolved decision authority.
- [x] A generated recommendation can be human-resolved, projected to execution, and measured for human authoring burden before Tier 1 hardening work begins.
