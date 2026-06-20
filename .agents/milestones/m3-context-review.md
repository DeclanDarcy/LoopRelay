# Milestone M3 - Operational Context Review

## Objective

Introduce human review for proposed project understanding without promoting it.

## Backend Changes

- [ ] Add `IOperationalContextReviewService`.
- [ ] Add `OperationalContextReview` with:
  - [ ] `ProposalId`.
  - [ ] `ReviewState`.
  - [ ] `BaselineCurrentContextHash`.
  - [ ] `ReviewedContentHash`.
  - [ ] `ReviewedAt`.
  - [ ] `ReviewNote`.
  - [ ] `StaleReason`.
- [ ] Supported review states:
  - [ ] `PendingReview`.
  - [ ] `Edited`.
  - [ ] `Accepted`.
  - [ ] `Rejected`.
  - [ ] `Stale`.
- [ ] Add edit operation:
  - [ ] Stores reviewer content in `edited.md`.
  - [ ] Parses reviewer content into `OperationalContextDocument`.
  - [ ] Recomputes content hash.
  - [ ] Recomputes coarse semantic changes against current context.
  - [ ] Keeps proposal reviewable.
- [ ] Add accept operation:
  - [ ] Requires proposal exists.
  - [ ] Requires proposal is latest or otherwise not superseded.
  - [ ] Requires current operational-context hash matches proposal baseline.
  - [ ] Stores accepted content hash.
  - [ ] Does not write `.agents/operational_context.md`.
- [ ] Add reject operation:
  - [ ] Stores rejection state and optional review note.
  - [ ] Leaves proposal content for audit.
- [ ] Add stale protection:
  - [ ] Blocks accept when current context changed after proposal generation.
  - [ ] Blocks accept when proposal was superseded by regeneration.

## API Changes

- [ ] `PUT /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/content`
- [ ] `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/accept`
- [ ] `POST /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}/reject`

## UI Changes

- [ ] Display current understanding and proposed understanding side by side.
- [ ] Display semantic understanding changes:
  - [ ] Sections added, removed, or changed.
  - [ ] Items added, removed, or changed.
  - [ ] Constraints added or removed.
  - [ ] Questions added or removed.
  - [ ] Risks added or removed.
  - [ ] Decision items added or removed when available.
- [ ] Provide edit, accept, and reject controls.
- [ ] Preserve reviewer edits in a Markdown editor.
- [ ] Make stale proposals visibly blocked.
- [ ] Promotion controls remain absent.

## Tests

Add backend tests:

- [ ] Pending proposal is reviewable.
- [ ] Current and proposed content load together.
- [ ] Current and proposed content parse into `OperationalContextDocument`.
- [ ] Editing persists and changes accepted content candidate.
- [ ] Accept records review state without changing current context.
- [ ] Reject records review state and blocks promotion.
- [ ] Accept fails for missing, superseded, or stale proposals.
- [ ] Review state survives service recreation.

## Certification

Review is certified when a user can inspect current and proposed understanding, understand semantic changes, edit proposed content, accept or reject the proposal, and still leave `.agents/operational_context.md` unchanged.
