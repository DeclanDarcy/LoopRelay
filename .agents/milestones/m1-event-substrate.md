# Milestone 1: Reasoning Event Substrate

Goal: implement durable events, threads, relationships, references, provenance, identity, persistence, markdown projection, endpoints, and basic UI.

## Backend Work

- [x] Add `CommandCenter.Reasoning` project and solution entry.
- [x] Add primitives and models for events, threads, relationships, references, provenance, and event classification.
- [x] Implement event identity, thread identity, and relationship identity as repository-scoped sequence IDs.
- [x] Implement `ReasoningArtifactDocument<T>` with schema version, repository ID, created/updated timestamps, and payload.
- [x] Implement `ReasoningJson.Options` with deterministic JSON and string enums.
- [x] Implement `ReasoningArtifactPaths` with safe relative paths and ID validation.
- [x] Implement `IReasoningRepository` and `FileSystemReasoningRepository`.
- [x] Implement `IReasoningArtifactProjectionService` and markdown projections for events, threads, and relationships.
- [x] Implement `IReasoningEventService`, `IReasoningThreadService`, and `IReasoningRelationshipService`.
- [x] Enforce event immutability.
- [x] Enforce event provenance.
- [x] Validate supported reference kinds.
- [x] Validate relationship source and target references.
- [x] Add `AddReasoning()` service registration.
- [x] Map event, thread, and relationship endpoints.

## UI Work

- [ ] Add Reasoning tab shell entry.
- [ ] Add reasoning DTOs, API wrappers, and hooks for events, threads, and relationships.
- [ ] Add `ReasoningEventFeed`, `ReasoningThreadPanel`, and `ReasoningTracePanel` components.
- [ ] Add command palette navigation targets for the Reasoning tab, event feed, and thread view.

## Tests

- [x] ID allocation scans existing reasoning artifacts.
- [x] Event persistence round trips through repository files.
- [x] Event immutability is enforced.
- [x] Events require provenance.
- [x] Repository ownership is enforced.
- [x] Unsafe IDs and paths are rejected.
- [x] Unsupported schema versions are rejected.
- [x] Thread persistence and event grouping round trip.
- [x] Relationship persistence and validation round trip.
- [x] Markdown projections are deterministic.
- [x] Endpoints return expected status codes.
- [x] Creating hypothesis, alternative, contradiction, or direction family events does not create corresponding entity directories.
- [ ] Event-family sequences produce derived display status only; they do not authorize lifecycle mutations.
- [ ] UI characterization covers event feed, empty states, provenance display, and thread selection.

## Exit Criteria

- [x] Event substrate is operational.
- [x] Thread grouping is operational.
- [x] Relationship persistence is operational.
- [x] No specialized entity persistence exists.
