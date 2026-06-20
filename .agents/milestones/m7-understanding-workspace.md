# Milestone M7 - Understanding Workspace

## Objective

Expose current project understanding as a first-class section inside the existing repository workspace.

## Backend Projection Changes

- [x] Add `OperationalContextProjection` to `RepositoryWorkspaceProjection`.
- [x] Add dashboard continuity summary fields.
- [x] Projection fields:
  - [x] Current context exists.
  - [x] Current relative path.
  - [x] Revision count.
  - [x] Current revision number.
  - [x] Last updated timestamp.
  - [x] Last promotion timestamp.
  - [x] Current understanding summary.
  - [x] Architecture items.
  - [x] Authority boundaries.
  - [x] Constraints.
  - [x] Stable decisions.
  - [x] Decision rationale.
  - [x] Open questions.
  - [x] Active risks.
  - [x] Recent semantic changes.
  - [x] Pending proposal summary.
  - [x] Latest review state.
  - [x] Continuity warnings.
- [x] All values originate from backend parsing and proposal metadata.

## UI Changes

- [x] Add an `OperationalContextSurface` inside repository details.
- [x] Dashboard shows:
  - [x] Operational context present or missing.
  - [x] Revision count.
  - [ ] Last updated.
  - [x] Open question count.
  - [x] Active risk count.
- [x] Workspace shows:
  - [x] Current understanding summary.
  - [x] Stable decisions.
  - [x] Open questions.
  - [x] Active risks.
  - [x] Recent understanding changes.
  - [ ] Whether operational context is included in execution context preview.
- [x] Keep artifact explorer available for full Markdown editing.
- [x] Avoid building a full historical revision browser.
- [x] Avoid computing understanding state client-side.

## Tests

Add backend tests:

- [x] Projection parses current operational context into expected sections.
- [x] Dashboard exposes revision count and counts for questions/risks.
- [x] Workspace projection includes pending proposal and review state.
- [x] Missing operational context produces explicit missing state, not failure.

Add UI build validation:

- [x] TypeScript build passes.
- [ ] Understanding components handle missing, empty, present, pending proposal, accepted proposal, and stale proposal states.

## Certification

Understanding workspace is certified when a user can enter a repository workspace and answer:

- [ ] What do we currently believe?
- [ ] Why do we believe it?
- [ ] What remains unresolved?
- [ ] What changed recently?
- [ ] What should guide future execution?

without opening historical handoff or decision archives.
