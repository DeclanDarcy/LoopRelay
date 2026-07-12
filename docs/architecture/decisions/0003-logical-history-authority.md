# ADR-0003: Keep History Authority Logical and Use SQLite as Its Current Storage

- Status: Accepted
- Date: 2026-07-11
- Owners: History Authority, Workspace State Authority, Storage Authority

## Context

The merge contains numbered collaboration files, a feature-specific SQLite loop-history store, and a convergence evidence ledger. Selecting two stores as peers would preserve the split history authority identified by the audits.

## Decision

History Authority is the logical Evidence Ledger contract within Workspace State Authority. It owns append order, causal identity, lineage, integrity, and replay semantics independently of storage technology.

SQLite is the selected storage implementation today. Numbered `.agents` files, reports, and exports are projections or compatibility inputs. They are not alternate sources of latest history after canonical initialization or import.

History facts carry the canonical workspace, run, workflow, transition, attempt, session, and turn identities that are available at production time. Provider thread and turn identifiers, continuity lineage, and recovery-attempt correlation are evidence attached to those facts rather than a separate history model.

Storage implementations must satisfy the logical ledger contract. Replacing SQLite later must not change history semantics or give the new physical representation architectural authority.

## Consequences

- `LedgerLoopHistoryStore` behavior moves behind the logical ledger abstraction.
- Useful provider and recovery correlation from `SqliteLoopHistoryStore` must be ported before that feature-specific authority is retired.
- `FileBackedLoopHistoryStore` remains only as a projection or bounded compatibility adapter.
- Runtime fallback from canonical history to legacy files is prohibited after successful migration.
