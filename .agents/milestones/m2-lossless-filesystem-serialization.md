# Milestone 2: Lossless Filesystem Serialization

## Objective

Make filesystem import/export a first-class capability for every migrated domain while files are still canonical.

## Implementation

- [ ] Add immutable snapshot models for every migrated domain.
- [ ] Add domain importers from current filesystem shapes.
- [ ] Add deterministic domain exporters to current filesystem shapes.
- [ ] Add workspace snapshot aggregate for all domains.
- [ ] Add validation for duplicate, malformed, missing, partial, and invalid sequence state.

## Implementation Constraints

- Serializers are snapshot/interchange components, not runtime authority.
- Import validates malformed, missing, partial, duplicate, and out-of-order exports before producing certified snapshots.
- Export overwrites only explicit export scope.
- Filesystem -> snapshot -> filesystem must be byte-stable for stable domains.
- Ambiguous `.agents/core/0*.md` and `.agents/evals/*.md` must not be migrated by inference.

## Tests

- [ ] Full `.agents` tree import to workspace snapshot.
- [ ] Snapshot export to clean `.agents` tree.
- [ ] Export/import/export stability for stable domains.
- [ ] Duplicate `DNNNN`, duplicate `NNNN`, duplicate lifecycle path, duplicate runtime prompt, duplicate family ID.
- [ ] Optional missing execution/selection manifests load empty.
- [ ] Legacy markdown-only fixtures for stores that currently support legacy migration.

## Exit Criteria

- [ ] Every migrated domain supports import and export.
- [ ] Stable domains are byte-stable after filesystem to snapshot to filesystem.
- [ ] Identity-preserving markdown histories and evidence preserve path, sequence, and body.
