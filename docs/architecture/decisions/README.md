# Architecture Decision Register

These decisions govern the `merge-4` convergence work. They are accepted Phase A inputs to conflict resolution, not descriptions of whichever branch happens to compile first.

Accepted decisions:

1. [ADR-0001: Evolve the Logical Workspace Schema from v8 to v9](0001-logical-schema-v9.md)
2. [ADR-0002: Separate Configuration Resolution from Policy Resolution](0002-configuration-and-policy-authorities.md)
3. [ADR-0003: Keep History Authority Logical and Use SQLite as Its Current Storage](0003-logical-history-authority.md)
4. [ADR-0004: Compose Templates and Policy Profiles Before Prompt Hashing](0004-canonical-prompt-composition.md)
5. [ADR-0005: Treat Agent Execution Recommendations as Evidence](0005-execution-recommendations-as-evidence.md)
6. [ADR-0006: Resolve Merge Conflicts by Target Authority](0006-conflict-resolution-by-authority.md)
7. [ADR-0007: Make Schema Lineage, Evidence Sets, and Persistence Projections Explicit](0007-persistence-lineage-evidence-and-projection.md)
8. [ADR-0008: Separate Single-Attempt Execution from Recovery and Effects](0008-single-attempt-runtime-and-recovery-coordinator.md)
9. [ADR-0009: Canonical Prompt Dispatch Gateway](0009-canonical-prompt-dispatch-gateway.md)
10. [ADR-0010: Bind Execution Recommendations to Decision Products and Policy Evaluations](0010-execution-recommendations-as-causal-evidence.md)
11. [ADR-0011: Establish a Thin Application Boundary](0011-thin-application-boundary.md)

## Governance

Accepted decisions are immutable historical records. A changed decision requires a new ADR that explicitly supersedes the prior record and identifies compatibility and migration consequences.

Conflict resolution begins only after these decisions are accepted. Implementation order is contracts, persistence, runtime, prompt and feature services, composition, then tests and certification.
