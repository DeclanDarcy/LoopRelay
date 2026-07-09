# Milestone 3: Logical Artifact Identity and Freshness Resolution

## Objective

Resolve content and hashes by logical repo-relative path independent of physical storage.

## Implementation

- [x] Add `LogicalArtifactDescriptor`, `LogicalArtifactContent`, and `LogicalArtifactResolutionResult`.
- [x] Add resolver providers for retained filesystem files and file-backed migrated domains.
- [x] Add canonical hash service using retained file bytes or canonical export-equivalent migrated content.
- [ ] Update freshness and prompt consumers to use logical resolution for any path that can become SQLite-backed.
- [x] Keep missing-path behavior domain-specific.

## Implementation Constraints

- Resolver classifies and dispatches; domain stores parse domain identities.
- Hashing migrated records uses canonical export-equivalent content unless explicitly overridden.
- Freshness results in file-backed mode must match current behavior.
- Missing migrated artifacts fail explicitly as stale, invalid, or blocked, never as silent file-read nulls.

## Code Impact

- [x] Replace direct `RoadmapArtifacts.ReadAsync(path)` hashing in `TransitionInputAccumulator`.
- [x] Replace `ExecutionPreparationProvenanceService.CaptureDecisionLedgerInputAsync` file hash of `decision-ledger.json` with canonical decision ledger hash.
- [x] Update completion evaluation context construction to resolve execution evidence through the resolver.
- [ ] Update unblock evidence hashing for execution evidence and migrated histories.

## Tests

- [x] Retained spec, active epic, plan, operational context, live decision, and live handoff resolve from disk.
- [x] Historical decision/handoff/delta paths resolve after import.
- [x] Execution evidence paths resolve from file-backed evidence store.
- [ ] Hash drift in retained and migrated domains reports stale.
- [ ] Missing migrated evidence reports stale, invalid, or blocked according to consumer behavior.

## Exit Criteria

- [ ] File-backed freshness results match current tests.
- [ ] All path references that may later point to SQLite-backed records resolve through the logical resolver.
