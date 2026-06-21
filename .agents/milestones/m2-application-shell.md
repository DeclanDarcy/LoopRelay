# Milestone 2: Application Shell

## Tracking

- [ ] Milestone complete
- [x] Workstream 2.1: Shell State
- [x] Workstream 2.2: Sidebar
- [x] Workstream 2.3: Header
- [x] Workstream 2.4: Primary Workspace Tabs
- [x] Workstream 2.5: Command Palette v1
- [ ] Certification complete

Goal: replace the top-level layout with the final shell while keeping existing workflow surfaces mostly intact.

This is a high-risk milestone because it is where navigation state becomes a first-class concern. The shell must feel instant even when backend projections are still loading. Client-owned navigation feedback should happen immediately; asynchronous projection refresh may continue after the navigation response.

## Workstream 2.1: Shell State

Use `shellState.ts` as the single source for client-owned navigation:

- [x] Selected repository.
- [x] Active primary tab.
- [x] Selected artifact.
- [x] Selected milestone.
- [x] Palette visibility.
- [x] Optional section target.

### Certification

- [x] Tab changes mutate navigation state only.
- [x] Repository switching does not mutate backend workflow state.
- [ ] In the mock certification app, tab switch visible response is p95 under 100ms.
- [ ] In the mock certification app, palette open, close, filter, and item navigation visible response is p95 under 100ms.
- [ ] Repository selection visually updates within 100ms and starts workspace loading without blocking the sidebar.
- [ ] Repository switching does not trigger duplicate workspace loads for the same selected repository.

## Workstream 2.2: Sidebar

Implement `components/shell/Sidebar.tsx`.

Required content:

- [x] Product identity: Kernritsu, Compass, Command Center.
- [x] Command launcher button with Ctrl+K / Meta+K hint.
- [x] Global navigation entries: Overview, Repositories, Executions, Insights.
- [x] Repository list sourced from dashboard projections.
- [x] Repository row content:
  - [x] Name.
  - [x] Execution state.
  - [x] Proposal indicator from continuity/proposal projection.
  - [x] Dirty indicator only when backed by a projection.
  - [x] Branch only when backed by a projection.

Rules:

- [x] Do not issue per-repository git calls from the sidebar to manufacture branch or dirty data.
- [x] If dashboard projections do not include branch or dirty state, omit those values or show a restrained unavailable state until a backend projection exists.
- [x] Global navigation entries without backend projections should be disabled or route to explicit placeholder surfaces.

## Workstream 2.3: Header

Implement `components/shell/Header.tsx`.

Required content:

- [x] Breadcrumb.
- [x] Selected repository name and path.
- [x] Branch if backed by selected-repository git status or workspace projection.
- [x] Execution state badge.
- [x] Refresh action using existing refresh behavior.
- [x] Notification placement, disabled or empty until real notification data exists.
- [x] Primary action placement.

Rules:

- [x] Header consumes projections and hook command functions.
- [x] Header owns no workflow state.

## Workstream 2.4: Primary Workspace Tabs

Implement `WorkspaceTabs` with:

- [x] Workspace.
- [x] Execution.
- [x] Operational Context.
- [x] Continuity.

Initial tab mapping:

- [x] Workspace hosts the current repository workspace experience.
- [x] Execution hosts existing execution-oriented surfaces.
- [x] Operational Context hosts existing operational-context surfaces.
- [x] Continuity hosts existing continuity diagnostics/report surfaces.

### Certification

- [x] Switching tabs never mutates backend state.
- [x] Existing actions still work from their new locations.
- [ ] Tab switch latency remains p95 under 100ms in Playwright against `?mock=workspace-certification`.

## Workstream 2.5: Command Palette v1

Implement `CommandPalette`.

Required behavior:

- [x] Open/close with Ctrl+K or Meta+K.
- [x] Close with Escape and outside click.
- [x] Search/filter visible navigation targets.
- [x] Navigate to repositories.
- [x] Navigate to primary tabs.
- [x] Navigate to major existing sections.

Forbidden in v1:

- [x] Start execution is not exposed.
- [x] Abort execution is not exposed.
- [x] Commit is not exposed.
- [x] Push is not exposed.
- [x] Accept/reject handoff is not exposed.
- [x] Generate/edit/accept/reject/promote operational context is not exposed.

### Certification

- [x] Palette actions only update navigation state.
- [x] Palette owns no workflow authority.
- [ ] Palette open, close, filtering, and navigation remain p95 under 100ms in Playwright against `?mock=workspace-certification`.

## Slice Notes

- 2026-06-21: Implemented the M2 shell layer with `AppShell`, `Sidebar`, `Header`, `WorkspaceTabs`, and `CommandPalette`; extended `shellState` with active primary tab, palette visibility, and section target ownership; wired the existing repository detail, execution, operational-context, continuity, and artifact surfaces into tab-visible shell regions without moving workflow authority out of `App.tsx`.
- 2026-06-21: Sidebar now uses dashboard projections only, omits branch/dirty values because the dashboard projection does not provide them, shows proposal state only from the continuity summary, and keeps unsupported global navigation entries disabled.
- 2026-06-21: Verification passed after shell implementation: `npm run lint`, `npm run test`, `npm run build`, `npm run test:e2e`, and `dotnet test CommandCenter.slnx`. Certification remains open for explicit p95 tab and command-palette latency coverage.
