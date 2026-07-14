# ADR-0017: Use One Versioned Workflow Catalog Snapshot

## Status

Accepted.

## Decision

The composition root constructs one immutable `CanonicalWorkflowCatalogSnapshot`. Its explicit semantic version and canonical SHA-256 identity cover the fully derived workflow and chain declarations, typed owner references, product schemas, normalized input/output surfaces, prompt template versions, policy/profile requirements, exact runtime capabilities, interaction and recovery contracts, and structurally derived effects.

Workflow declarations describe domain mutations and publication policy. They do not author Git mechanics. Blocking commits and required asynchronous pushes are derived from output surfaces and participate in catalog identity and certification obligations.

Catalog validation is fail-closed, deterministic, path-qualified, and runs before workspace or provider initialization. Root runs and workflow instances persist both the exact catalog identity and version. Restart resolves that exact accepted snapshot; absence or mismatch is recovery-required and never a silent upgrade.

Obligation keys derive from owner, kind, and stable semantic identity. Their content hashes change independently, so adding or changing one declaration does not renumber unrelated coverage obligations.

## Consequences

- Resolver, runtime, effect coordination, prompt lookup, and certification consume the same object graph.
- Validator, capability, interaction, recovery, and effect references require registered owners.
- Every repository read/write surface is normalized and root-scoped.
- Adding a workflow is declaration work; the orchestration kernel cannot add a workflow-specific branch.
