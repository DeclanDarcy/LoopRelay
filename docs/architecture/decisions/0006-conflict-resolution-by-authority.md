# ADR-0006: Resolve Merge Conflicts by Target Authority

- Status: Accepted
- Date: 2026-07-11
- Owners: Architecture Governance

## Context

The merge combines independently developed behavior across configuration, policy, persistence, history, prompts, runtime, recovery, completion, and certification. Selecting an entire branch at a conflict marker can preserve duplicate authority or discard required behavior.

## Decision

No conflict is resolved because a branch is "ours" or "theirs." Every conflict is resolved by identifying the target authority that owns the behavior.

For each conflict:

1. Name the target authority and enduring contract.
2. Inventory useful behavior and evidence contributed by each branch.
3. Move that behavior behind the target authority.
4. Remove alternate authority only after compatibility and behavioral parity are proven.
5. Update authority-level tests and certification evidence.

When neither branch implements the target contract, the merge introduces the smallest coherent target implementation rather than preserving a temporary dual authority.

Phase A ratifies architecture decisions without resolving source conflicts. Phase B resolves foundational contracts, followed by persistence, runtime, prompt and feature services, composition, tests, and certification.

## Consequences

- Bulk `ours` or `theirs` conflict resolution is prohibited for architectural files.
- File ownership and branch provenance do not determine behavioral ownership.
- A deletion is accepted only when its behavior is retired, migrated, or proven redundant under the target authority.
- Reviews must be able to trace each non-mechanical resolution to its target authority and ADR.
