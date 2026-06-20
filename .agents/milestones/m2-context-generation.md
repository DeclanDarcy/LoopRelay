# Milestone M2 - Operational Context Generation

## Objective

Generate and persist reviewable proposed project understanding without modifying `.agents/operational_context.md`.

## Backend Changes

- [ ] Implement `OperationalContextDocument`, `OperationalContextItem`, `OperationalContextSection`, and `OperationalContextItemKind` according to `docs/operational-context-schema.md`.
- [ ] Implement `MarkdownOperationalContextParser` and renderer before proposal generation.
- [ ] Add `IOperationalContextGenerationService`.
- [ ] Add `IOperationalContextProposalStore`.
- [ ] Add repository-owned proposal persistence under:

```text
.agents/operational_context/proposals/<proposal-id>/
```

- [ ] Introduce `OperationalContextProposal` with:
  - [ ] `ProposalId`.
  - [ ] `RepositoryId`.
  - [ ] `GeneratedAt`.
  - [ ] `Status`.
  - [ ] `InputFingerprints`.
  - [ ] `BaselineCurrentContextHash`.
  - [ ] `GeneratedContentHash`.
  - [ ] `GeneratedContentRelativePath`.
  - [ ] `EditedContentRelativePath`.
  - [ ] `SemanticChanges`.
  - [ ] `CompressionSummary`.
- [ ] Introduce `OperationalContextInputSet` containing:
  - [ ] Current operational context when present.
  - [ ] Current handoff when present.
  - [ ] Current decisions when present.
  - [ ] Bounded execution session summaries from `IExecutionSessionStore`.
  - [ ] Planning state and milestone inventory.
  - [ ] Repository identity and availability.
- [ ] Do not consume Git commit history, raw execution streams, raw provider output, or full conversation logs.
- [ ] Implement deterministic generation as a backend service that:
  - [ ] Parses existing context into `OperationalContextDocument`.
  - [ ] Generates a new `OperationalContextDocument`.
  - [ ] Renders the proposed document to Markdown for persistence.
  - [ ] Preserves existing stable understanding.
  - [ ] Incorporates latest handoff and decision signal.
  - [ ] Uses execution history only as bounded metadata.
  - [ ] Produces the stable operational-context Markdown structure.
  - [ ] Compresses completed work into current conclusions.
  - [ ] Excludes chronological session replay.
- [ ] Generate a coarse semantic change summary from current document to proposed document.
- [ ] Persist generated proposal content and metadata before returning.
- [ ] Regeneration creates a new proposal and marks previous pending proposal as stale or superseded.

## API Changes

- [ ] `POST /api/repositories/{repositoryId}/operational-context/generate` creates a proposal.
- [ ] `GET /api/repositories/{repositoryId}/operational-context/proposals` lists proposals.
- [ ] `GET /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}` loads proposal detail.

## Projection Changes

- [ ] Extend `RepositoryWorkspaceProjection` with a proposal summary:
  - [ ] Pending proposal exists.
  - [ ] Latest proposal id.
  - [ ] Generated timestamp.
  - [ ] Proposal status.
  - [ ] Source input count.
  - [ ] Content byte and character count.

## UI Changes

- [ ] Add a manual `Generate Proposal` action.
- [ ] Show proposal existence, generated timestamp, status, and semantic changes.
- [ ] Do not add accept, reject, edit, or promote actions yet.

## Tests

Add backend tests:

- [ ] Generation succeeds without existing operational context.
- [ ] Generation uses existing operational context when present.
- [ ] Generation succeeds when handoff, decisions, or execution history are missing.
- [ ] Proposal persists across service recreation.
- [ ] Proposal content contains understanding sections rather than chronological replay.
- [ ] Parser maps canonical Markdown sections into `OperationalContextDocument`.
- [ ] Parser preserves unknown hand-written sections instead of discarding them.
- [ ] Renderer round-trips the document model into stable Markdown.
- [ ] Coarse semantic changes report section and item changes without deep interpretation.
- [ ] Regeneration creates a new proposal and supersedes stale pending review state.
- [ ] Workspace projection surfaces latest proposal summary.

## Certification

Generation is certified when Command Center can read current understanding and available repository artifacts, create a proposed understanding artifact, persist it, surface it in the workspace, and leave `.agents/operational_context.md` unchanged.
