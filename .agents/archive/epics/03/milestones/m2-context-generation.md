# Milestone M2 - Operational Context Generation

## Objective

Generate and persist reviewable proposed project understanding without modifying `.agents/operational_context.md`.

## Backend Changes

- [x] Implement `OperationalContextDocument`, `OperationalContextItem`, `OperationalContextSection`, and `OperationalContextItemKind` according to `docs/operational-context-schema.md`.
- [x] Implement `MarkdownOperationalContextParser` and renderer before proposal generation.
- [x] Add `IOperationalContextGenerationService`.
- [x] Add `IOperationalContextProposalStore`.
- [x] Add repository-owned proposal persistence under:

```text
.agents/operational_context/proposals/<proposal-id>/
```

- [x] Introduce `OperationalContextProposal` with:
  - [x] `ProposalId`.
  - [x] `RepositoryId`.
  - [x] `GeneratedAt`.
  - [x] `Status`.
  - [x] `InputFingerprints`.
  - [x] `BaselineCurrentContextHash`.
  - [x] `GeneratedContentHash`.
  - [x] `GeneratedContentRelativePath`.
  - [x] `EditedContentRelativePath`.
  - [x] `SemanticChanges`.
  - [x] `CompressionSummary`.
- [x] Introduce `OperationalContextInputSet` containing:
  - [x] Current operational context when present.
  - [x] Current handoff when present.
  - [x] Current decisions when present.
  - [x] Bounded execution session summaries from `IExecutionSessionStore`.
  - [x] Planning state and milestone inventory.
  - [x] Repository identity and availability.
- [x] Do not consume Git commit history, raw execution streams, raw provider output, or full conversation logs.
- [x] Implement deterministic generation as a backend service that:
  - [x] Parses existing context into `OperationalContextDocument`.
  - [x] Generates a new `OperationalContextDocument`.
  - [x] Renders the proposed document to Markdown for persistence.
  - [x] Preserves existing stable understanding.
  - [x] Incorporates latest handoff and decision signal.
  - [x] Uses execution history only as bounded metadata.
  - [x] Produces the stable operational-context Markdown structure.
  - [x] Compresses completed work into current conclusions.
  - [x] Excludes chronological session replay.
- [x] Generate a coarse semantic change summary from current document to proposed document.
- [x] Persist generated proposal content and metadata before returning.
- [x] Regeneration creates a new proposal and marks previous pending proposal as stale or superseded.

## API Changes

- [x] `POST /api/repositories/{repositoryId}/operational-context/generate` creates a proposal.
- [x] `GET /api/repositories/{repositoryId}/operational-context/proposals` lists proposals.
- [x] `GET /api/repositories/{repositoryId}/operational-context/proposals/{proposalId}` loads proposal detail.

## Projection Changes

- [x] Extend `RepositoryWorkspaceProjection` with a proposal summary:
  - [x] Pending proposal exists.
  - [x] Latest proposal id.
  - [x] Generated timestamp.
  - [x] Proposal status.
  - [x] Source input count.
  - [x] Content byte and character count.

## UI Changes

- [x] Add a manual `Generate Proposal` action.
- [x] Show proposal existence, generated timestamp, status, and semantic changes.
- [x] Do not add accept, reject, edit, or promote actions yet.

## Tests

Add backend tests:

- [x] Generation succeeds without existing operational context.
- [x] Generation uses existing operational context when present.
- [x] Generation succeeds when handoff, decisions, or execution history are missing.
- [x] Proposal persists across service recreation.
- [x] Proposal content contains understanding sections rather than chronological replay.
- [x] Parser maps canonical Markdown sections into `OperationalContextDocument`.
- [x] Parser preserves unknown hand-written sections instead of discarding them.
- [x] Renderer round-trips the document model into stable Markdown.
- [x] Coarse semantic changes report section and item changes without deep interpretation.
- [x] Regeneration creates a new proposal and supersedes stale pending review state.
- [x] Workspace projection surfaces latest proposal summary.

## Certification

Generation is certified when Command Center can read current understanding and available repository artifacts, create a proposed understanding artifact, persist it, surface it in the workspace, and leave `.agents/operational_context.md` unchanged.
