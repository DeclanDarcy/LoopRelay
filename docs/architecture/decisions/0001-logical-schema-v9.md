# ADR-0001: Evolve the Logical Workspace Schema from v8 to v9

- Status: Accepted
- Date: 2026-07-11
- Owners: Workspace State Authority, Compatibility Authority

## Context

The architecture-convergence work establishes logical workspace schema v8. The other side of the merge introduces durable session continuity and recovery using a branch-local schema numbered v3. Branch-local version numbers cannot remain meaningful after the branches are merged.

## Decision

Schema versions describe logical model evolution, independent of branch history.

Logical schema v8 remains the predecessor. Durable session continuity, recovery plans and attempts, provider/session lineage, turn correlation, and their required history correlation are introduced as logical schema v9.

Existing v8 workspaces upgrade in place through an explicit v8-to-v9 migration. Fresh workspace creation and upgraded workspaces must produce the same v9 invariants. Older supported formats enter through Compatibility Authority and are migrated forward; they do not become alternate runtime schemas.

CanonicalWorkspace v9 is the complete union of the two provisional branch implementations. Its durable metadata records schema identity, family, logical version, and the complete physical-shape fingerprint. Inspection distinguishes `Merge4V9Partial`, `ArchitectureConvergenceV9Partial`, and `RecognizedMixedV9Partial` from `CanonicalV9Complete`; an unknown or stamped-but-incomplete v9 fails closed.

Completing a recognized provisional v9 is schema convergence, not an ordinary `9 -> 9` version migration. Convergence runs transactionally, verifies the complete target manifest before recording a dedicated convergence receipt, and stamps canonical lineage and shape only as its final operation. Historical evidence that was not observed remains null, and an existing workspace identity is preserved verbatim.

The v9 migration must preserve the v8 workspace, run, workflow, transition, attempt, session, turn, policy, prompt, product, and history identities. It may not create a second workspace identity or a parallel state authority.

## Consequences

- The branch-local schema number v3 is not retained.
- Migration tests must cover fresh v9 creation, v8-to-v9 upgrade, interruption, retry, corruption, and identity preservation.
- Later versions remain a single logical sequence: v9, v10, and onward.
- A clean consolidated baseline may be produced only by a future compatibility decision with proven import parity.
