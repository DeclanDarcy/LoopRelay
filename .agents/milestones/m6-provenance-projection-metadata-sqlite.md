# Milestone 6: Provenance and Projection Metadata Run from SQLite

## Objective

Make execution preparation provenance, selection provenance, and projection manifest metadata SQLite-canonical while preserving freshness.

## Implementation

- [x] Implement SQLite-backed execution preparation manifest store.
- [x] Implement SQLite-backed selection provenance manifest store.
- [x] Implement SQLite-backed projection manifest store keyed by runtime prompt name.
- [x] Consolidate duplicated projection manifest behavior so Roadmap and Projections use one canonical contract or conformance suite.
- [x] Keep projection body markdown files on disk.
- [x] Preserve empty-on-malformed compatibility only at import/export boundaries for execution and selection manifests.

## Implementation Constraints

- Migrate execution preparation provenance, selection provenance, and projection manifest only.
- Projection bodies remain filesystem-backed.
- Metadata writes occur after referenced artifacts exist or are explicitly recorded as missing/stale.
- Malformed exported provenance loads empty only at import/compatibility boundary.
- Roadmap and projections projection-manifest behavior must be consolidated or conformance-tested.

## Code Impact

- [x] `ExecutionPreparationProvenanceService` reads/writes SQLite metadata and hashes retained inputs through the logical hasher.
- [x] `SelectionProvenanceService` evaluates drift from SQLite metadata and logical input snapshots.
- [x] `ProjectionCache` and `ProjectContextProjectionService` observe the same manifest semantics.

## Tests

- [x] Freshness parity for retained-file drift, decision ledger drift, retired epic drift, projection body drift, and missing projection bodies.
- [x] Malformed exported execution/selection manifests load empty during compatibility import.
- [x] Projection manifest upsert by runtime prompt replaces existing metadata.
- [x] Roadmap and Projections project tests pass against the same behavior.
- [x] Metadata export/import into clean database preserves logical equality.

## Exit Criteria

- [x] Provenance and projection metadata are SQLite-canonical.
- [x] Projection body content remains filesystem-backed.
- [x] Freshness decisions match file-backed behavior.
