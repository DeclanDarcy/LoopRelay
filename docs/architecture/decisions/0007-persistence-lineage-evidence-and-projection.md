# ADR-0007: Make Schema Lineage, Evidence Sets, and Persistence Projections Explicit

- Status: Accepted
- Date: 2026-07-11
- Owners: Persistence Authority, Compatibility Authority, History Authority, Effect Coordinator

## Context

Numeric schema versions were independently assigned on divergent branches, so the same number can describe incompatible physical models. History correlation also needs to evolve from nullable attachments into identifiable evidence, and filesystem materialization cannot be atomic with the workspace ledger.

## Decision

Every workspace database identifies its schema with three values: schema identity, schema family, and logical version. Version is interpreted only within a recognized family. CanonicalWorkspace v8 migrates in place to v9. LegacyContinuity v3 enters through an explicit shadow import and is never silently treated as canonical v3.

Workspace identity is immutable. Migration preserves the existing opaque value and records identity format, migration source, and import time as metadata.

Every history fact owns one stable evidence set. Evidence items have their own stable identities, kinds, schema versions, and causal correlations. Provider, continuity, recovery, repository, and effect evidence remain distinct evidence kinds and may evolve into an evidence graph without changing history-fact identity.

Compatibility import is a journaled canonical operation with Planned, Started, Verified, Completed, and Failed evidence. Detection is read-only; import mutation is explicit.

Filesystem materialization is a Projection Effect. The history fact, evidence set, and projection effect are committed before filesystem work starts. Projection failure is durable and retryable.

Persistence Authority projects ledger state into `CanonicalPersistenceReadModel`. Repository Observer consumes that model and independently observes repository, Git, workspace, and filesystem evidence; it does not query or interpret persistence tables.

## Consequences

- Schema version alone is never sufficient for compatibility decisions.
- Runtime selection between file and SQLite history authorities is prohibited.
- Legacy numbered files are read only by Compatibility Authority and imported once.
- Read models depend on persistence projections rather than raw table layouts.
- Every durable fact precedes its projection, never the reverse.
