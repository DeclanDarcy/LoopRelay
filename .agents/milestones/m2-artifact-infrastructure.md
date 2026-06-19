# M2: Artifact Infrastructure

## Goal

Make repository artifacts discoverable, loadable, editable, saveable, and viewable.

## Artifact Discovery Rules

Scan under:

```text
.agents/
```

Recognize:

```text
.agents/plan.md
.agents/operational_context.md
.agents/milestones/*.md
.agents/handoffs/*.md
.agents/decisions/*.md
```

Missing artifacts are valid. Missing directories are valid and represented as empty categories.

## Artifact Categories

- Plan
- Operational Context
- Milestones
- Handoffs
- Decisions

At M2, artifacts are markdown files with metadata. Do not derive execution meaning or current-versus-historical semantics yet beyond returning `Current` as the default version kind.

## Backend Tasks

- [ ] Implement `ArtifactType`.
- [ ] Implement `ArtifactFamily`.
- [ ] Implement `ArtifactVersionKind`.
- [ ] Implement `Artifact` with `RelativePath`.
- [ ] Implement artifact discovery by category.
- [ ] Implement artifact load by relative path.
- [ ] Implement artifact save by relative path.
- [ ] Ensure all file operations are repository-root safe.
- [ ] Add refresh operation that rediscovers artifacts from disk.
- [ ] Cache artifact inventories in memory for responsiveness.
- [ ] Rebuild cache from filesystem on refresh or restart.
- [ ] Keep refresh manual; do not add `FileSystemWatcher` or background polling.

## UI Tasks

- [ ] Add artifact explorer to repository workspace.
- [ ] Display categories:
   - [ ] Plan
   - [ ] Operational Context
   - [ ] Milestones
   - [ ] Handoffs
   - [ ] Decisions
- [ ] Display missing static artifacts explicitly.
- [ ] Display empty dynamic categories explicitly.
- [ ] Load selected artifact content.
- [ ] Render markdown content.
- [ ] Add direct edit and save flow for markdown content.
- [ ] Add refresh action.

## Tests

- [ ] Plan discovery.
- [ ] Operational context discovery.
- [ ] Milestone discovery.
- [ ] Handoff discovery.
- [ ] Decision discovery.
- [ ] Missing artifacts do not fail discovery.
- [ ] Missing directories do not fail discovery.
- [ ] Artifact content loads correctly.
- [ ] Artifact save persists to disk.
- [ ] Refresh discovers newly added files.
- [ ] Relative path traversal attempts are rejected.

## Acceptance Criteria

- [ ] Repository artifacts are automatically discovered.
- [ ] Artifact inventory is displayed in the repository workspace.
- [ ] Artifact markdown content is viewable.
- [ ] Artifact markdown content is editable and saveable.
- [ ] Saved changes persist after reload.
- [ ] Refresh updates artifact inventory without application restart.
