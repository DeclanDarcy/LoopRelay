# Milestone 12: Transactional Workflow Persistence and Recovery

## Objective

Coordinate multi-domain workflow writes across SQLite stores and retained filesystem artifacts so failures are deterministic and recoverable.

## Implementation

- [ ] Define persistence units for covered workflows:
  - [ ] roadmap transition save;
  - [ ] decision recording plus state update;
  - [ ] split lineage plus child artifacts plus lifecycle;
  - [ ] execution preparation/provenance updates;
  - [ ] journal event emission;
  - [ ] loop history/evidence writes;
  - [ ] completed epic archive.
- [ ] Implement `IWorkflowPersistenceCoordinator` around SQLite transactions.
- [ ] Implement retained file staging/commit/rollback adapter for filesystem artifacts.
- [ ] Add workflow transaction markers and recovery classification.
- [ ] Add cross-domain integrity validator.
- [ ] Ensure journal started/completed/failed records reflect actual outcomes.
- [ ] Keep transactions out of agent execution time; only persistence phases are transactional.

## Implementation Constraints

- Do not claim distributed transactions across SQLite and filesystem.
- Use staging, commit ordering, markers, and recovery classification.
- Journal started records must not be lost when a workflow begins; completed/failed records must reflect outcome.
- Integrity detects orphaned evidence/history, missing logical paths, duplicates, invalid archive references, stale sync metadata, and sequence conflicts.
- Do not hold database transactions open during long-running agent or prompt work.

## Integrity Rules

Detect:

- [ ] state or journal references to missing logical paths;
- [ ] orphaned execution evidence;
- [ ] orphaned loop histories;
- [ ] duplicate identities;
- [ ] invalid archive references;
- [ ] stale sync metadata;
- [ ] incomplete workflow transaction markers;
- [ ] invalid split child references;
- [ ] lifecycle rows pointing to invalid paths.

## Tests

- [ ] Injected failure after decision append rolls back state/journal claims.
- [ ] Injected split failure does not leave incomplete split family state.
- [ ] Evidence write plus state/journal update either commits coherently or classifies retryable partial state.
- [ ] Retained file finalization failure classifies retryable versus corrupt based on commit point.
- [ ] Concurrent sequence allocation remains unique.
- [ ] Integrity validator reports valid, retryable partial, corrupt, unsupported, and conflict categories.

## Exit Criteria

- [ ] Covered workflows use the coordinator.
- [ ] Failure paths are deterministic.
- [ ] No stronger cross-store atomicity guarantee is claimed than staging and recovery can enforce.
