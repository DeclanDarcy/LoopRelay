# Command Center Frontend Modernization Plan

## Objective

Modernize the Command Center frontend into a dense, dark, repository-centered operational console while preserving the current backend-owned workflow model.

The target application has these visible structural elements:

- A persistent left sidebar, approximately 264px wide, containing product identity, command launcher, global navigation, and repository selection.
- A top header containing breadcrumb, selected repository identity, refresh, notification placement, and primary action placement.
- Four primary repository workspaces: Workspace, Execution, Operational Context, and Continuity.
- A command palette opened by Ctrl+K or Meta+K.
- A Workspace tab with a workflow rail, execution context summary, live activity, milestones, and a right inspector rail.
- A dedicated Execution tab with full event stream, session details, execution diagnostics, context diagnostics, and launch readiness.
- A dedicated Operational Context tab for current understanding, proposed understanding, semantic changes, compression effects, decision continuity, review, and promotion visibility.
- A dedicated Continuity tab for understanding evolution, warnings, compression trends, decision retention, question lifecycle, risk lifecycle, and report visibility.

The migration is complete when the live application presents that structure using real backend projections and real platform capabilities, without moving business authority into React.

## Current Codebase Facts

- React UI lives in `src/CommandCenter.UI/src`.
- `src/CommandCenter.UI/src/App.tsx` currently contains projection DTOs, Tauri `invoke()` calls, backend `EventSource` setup, state hooks, draft state, workflow helpers, and the entire rendered UI.
- `src/CommandCenter.UI/src/App.css` currently contains the whole frontend visual system and layout.
- `src/CommandCenter.UI/src/devTauriMock.ts` duplicates many DTOs and provides a `?mock=workspace-certification` development state for frontend certification.
- The Tauri shell in `src/CommandCenter.Shell/src/main.rs` exposes commands for repositories, artifacts, execution context preview, operational-context proposal review/promotion, continuity diagnostics/reports, execution start, handoff accept/reject, git status, commit, and push.
- Backend endpoint registration is in `src/CommandCenter.Backend/Program.cs`.
- Backend projections and services already exist for repositories, workspaces, execution sessions, execution events, execution context, operational context, continuity diagnostics, continuity reports, git status, commit, and push.
- Backend tests are extensive under `tests/CommandCenter.Backend.Tests`.
- The frontend currently has `npm run lint` and `npm run build`, but no dedicated frontend test runner.

## Non-Negotiable Architecture Rules

1. Backend authority remains intact.
   Data and workflow decisions flow `Backend -> Projection -> UI`.

2. React owns presentation state only.
   React may own selected repository, selected tab, selected artifact, command-palette visibility, local editor drafts, expanded sections, focus, and scroll targets.

3. React must not become workflow authority.
   React must not decide execution transitions, handoff validity, commit readiness, push readiness, proposal lifecycle validity, promotion validity, continuity health, or repository readiness.

4. Navigation state, projection state, and draft state stay separate.
   Navigation state is synchronous and client-owned. Projection state is asynchronous and backend-owned. Draft state is local and ephemeral.

5. Missing backend capability must be represented honestly.
   If a target affordance requires data or behavior that does not exist, either add a backend projection/capability or render a disabled/omitted placeholder. Do not fabricate values in React.

6. The command palette is navigation-only until explicit backend workflow authority exists for palette actions.
   It may switch repositories, tabs, and sections. It must not start execution, abort execution, commit, push, accept, reject, or promote.

7. Existing repository-owned artifact semantics remain unchanged.
   Repository `.agents` files remain authoritative. Command Center reads, edits, rotates, proposes, reviews, promotes, and projects those artifacts; it does not replace them with hidden client state.

8. Manual refresh behavior is preserved unless a later backend projection explicitly introduces a different policy.

9. Do not over-implement the target mock.
   The target UX is authoritative for layout, interaction, navigation, density, and visual hierarchy. Mock data, fake statistics, and imaginary capabilities are not authoritative.

## Backend Projection Inventory

Use these existing projection and command surfaces before adding anything new:

- Dashboard list: `list_repositories`, backed by `GET /api/repositories`.
  Provides repository identity, availability, readiness, execution state, active execution session, execution summary, execution history, milestone count, current handoff/decisions flags, and continuity summary.

- Workspace: `get_repository_workspace` and `refresh_repository_workspace`, backed by `/api/repositories/{repositoryId}/workspace` and `/api/repositories/{repositoryId}/refresh`.
  Provides repository identity, availability, readiness, execution state, execution summary/history, artifact inventory, milestone count, artifact presence flags, operational-context proposal summary, and operational-context projection.

- Artifact content: `load_artifact_content`, `save_artifact_content`, `rotate_current_handoff`, and `rotate_current_decisions`.

- Execution context: `preview_execution_context`.
  Provides artifacts, repository git snapshot, aggregate diagnostics, per-artifact diagnostics, missing optional artifacts, validation errors, hard/warning thresholds, and launch-blocking status.

- Execution session and events: `start_execution`, `get_active_execution`, `get_execution_session`, execution status, execution events, and SSE event stream from `/api/execution-sessions/{sessionId}/events/stream`.

- Handoff decision: `accept_execution_handoff` and `reject_execution_handoff`.

- Git, commit, and push: `get_git_status`, `prepare_commit`, `commit_execution`, and `push_execution`.

