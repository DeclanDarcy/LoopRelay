# Milestone M3 - Operational Context Review

## Objective

Introduce human review for proposed project understanding without promoting it.

## Backend Changes

- [x] Add `IOperationalContextReviewService`.
- [x] Add `OperationalContextReview` with:
  - [x] `ProposalId`.
  - [x] `ReviewState`.
  - [x] `BaselineCurrentContextHash`.
  - [x] `ReviewedContentHash`.
  - [x] `ReviewedAt`.
  - [x] `ReviewNote`.
  - [x] `StaleReason`.
- [x] Supported review states:
  - [x] `PendingReview`.
  - [x] `Edited`.
  - [x] `Accepted`.
  - [x] `Rejected`.
  - [x] `Stale`.
- [x] Add edit operation:
  - [x] Stores reviewer content in `edited.md`.
  - [x] Parses reviewer content into `OperationalContextDocument`.
  - [x] Recomputes content hash.
  - [x] Recomputes coarse semantic changes against current context.
  - [x] Keeps proposal reviewable.
- [x] Add accept operation:
  - [x] Requires proposal exists.
  - [x] Requires proposal is latest or otherwise not superseded.
  - [x] Requires current operational-context hash matches proposal baseline.
  - [x] Stores accepted content hash.
  - [x] Does not write `.agents/operational_context.md`.
- [x] Add reject operation:
  - [x] Stores rejection state and optional review note.
  - [x] Leaves proposal content for audit.
- [x] Add stale protection:
  - [x] Blocks accept when current context changed after proposal generation.
  - [x] Blocks accept when proposal was superseded by regeneration.

## API Changes

- [x] `PUT /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/content`
- [x] `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/accept`
- [x] `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/reject`

## UI Changes

- [x] Display current understanding and proposed understanding side by side.
- [x] Display semantic understanding changes:
  - [x] Sections added, removed, or changed.
  - [x] Items added, removed, or changed.
  - [x] Constraints added or removed.
  - [x] Questions added or removed.
  - [x] Risks added or removed.
  - [x] Decision items added or removed when available.
- [x] Provide edit, accept, and reject controls.
- [x] Preserve reviewer edits in a Markdown editor.
- [x] Make stale proposals visibly blocked.
- [x] Promotion controls remain absent.

## Tests

Add backend tests:

- [x] Pending proposal is reviewable.
- [x] Current and proposed content load together.
- [x] Current and proposed content parse into `OperationalContextDocument`.
- [x] Editing persists and changes accepted content candidate.
- [x] Accept records review state without changing current context.
- [x] Reject records review state and blocks promotion.
- [x] Accept fails for missing, superseded, or stale proposals.
- [x] Review state survives service recreation.

## Certification

Review is certified when a user can inspect current and proposed understanding, understand semantic changes, edit proposed content, accept or reject the proposal, and still leave `.agents/operational_context.md` unchanged.
