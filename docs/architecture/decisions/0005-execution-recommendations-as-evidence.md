# ADR-0005: Treat Agent Execution Recommendations as Evidence

- Status: Accepted
- Date: 2026-07-11
- Owners: Policy Authority, Runtime Authority

## Context

The current branch lets a decision agent recommend model and effort for a subsequent execution agent. Passing that recommendation directly into runtime construction would give agent output implicit policy authority.

## Decision

An agent execution recommendation is typed evidence. It is never an effective execution profile by itself.

Policy Authority evaluates the recommendation together with validated configuration, provider capabilities, workspace and workflow state, product policy, cost and safety limits, and the recommendation's causal input snapshot. Policy Authority then accepts, constrains, overrides, or rejects it and produces the resolved execution profile referenced by the attempt.

The evidence record captures the recommendation body, producer session and turn, input snapshot, creation time, and validation outcome. Stale, malformed, unsupported, or unauthorized recommendations cannot influence runtime behavior.

Permission, approval, sandbox, and network ceilings cannot be elevated by recommendation. Runtime Authority receives only the resolved execution profile.

## Consequences

- Direct `AgentSpecs.Execution(... recommendation ...)` paths must be removed.
- Adaptive model and effort selection can be preserved without creating a new authority.
- Policy tests must cover acceptance, constraint, override, rejection, staleness, missing evidence, and unsupported provider capabilities.
- If recommendation production is later retired, Policy Authority continues to resolve a profile from its remaining evidence.
