# Milestone 6: Provenance and Projection Metadata Run from SQLite

## Objective

Make execution preparation provenance, selection provenance, and projection manifest metadata SQLite-canonical while preserving freshness.

## Implementation

- [ ] Implement SQLite-backed execution preparation manifest store.
- [ ] Implement SQLite-backed selection provenance manifest store.
- [ ] Implement SQLite-backed projection manifest store keyed by runtime prompt name.
- [ ] Consolidate duplicated projection manifest behavior so Roadmap and Projections use one canonical contract or conformance suite.
- [ ] Keep projection body markdown files on disk.
- [ ] Preserve empty-on-malformed compatibility only at import/export boundaries for execution and selection manifests.

## Implementation Constraints

- Migrate execution preparation provenance, selection provenance, and projection manifest only.
- Projection bodies remain filesystem-backed.
- Metadata writes occur after referenced artifacts exist or are explicitly recorded as missing/stale.
- Malformed exported provenance loads empty only at import/compatibility boundary.
- Roadmap and projections projection-manifest behavior must be consolidated or conformance-tested.

## Code Impact

- [ ] `ExecutionPreparationProvenanceService` reads/writes SQLite metadata and hashes retained inputs through the logical hasher.
- [ ] `SelectionProvenanceService` evaluates drift from SQLite metadata and logical input snapshots.
- [ ] `ProjectionCache` and `ProjectContextProjectionService` observe the same manifest semantics.

## Tests

- [ ] Freshness parity for retained-file drift, decision ledger drift, retired epic drift, projection body drift, and missing projection bodies.
- [ ] Malformed exported execution/selection manifests load empty during compatibility import.
- [ ] Projection manifest upsert by runtime prompt replaces existing metadata.
- [ ] Roadmap and Projections project tests pass against the same behavior.
- [ ] Metadata export/import into clean database preserves logical equality.

## Exit Criteria

- [ ] Provenance and projection metadata are SQLite-canonical.
- [ ] Projection body content remains filesystem-backed.
- [ ] Freshness decisions match file-backed behavior.