- Operational context: `generate_operational_context_proposal`, `list_operational_context_proposals`, `get_operational_context_proposal`, `edit_operational_context_proposal`, `accept_operational_context_proposal`, `reject_operational_context_proposal`, and `promote_operational_context_proposal`.

- Continuity: `get_continuity_diagnostics`, `generate_continuity_report`, and `list_continuity_reports`.

## Target Frontend File Structure

Create a modular frontend structure under `src/CommandCenter.UI/src`:

```text
api/
  artifacts.ts
  backend.ts
  continuity.ts
  execution.ts
  git.ts
  operationalContext.ts
  repositories.ts
  tauri.ts
  index.ts

components/
  design/
    Badge.tsx
    Button.tsx
    EmptyState.tsx
    IconButton.tsx
    InspectorSection.tsx
    Metric.tsx
    Panel.tsx
    SectionHeader.tsx
    StatusBadge.tsx
    Table.tsx
    Tabs.tsx
  shell/
    AppShell.tsx
    CommandPalette.tsx
    Header.tsx
    Sidebar.tsx
    WorkspaceTabs.tsx

features/
  artifacts/
  continuity/
  execution/
  operational-context/
  repositories/
  workspace/

hooks/
  useArtifactContent.ts
  useContinuityDiagnostics.ts
  useExecutionContextPreview.ts
  useExecutionEvents.ts
  useExecutionSession.ts
  useGitStatus.ts
  useOperationalContextProposal.ts
  useRepositories.ts
  useRepositoryWorkspace.ts

lib/
  executionWorkflow.ts
  formatting.ts
  markdown.tsx
  status.ts

state/
  shellState.ts

styles/
  base.css
  tokens.css
  theme.css

test/
  characterization/
  fixtures/
  render.tsx

e2e/
  workspace-certification.spec.ts

types/
  artifacts.ts
  continuity.ts
  execution.ts
  git.ts
  operationalContext.ts
  planning.ts
  repositories.ts
  index.ts
```

`App.tsx` should become a thin composition root that wires providers, hooks, and shell state. It should not contain DTO declarations, transport calls, projection loading effects, or large rendering branches.

## Milestone 0: Frontend Foundations

(See ./milestones/m0-frontend-foundations.md)

## Milestone 1: Design System Foundation

(See ./milestones/m1-design-system-foundation.md)

## Milestone 2: Application Shell

(See ./milestones/m2-application-shell.md)

## Milestone 3: Workspace Migration

(See ./milestones/m3-workspace-migration.md)

## Milestone 4: Execution Workspace

(See ./milestones/m4-execution-workspace.md)

## Milestone 5: Operational Context Workspace

(See ./milestones/m5-operational-context-workspace.md)

## Milestone 6: Continuity Workspace

(See ./milestones/m6-continuity-workspace.md)

## Milestone 7: Navigation, Discovery, and Cohesion

(See ./milestones/m7-navigation-discovery-and-cohesion.md)

## Milestone 8: Capability Gaps, Cleanup, and Final Validation

(See ./milestones/m8-capability-gaps-cleanup-and-final-validation.md)

## Verification Plan

Run these gates at minimum:

```powershell
dotnet test CommandCenter.slnx
cd src\CommandCenter.UI
npm run lint
npm run build
```

From Milestone 0 onward, also run:

```powershell
cd src\CommandCenter.UI
npm run test
npm run test:e2e
```

Manual or browser-driven certification should cover:

- Desktop 1440x900.
- Desktop 1280x800.
- Narrow/mobile around 390x844.
- `?mock=workspace-certification`.
- Real backend through Tauri/dev server where available.

Critical scenarios:

- Register repository.
- Remove repository registration.
- Select repository.
- Refresh workspace.
- Select artifact.
- Edit and save artifact.
- Rotate current handoff.
- Rotate current decisions.
- Select milestone.
- Build execution context.
- Start execution.
- Stream execution events.
- Accept generated handoff.
- Reject generated handoff.
- Prepare commit.
- Select commit scope.
- Commit.
- Push.
- Generate operational-context proposal.
- Edit proposal.
- Accept proposal.
- Reject proposal.
- Promote proposal.
- Load continuity diagnostics.
- Generate continuity report.
- Navigate with tabs.
- Navigate with command palette.
- Navigate cross-workspace links.

## Definition of Done

The modernization is done when:

- `App.tsx` is a thin composition root.
- Projection DTOs live in `src/types`.
- Transport lives in `src/api`.
- Projection loading lives in hooks.
- Navigation state, projection state, and draft state are separated.
- The dark operational design system is tokenized and reused.
- Sidebar, header, primary tabs, command palette, workflow rail, Workspace inspector, Execution workspace, Operational Context workspace, and Continuity workspace exist.
- All displayed values come from backend projections or explicit local draft/navigation state.
- All workflow mutations use existing or newly added backend-owned commands.
- Missing capabilities are either implemented backend-first or explicitly disabled/deferred.
- Intentional UX/capability deviations are recorded in `docs/frontend-modernization-deviations.md`.
- Shell navigation latency remains within the defined p95 bounds in the mock certification app.
- Legacy CSS, duplicate components, duplicate DTOs, and unused migration scaffolding are removed.
- Backend tests, frontend lint/build, and frontend characterization/e2e tests pass.
