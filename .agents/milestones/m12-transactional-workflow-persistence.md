# Milestone 12: Transactional Workflow Persistence and Recovery

## Objective

Coordinate multi-domain workflow writes across SQLite stores and retained filesystem artifacts so failures are deterministic and recoverable.

## Implementation

- [x] Define persistence units for covered workflows:
  - [x] roadmap transition save;
  - [x] decision recording plus state update;
  - [x] split lineage plus child artifacts plus lifecycle;
  - [x] execution preparation/provenance updates;
  - [x] journal event emission;
  - [x] loop history/evidence writes;
  - [x] completed epic archive.
- [x] Implement `IWorkflowPersistenceCoordinator` marker boundary for persistence phases.
- [x] Wire `IWorkflowPersistenceCoordinator` into covered workflow write paths.
- [x] Implement retained file staging/commit/rollback adapter for filesystem artifacts.
- [x] Add workflow transaction markers and recovery classification.
- [x] Add cross-domain integrity validator.
- [x] Ensure journal started/completed/failed records reflect actual outcomes.
- [x] Keep transactions out of agent execution time; only persistence phases are transactional.

## Implementation Constraints

- Do not claim distributed transactions across SQLite and filesystem.
- Use staging, commit ordering, markers, and recovery classification.
- Journal started records must not be lost when a workflow begins; completed/failed records must reflect outcome.
- Integrity detects orphaned evidence/history, missing logical paths, duplicates, invalid archive references, stale sync metadata, and sequence conflicts.
- Do not hold database transactions open during long-running agent or prompt work.

## Integrity Rules

Detect:

- [x] state or journal references to missing logical paths;
- [x] orphaned execution evidence;
- [x] orphaned loop histories;
- [x] duplicate identities;
- [x] invalid archive references;
- [x] stale sync metadata;
- [x] incomplete workflow transaction markers;
- [x] invalid split child references;
- [x] lifecycle rows pointing to invalid paths.

## Tests

- [x] Injected failure after decision append rolls back state/journal claims.
- [x] Injected split failure does not leave incomplete split family state.
- [x] Evidence write plus state/journal update either commits coherently or classifies retryable partial state.
- [x] Retained file staging can roll back before commit and commit writes before deletes.
- [x] Concurrent sequence allocation remains unique.
- [x] Workflow marker classifier reports retryable partial and corrupt categories.
- [x] Integrity validator reports valid, retryable partial, corrupt, unsupported, and conflict categories.

## Exit Criteria

- [x] Covered workflows use the coordinator.
- [x] Failure paths are deterministic.
- [x] No stronger cross-store atomicity guarantee is claimed than staging and recovery can enforce.
