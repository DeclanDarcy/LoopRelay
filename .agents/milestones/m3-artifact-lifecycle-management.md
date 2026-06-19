# M3: Artifact Lifecycle Management

## Goal

Add current and historical artifact classification plus safe rotation for handoff and decision artifacts.

## Static Artifacts

Static artifacts have only a current version:

```text
.agents/plan.md
.agents/operational_context.md
.agents/milestones/*.md
```

## Rotating Artifacts

Handoff artifacts:

```text
.agents/handoffs/handoff.md
.agents/handoffs/handoff.0001.md
.agents/handoffs/handoff.0002.md
```

Decision artifacts:

```text
.agents/decisions/decisions.md
.agents/decisions/decisions.0001.md
.agents/decisions/decisions.0002.md
```

## Current Artifact Rules

Current handoff:

```text
.agents/handoffs/handoff.md
```

Current decisions:

```text
.agents/decisions/decisions.md
```

Historical handoffs match:

```text
handoff.NNNN.md
```

Historical decisions match:

```text
decisions.NNNN.md
```

where `NNNN` is a four-digit positive sequence number.

## Rotation Rules

Rotation is supported only for:

```text
ArtifactFamily.Handoff
ArtifactFamily.Decision
```

Rotation algorithm:

1. Resolve the current artifact for the requested artifact family.
2. Confirm the current artifact exists.
3. List existing historical files for the artifact family.
4. Parse valid four-digit sequence numbers.
5. Select `highest + 1`, or `0001` when no historical file exists.
6. Build the target historical path.
7. Fail if the target already exists.
8. Archive current artifact content to the target historical file.
9. Leave the current artifact present and still classified as current.
10. Refresh artifact inventory and classification.

Historical files must never be renumbered.

Epic 1 rotation does not replace, truncate, clear, or rewrite the current artifact after archiving it.

## Backend Tasks

- [ ] Extend artifact discovery to classify `Current` and `Historical`.
- [ ] Implement `GetCurrentHandoffAsync`.
- [ ] Implement `GetCurrentDecisionsAsync`.
- [ ] Implement `IArtifactRotationService`.
- [ ] Add backend rotation endpoints for current handoff and current decisions.
- [ ] Preserve historical file identity.
- [ ] Reject unsupported rotation requests.
- [ ] Reject overwrite attempts.
- [ ] Refresh repository artifact inventory after rotation.

## UI Tasks

- [ ] Split handoffs into:
   - [ ] Current Handoff
   - [ ] Historical Handoffs
- [ ] Split decisions into:
   - [ ] Current Decisions
   - [ ] Historical Decisions
- [ ] Prominently show current handoff and current decisions status.
- [ ] Allow browsing historical handoffs and decisions.
- [ ] Add rotate action for current handoff and current decisions only.
- [ ] Refresh explorer and viewer state after rotation.

## Tests

- [ ] `handoff.md` classifies as current.
- [ ] `handoff.0001.md` classifies as historical.
- [ ] `decisions.md` classifies as current.
- [ ] `decisions.0001.md` classifies as historical.
- [ ] Current handoff API returns `handoff.md` when present.
- [ ] Current decisions API returns `decisions.md` when present.
- [ ] First handoff rotation creates `handoff.0001.md`.
- [ ] Second handoff rotation creates `handoff.0002.md`.
- [ ] First decision rotation creates `decisions.0001.md`.
- [ ] Second decision rotation creates `decisions.0002.md`.
- [ ] Existing historical files remain unchanged.
- [ ] Rotation fails when target exists.
- [ ] Rotation fails for unsupported artifact families.
- [ ] Refresh after rotation updates artifact inventory.

## Acceptance Criteria

- [ ] Current handoff and current decisions resolve consistently.
- [ ] Historical handoffs and decisions are browsable.
- [ ] Handoff rotation preserves historical files.
- [ ] Decision rotation preserves historical files.
- [ ] Historical numbering remains stable.
- [ ] Rotation tests pass.
