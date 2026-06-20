# Milestone M7 - Understanding Workspace

## Objective

Expose current project understanding as a first-class section inside the existing repository workspace.

## Backend Projection Changes

- [ ] Add `OperationalContextProjection` to `RepositoryWorkspaceProjection`.
- [ ] Add dashboard continuity summary fields.
- [ ] Projection fields:
  - [ ] Current context exists.
  - [ ] Current relative path.
  - [ ] Revision count.
  - [ ] Current revision number.
  - [ ] Last updated timestamp.
  - [ ] Last promotion timestamp.
  - [ ] Current understanding summary.
  - [ ] Architecture items.
  - [ ] Authority boundaries.
  - [ ] Constraints.
  - [ ] Stable decisions.
  - [ ] Decision rationale.
  - [ ] Open questions.
  - [ ] Active risks.
  - [ ] Recent semantic changes.
  - [ ] Pending proposal summary.
  - [ ] Latest review state.
  - [ ] Continuity warnings.
- [ ] All values originate from backend parsing and proposal metadata.

## UI Changes

- [ ] Add an `OperationalContextSurface` inside repository details.
- [ ] Dashboard shows:
  - [ ] Operational context present or missing.
  - [ ] Revision count.
  - [ ] Last updated.
  - [ ] Open question count.
  - [ ] Active risk count.
- [ ] Workspace shows:
  - [ ] Current understanding summary.
  - [ ] Stable decisions.
  - [ ] Open questions.
  - [ ] Active risks.
  - [ ] Recent understanding changes.
  - [ ] Whether operational context is included in execution context preview.
- [ ] Keep artifact explorer available for full Markdown editing.
- [ ] Avoid building a full historical revision browser.
- [ ] Avoid computing understanding state client-side.

## Tests

Add backend tests:

- [ ] Projection parses current operational context into expected sections.
- [ ] Dashboard exposes revision count and counts for questions/risks.
- [ ] Workspace projection includes pending proposal and review state.
- [ ] Missing operational context produces explicit missing state, not failure.

Add UI build validation:

- [ ] TypeScript build passes.
- [ ] Understanding components handle missing, empty, present, pending proposal, accepted proposal, and stale proposal states.

## Certification

Understanding workspace is certified when a user can enter a repository workspace and answer:

- [ ] What do we currently believe?
- [ ] Why do we believe it?
- [ ] What remains unresolved?
- [ ] What changed recently?
- [ ] What should guide future execution?

without opening historical handoff or decision archives.
