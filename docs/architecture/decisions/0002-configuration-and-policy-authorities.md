# ADR-0002: Separate Configuration Resolution from Policy Resolution

- Status: Accepted
- Date: 2026-07-11
- Owners: Configuration Authority, Policy Authority, Runtime Authority

## Context

The merge contains direct model and effort configuration, environment-driven resume and recovery behavior, permission settings, and a convergence implementation that resolves operational policy. Treating all of these as one configuration-to-policy shortcut would obscure the difference between configured input and an authorized execution decision.

## Decision

Configuration Authority and Policy Authority remain separate.

The required flow is:

```text
Raw configuration
  -> Configuration Resolver
  -> Validated Configuration with provenance
  -> Policy Authority plus capabilities, workspace state, workflow and product policy
  -> Resolved Session Policy
  -> Runtime enforcement
```

Configuration Authority answers what was configured and whether it is valid. It does not select runtime behavior.

Policy Authority decides the effective model, effort, permissions, sandbox, approval posture, resume behavior, recovery behavior, retry and operational limits for an attempt. It records both the resolved values and the provenance and evidence used to decide them.

Runtime Authority receives only the resolved session policy. Raw configuration, ambient environment values, and feature-local defaults may not reach `AgentSpecs`, provider adapters, or workflow handlers directly.

Every attempt references one final resolved policy identity. Configuration identity and evidence remain linked inputs, not competing policy identities.

## Consequences

- Existing `BrainConfiguration` behavior must move behind Configuration Authority and Policy Authority rather than disappear or remain a direct constructor dependency.
- Environment overrides are configuration inputs with explicit precedence and provenance.
- Role-specific session policies are allowed, but each is produced by Policy Authority and causally linked to its attempt.
- Policy resolution must reject unsupported or ineffective configured values instead of silently ignoring them.
