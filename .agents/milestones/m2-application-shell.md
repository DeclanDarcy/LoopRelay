# Milestone 2: Application Shell

## Tracking

- [ ] Milestone complete
- [ ] Workstream 2.1: Shell State
- [ ] Workstream 2.2: Sidebar
- [ ] Workstream 2.3: Header
- [ ] Workstream 2.4: Primary Workspace Tabs
- [ ] Workstream 2.5: Command Palette v1
- [ ] Certification complete

Goal: replace the top-level layout with the final shell while keeping existing workflow surfaces mostly intact.

This is a high-risk milestone because it is where navigation state becomes a first-class concern. The shell must feel instant even when backend projections are still loading. Client-owned navigation feedback should happen immediately; asynchronous projection refresh may continue after the navigation response.

## Workstream 2.1: Shell State

Use `shellState.ts` as the single source for client-owned navigation:

- [ ] Selected repository.
- [ ] Active primary tab.
- [ ] Selected artifact.
- [ ] Selected milestone.
- [ ] Palette visibility.
- [ ] Optional section target.

### Certification

- [ ] Tab changes mutate navigation state only.
- [ ] Repository switching does not mutate backend workflow state.
- [ ] In the mock certification app, tab switch visible response is p95 under 100ms.
- [ ] In the mock certification app, palette open, close, filter, and item navigation visible response is p95 under 100ms.
- [ ] Repository selection visually updates within 100ms and starts workspace loading without blocking the sidebar.
- [ ] Repository switching does not trigger duplicate workspace loads for the same selected repository.

## Workstream 2.2: Sidebar

Implement `components/shell/Sidebar.tsx`.

Required content:

- [ ] Product identity: Kernritsu, Compass, Command Center.
- [ ] Command launcher button with Ctrl+K / Meta+K hint.
- [ ] Global navigation entries: Overview, Repositories, Executions, Insights.
- [ ] Repository list sourced from dashboard projections.
- [ ] Repository row content:
  - [ ] Name.
  - [ ] Execution state.
  - [ ] Proposal indicator from continuity/proposal projection.
  - [ ] Dirty indicator only when backed by a projection.
  - [ ] Branch only when backed by a projection.

Rules:

- [ ] Do not issue per-repository git calls from the sidebar to manufacture branch or dirty data.
- [ ] If dashboard projections do not include branch or dirty state, omit those values or show a restrained unavailable state until a backend projection exists.
- [ ] Global navigation entries without backend projections should be disabled or route to explicit placeholder surfaces.

## Workstream 2.3: Header

Implement `components/shell/Header.tsx`.

Required content:

- [ ] Breadcrumb.
- [ ] Selected repository name and path.
- [ ] Branch if backed by selected-repository git status or workspace projection.
- [ ] Execution state badge.
- [ ] Refresh action using existing refresh behavior.
- [ ] Notification placement, disabled or empty until real notification data exists.
- [ ] Primary action placement.

Rules:

- [ ] Header consumes projections and hook command functions.
- [ ] Header owns no workflow state.

## Workstream 2.4: Primary Workspace Tabs

Implement `WorkspaceTabs` with:

- [ ] Workspace.
- [ ] Execution.
- [ ] Operational Context.
- [ ] Continuity.

Initial tab mapping:

- [ ] Workspace hosts the current repository workspace experience.
- [ ] Execution hosts existing execution-oriented surfaces.
- [ ] Operational Context hosts existing operational-context surfaces.
- [ ] Continuity hosts existing continuity diagnostics/report surfaces.

### Certification

- [ ] Switching tabs never mutates backend state.
- [ ] Existing actions still work from their new locations.
- [ ] Tab switch latency remains p95 under 100ms in Playwright against `?mock=workspace-certification`.

## Workstream 2.5: Command Palette v1

Implement `CommandPalette`.

Required behavior:

- [ ] Open/close with Ctrl+K or Meta+K.
- [ ] Close with Escape and outside click.
- [ ] Search/filter visible navigation targets.
- [ ] Navigate to repositories.
- [ ] Navigate to primary tabs.
- [ ] Navigate to major existing sections.

Forbidden in v1:

- [ ] Start execution.
- [ ] Abort execution.
- [ ] Commit.
- [ ] Push.
- [ ] Accept/reject handoff.
- [ ] Generate/edit/accept/reject/promote operational context.

### Certification

- [ ] Palette actions only update navigation state.
- [ ] Palette owns no workflow authority.
- [ ] Palette open, close, filtering, and navigation remain p95 under 100ms in Playwright against `?mock=workspace-certification`.
