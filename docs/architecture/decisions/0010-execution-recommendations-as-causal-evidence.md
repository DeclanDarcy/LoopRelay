# ADR-0010: Bind Execution Recommendations to Decision Products and Policy Evaluations

- Status: Accepted
- Date: 2026-07-11
- Supersedes: clarifies ADR-0005
- Owners: Policy Authority, History Authority, Runtime Authority

## Decision

An execution recommendation is an immutable advisory fact bound to exactly one canonical decision
product version and its source attempt, session, and turn. It contains recommended model and effort
but no effective permissions, sandbox, provider, approvals, recovery behavior, or runtime profile.

Policy Authority evaluates that fact against the current decision product, policy identity, and a
durable provider-capability snapshot. Its immutable evaluation records one of `Accepted`,
`Constrained`, `Rejected`, `IgnoredByPolicy`, `Stale`, `Invalid`, or `Unsupported`, together with
reasons and the complete effective runtime profile.

Staleness is causal, never timestamp-based. A recommendation is current only when its decision
product identity matches and its schema remains evaluable. A policy or capability change may
reevaluate the same immutable recommendation and append a new evaluation.

Execution receives only an `ExecutionAuthorization` containing decision-product, runtime-profile,
policy-evaluation, prompt-fact, input-manifest, and causal identities. Runtime loads the profile
through the durable evaluation; recommendation values never reach `AgentSpecs` directly.

Decision products, recommendation evidence, evaluations, and runtime profiles remain separate
facts. Filesystem recommendation files are compatibility projections and never runtime authority.

## Consequences

- `LoopArtifacts` coordinates persistence and projection but performs no policy evaluation.
- Superseding or retiring decisions invalidates the live recommendation projection.
- Missing, stale, rejected, or ignored recommendations still yield an explicitly governed fallback
  profile and durable reason.
- Legacy recommendation files require explicit causal import or remain non-authoritative history.
