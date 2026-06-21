# Milestone 0: Frontend Foundations

## Tracking

- [ ] Milestone complete
- [x] Workstream 0.0: Mandatory Frontend Test Infrastructure
- [x] Workstream 0.1: Centralize Types
- [x] Workstream 0.2: Centralize Transport
- [ ] Workstream 0.3: Extract Projection Hooks
- [ ] Workstream 0.4: Separate State Boundaries
- [ ] Workstream 0.5: Decompose Without Redesign
- [ ] Workstream 0.6: Add Characterization Protection
- [ ] Certification complete

Goal: create stable type, transport, hook, and state boundaries with no visible UX change.

## Workstream 0.0: Mandatory Frontend Test Infrastructure

Add frontend test infrastructure before extracting types, APIs, hooks, or components.

Required tooling:

- [x] Vitest for unit and hook tests.
- [x] Testing Library for component behavior tests.
- [x] Playwright for browser-level characterization and layout/navigation checks.
- [x] A stable render helper for components that need shell state, mock Tauri commands, and projection fixtures.
- [ ] Script entries:
  - [x] `npm run test`
  - [x] `npm run test:e2e`
  - [x] Optional `npm run test:watch`

Required setup:

- [x] Make `devTauriMock.ts` usable by tests or extract its fixtures into reusable builders.
- [x] Add fixtures for at least one repository in every repository execution state: `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- [x] Add browser tests that launch the Vite app with `?mock=workspace-certification`.
- [x] Add a small performance helper for client-side navigation timing in Playwright.

### Certification

- [x] A trivial Vitest test and a trivial Playwright test pass before any M0 extraction begins.
- [x] `npm run lint`, `npm run build`, `npm run test`, and `npm run test:e2e` are available from `src/CommandCenter.UI`.
- [x] Subsequent M0 work must add or update characterization coverage before moving behavior.

## Workstream 0.1: Centralize Types

- [x] Move all frontend DTOs out of `App.tsx` into `src/types`.
- [x] Import those types from `App.tsx`, feature components, hooks, API modules, and `devTauriMock.ts`.
- [x] Include these type families:
  - [x] Repository and dashboard projections.
  - [x] Workspace projection and artifact inventory.
  - [x] Execution states, session summaries, status, events, context preview, context diagnostics.
  - [x] Git status, dirty state, commit preparation, commit request, push result.
  - [x] Operational-context projection, proposal summary, proposal, semantic change, compression summary, review state, promotion state.
  - [x] Continuity diagnostics, trends, compression trend, and reports.
  - [x] Planning projection and milestones.

### Certification

- [x] No projection DTO definitions remain in `App.tsx`.
- [x] `devTauriMock.ts` imports shared DTOs instead of maintaining a parallel type universe where practical.
- [x] `npm run build` succeeds.

## Workstream 0.2: Centralize Transport

- [x] Create API modules wrapping every Tauri command currently invoked from `App.tsx`.
- [x] Create one low-level `invokeCommand<T>()` wrapper in `src/api/tauri.ts` to normalize unknown errors.
- [x] Create execution event subscription helpers that own `EventSource` construction and cleanup.
- [x] Keep command names and endpoint-specific knowledge inside `src/api`.
- [x] Keep UI components unaware of Tauri command names, backend URLs, request serialization, and SSE framing.

### Certification

- [x] No direct `invoke()` imports remain in rendering components.
- [x] No direct `EventSource` construction remains outside the execution API or event hook.
- [x] Error formatting is centralized.

## Workstream 0.3: Extract Projection Hooks

Create hooks that own loading, refreshing, errors, and cleanup:

- [x] `useRepositories()`
- [x] `useRepositoryWorkspace(repositoryId)`
- [x] `useArtifactContent(repositoryId, relativePath)`
- [x] `useExecutionContextPreview(repositoryId, milestonePath)`
- [x] `useExecutionSession(repositoryId, sessionId)`
- [x] `useExecutionEvents(sessionId)`
- [x] `useGitStatus(repositoryId)`
- [ ] `useCommitPreparation(sessionId)`
- [ ] `useOperationalContextProposal(repositoryId, proposalId)`
- [ ] `useContinuityDiagnostics(repositoryId)`

Rules:

- [ ] Hooks may call API modules.
- [ ] Hooks may expose `data`, `isLoading`, `error`, and command functions.
- [ ] Hooks must not own workflow authority.
- [ ] Hooks must not mutate backend state except through explicit command functions called by a user action.

### Certification

- [ ] Projection-loading effects are removed from `App.tsx`.
- [ ] Existing loading and error behavior is preserved.
- [ ] Existing manual refresh behavior is preserved.

## Workstream 0.4: Separate State Boundaries

Create shell/navigation state in `src/state/shellState.ts`.

Navigation state:

- [ ] Selected repository id.
- [ ] Active primary tab: `workspace`, `execution`, `operational-context`, `continuity`.
- [ ] Selected artifact path by repository.
- [ ] Selected milestone path by repository.
- [ ] Command palette open/closed.
- [ ] Optional section anchors and expanded sections.

Projection state:

- [ ] Repository dashboard projection.
- [ ] Workspace projection.
- [ ] Execution projection/status/events.
- [ ] Operational context projection/proposal.
- [ ] Continuity diagnostics/reports.
- [ ] Git and commit projections.

Draft state:

- [ ] Artifact editor draft.
- [ ] Commit message draft.
- [ ] Commit path selection.
- [ ] Operational-context proposal edit draft.
- [ ] Review note draft.

### Certification

- [ ] Changing draft state does not trigger projection reloads.
- [ ] Changing tabs does not trigger backend mutations.
- [ ] Navigation state never stores projection objects.

## Workstream 0.5: Decompose Without Redesign

- [ ] Extract current large rendering sections into feature components while preserving current layout and class names.
- [ ] Extract pure helpers from `App.tsx` into `src/lib`, including formatting, markdown rendering, artifact categories, dirty path counts, and workflow-display mapping.
- [ ] Keep the user-visible interface unchanged during this milestone.

### Certification

- [ ] Existing UI behavior is unchanged.
- [ ] The old layout still renders while `App.tsx` becomes substantially smaller.

## Workstream 0.6: Add Characterization Protection

Use the mandatory frontend test infrastructure to protect current behavior before structural redesign.

Minimum scenarios:

- [ ] Repository list loads and first repository selection behavior is preserved.
- [ ] Selecting a repository loads the same workspace projection.
- [ ] Refresh reloads workspace and reconciles selected artifacts.
- [ ] Milestone selection builds execution context only when requested.
- [x] Execution events merge by sequence and preserve ordering.
- [x] SSE cleanup occurs when session changes or unmounts.
- [ ] Proposal generation, load, edit, accept, reject, and promote keep current gating.
- [ ] Commit preparation, selection, commit, and push keep current gating.
- [ ] Continuity diagnostics and report generation remain read-only except for explicit report generation.

Use `?mock=workspace-certification` to certify all repository execution states:

- [ ] `Ready`
- [ ] `Executing`
- [ ] `AwaitingAcceptance`
- [ ] `AwaitingCommit`
- [ ] `AwaitingPush`
- [ ] `Failed`
- [ ] `Cancelled`

### Certification

- [ ] Frontend tests cover the behavior above.
- [ ] `npm run lint`, `npm run build`, `npm run test`, `npm run test:e2e`, and `dotnet test CommandCenter.slnx` pass.
