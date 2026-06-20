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

- [x] Extend artifact discovery to classify `Current` and `Historical`.
- [x] Implement `GetCurrentHandoffAsync`.
- [x] Implement `GetCurrentDecisionsAsync`.
- [x] Implement `IArtifactRotationService`.
- [x] Add backend rotation endpoints for current handoff and current decisions.
- [x] Preserve historical file identity.
- [x] Reject unsupported rotation requests.
- [x] Reject overwrite attempts.
- [x] Refresh repository artifact inventory after rotation.

## UI Tasks

- [x] Split handoffs into:
   - [x] Current Handoff
   - [x] Historical Handoffs
- [x] Split decisions into:
   - [x] Current Decisions
   - [x] Historical Decisions
- [x] Prominently show current handoff and current decisions status.
- [x] Allow browsing historical handoffs and decisions.
- [x] Add rotate action for current handoff and current decisions only.
- [x] Refresh explorer and viewer state after rotation.

## Tests

- [x] `handoff.md` classifies as current.
- [x] `handoff.0001.md` classifies as historical.
- [x] `decisions.md` classifies as current.
- [x] `decisions.0001.md` classifies as historical.
- [x] Current handoff API returns `handoff.md` when present.
- [x] Current decisions API returns `decisions.md` when present.
- [x] First handoff rotation creates `handoff.0001.md`.
- [x] Second handoff rotation creates `handoff.0002.md`.
- [x] First decision rotation creates `decisions.0001.md`.
- [x] Second decision rotation creates `decisions.0002.md`.
- [x] Existing historical files remain unchanged.
- [x] Rotation fails when target exists.
- [x] Rotation fails for unsupported artifact families.
- [x] Refresh after rotation updates artifact inventory.

## Acceptance Criteria

- [x] Current handoff and current decisions resolve consistently.
- [x] Historical handoffs and decisions are browsable.
- [x] Handoff rotation preserves historical files.
- [x] Decision rotation preserves historical files.
- [x] Historical numbering remains stable.
- [x] Rotation tests pass.
