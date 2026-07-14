# ADR 0015: Strict storage operation boundaries

Status: Accepted  
Date: 2026-07-12

## Decision

`status` and `storage verify` are strictly read-only. They open an existing SQLite authority through the read-only connection factory, inventory and hash the persistence tree, and report `Healthy`, `ActionRequired`, `Unsupported`, or `Corrupt`. They never initialize, migrate, repair, synchronize, or create SQLite side files.

Only `storage migrate` may execute a supported schema upgrade. `storage init` requires an absent authority and uses a durable staging plan plus an absence-guarded typed promotion effect. Compatibility import is exposed separately as `import detect|preview|execute|verify` and is owned by the M12 Import Gateway; it is not a storage subcommand or an ordinary-run fallback. Export uses the versioned canonical codec and M8 effects; sync is bounded to rebuildable projections and already-journaled effects.

## Consequences

Startup fails closed when storage is missing or requires migration. Command names describe the work actually performed. Application and observer code do not issue SQL or invoke schema migration; those details remain behind Storage Authority and Core persistence primitives.
