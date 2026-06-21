# Milestone 1: Design System Foundation

## Tracking

- [ ] Milestone complete
- [ ] Workstream 1.1: Token System
- [ ] Workstream 1.2: Typography
- [ ] Workstream 1.3: Shared Primitives
- [ ] Workstream 1.4: Status Language
- [ ] Workstream 1.5: Apply Theme Without Layout Migration
- [ ] Certification complete

Goal: establish the dark operational visual system without changing information architecture.

## Workstream 1.1: Token System

Create `src/styles/tokens.css` and use CSS variables for:

- [ ] Canvas and surfaces:
  - [ ] `--bg-canvas`
  - [ ] `--bg-inset`
  - [ ] `--surface-1`
  - [ ] `--surface-2`
  - [ ] `--surface-3`
  - [ ] `--surface-overlay`
- [ ] Borders:
  - [ ] `--border-subtle`
  - [ ] `--border-default`
  - [ ] `--border-strong`
- [ ] Text:
  - [ ] `--fg-default`
  - [ ] `--fg-muted`
  - [ ] `--fg-subtle`
  - [ ] `--fg-faint`
- [ ] Status:
  - [ ] success, warning, danger, info, done.
- [ ] Accent:
  - [ ] subtle, muted, foreground, emphasis.
- [ ] Radius:
  - [ ] 4px, 6px, 8px, 12px where needed.
- [ ] Shadows:
  - [ ] panel, popover, modal.

Use the dark console palette consistently. Do not leave major surfaces on the current light theme.

## Workstream 1.2: Typography

Create type tokens for:

- [ ] Body.
- [ ] Large body.
- [ ] Small text.
- [ ] Caption.
- [ ] Micro uppercase labels.
- [ ] H2, H3, H4.
- [ ] Mono small.

Rules:

- [ ] Use dense, operational typography.
- [ ] Keep letter spacing at `0` for normal text.
- [ ] Use uppercase letter spacing only for micro labels.
- [ ] Do not scale font size with viewport width.

## Workstream 1.3: Shared Primitives

Create visual primitives:

- [ ] `Button`: primary, secondary, danger, ghost.
- [ ] `IconButton`: for refresh, notification, expand, search, and similar icon actions.
- [ ] `Badge` and `StatusBadge`.
- [ ] `Panel`.
- [ ] `InspectorSection`.
- [ ] `Metric`.
- [ ] `Table`.
- [ ] `Tabs`.
- [ ] `EmptyState`.
- [ ] `SectionHeader`.

Rules:

- [ ] Cards and panels use 8px radius or less unless modal/palette needs 12px.
- [ ] Do not nest cards inside cards.
- [ ] Use icons for familiar actions where available through the existing dependency set or inline only where no icon library exists.

## Workstream 1.4: Status Language

Centralize status presentation in `src/lib/status.ts`.

Cover:

- [ ] `RepositoryAvailability`
- [ ] `ExecutionReadiness`
- [ ] `RepositoryExecutionState`
- [ ] `ExecutionSessionState`
- [ ] `OperationalContextProposalStatus`
- [ ] `OperationalContextReviewState`
- [ ] Continuity warning severity, where projected.

### Certification

- [ ] Equivalent states render with the same label, color, and badge style across repository list, header, workspace, execution, operational context, and continuity.

## Workstream 1.5: Apply Theme Without Layout Migration

- [ ] Convert current surfaces to the new tokens and primitives.
- [ ] Preserve current component hierarchy and user workflows.
- [ ] Avoid introducing sidebar, header, command palette, workflow rail changes, or tab changes in this milestone.

### Certification

- [ ] The app is visibly on the dark console theme.
- [ ] Existing interactions are unchanged.
- [ ] `npm run lint`, `npm run build`, and frontend characterization tests pass.
