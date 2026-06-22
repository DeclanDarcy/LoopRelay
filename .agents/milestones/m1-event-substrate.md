# Milestone 1: Reasoning Event Substrate

Goal: implement durable events, threads, relationships, references, provenance, identity, persistence, markdown projection, endpoints, and basic UI.

## Backend Work

- [ ] Add `CommandCenter.Reasoning` project and solution entry.
- [ ] Add primitives and models for events, threads, relationships, references, provenance, and event classification.
- [ ] Implement event identity, thread identity, and relationship identity as repository-scoped sequence IDs.
- [ ] Implement `ReasoningArtifactDocument<T>` with schema version, repository ID, created/updated timestamps, and payload.
- [ ] Implement `ReasoningJson.Options` with deterministic JSON and string enums.
- [ ] Implement `ReasoningArtifactPaths` with safe relative paths and ID validation.
- [ ] Implement `IReasoningRepository` and `FileSystemReasoningRepository`.
- [ ] Implement `IReasoningArtifactProjectionService` and markdown projections for events, threads, and relationships.
- [ ] Implement `IReasoningEventService`, `IReasoningThreadService`, and `IReasoningRelationshipService`.
- [ ] Enforce event immutability.
- [ ] Enforce event provenance.
- [ ] Validate supported reference kinds.
- [ ] Validate relationship source and target references.
- [ ] Add `AddReasoning()` service registration.
- [ ] Map event, thread, and relationship endpoints.

## UI Work

- [ ] Add Reasoning tab shell entry.
- [ ] Add reasoning DTOs, API wrappers, and hooks for events, threads, and relationships.
- [ ] Add `ReasoningEventFeed`, `ReasoningThreadPanel`, and `ReasoningTracePanel` components.
- [ ] Add command palette navigation targets for the Reasoning tab, event feed, and thread view.

## Tests

- [ ] ID allocation scans existing reasoning artifacts.
- [ ] Event persistence round trips through repository files.
- [ ] Event immutability is enforced.
- [ ] Events require provenance.
- [ ] Repository ownership is enforced.
- [ ] Unsafe IDs and paths are rejected.
- [ ] Unsupported schema versions are rejected.
- [ ] Thread persistence and event grouping round trip.
- [ ] Relationship persistence and validation round trip.
- [ ] Markdown projections are deterministic.
- [ ] Endpoints return expected status codes.
- [ ] Creating hypothesis, alternative, contradiction, or direction family events does not create corresponding entity directories.
- [ ] Event-family sequences produce derived display status only; they do not authorize lifecycle mutations.
- [ ] UI characterization covers event feed, empty states, provenance display, and thread selection.

## Exit Criteria

- [ ] Event substrate is operational.
- [ ] Thread grouping is operational.
- [ ] Relationship persistence is operational.
- [ ] No specialized entity persistence exists.
