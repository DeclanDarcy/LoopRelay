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
12. [ADR-0012: Use Specific Reason-Bearing Outcomes](0012-specific-reason-bearing-outcomes.md)
13. [ADR-0013: Recovery, Cancellation, and Profile Authority](0013-recovery-cancellation-and-profile-authority.md)
14. [ADR-0014: Durable Interaction Policy](0014-durable-interaction-policy.md)
15. [ADR-0015: Strict Storage Operation Boundaries](0015-strict-storage-operation-boundaries.md)
16. [ADR-0016: Use a One-Way, Verified Import Gateway](0016-one-way-import-gateway.md)
17. [ADR-0017: Use One Versioned Workflow Catalog Snapshot](0017-single-versioned-workflow-catalog.md)
18. [ADR-0018: Use One Durable Orchestration Kernel](0018-one-durable-orchestration-kernel.md)
19. [ADR-0019: Separate Completion Decision from Terminal Settlement](0019-completion-decision-and-settlement.md)

## Governance

Accepted decisions are immutable historical records. A changed decision requires a new ADR that explicitly supersedes the prior record and identifies compatibility and migration consequences.

Conflict resolution begins only after these decisions are accepted. Implementation order is contracts, persistence, runtime, prompt and feature services, composition, then tests and certification.
