# Milestone 1: Design System Foundation

## Tracking

- [ ] Milestone complete
- [x] Workstream 1.1: Token System
- [x] Workstream 1.2: Typography
- [x] Workstream 1.3: Shared Primitives
- [ ] Workstream 1.4: Status Language
- [ ] Workstream 1.5: Apply Theme Without Layout Migration
- [ ] Certification complete

Goal: establish the dark operational visual system without changing information architecture.

## Workstream 1.1: Token System

Create `src/styles/tokens.css` and use CSS variables for:

- [x] Canvas and surfaces:
  - [x] `--bg-canvas`
  - [x] `--bg-inset`
  - [x] `--surface-1`
  - [x] `--surface-2`
  - [x] `--surface-3`
  - [x] `--surface-overlay`
- [x] Borders:
  - [x] `--border-subtle`
  - [x] `--border-default`
  - [x] `--border-strong`
- [x] Text:
  - [x] `--fg-default`
  - [x] `--fg-muted`
  - [x] `--fg-subtle`
  - [x] `--fg-faint`
- [x] Status:
  - [x] success, warning, danger, info, done.
- [x] Accent:
  - [x] subtle, muted, foreground, emphasis.
- [x] Radius:
  - [x] 4px, 6px, 8px, 12px where needed.
- [x] Shadows:
  - [x] panel, popover, modal.

Use the dark console palette consistently. Do not leave major surfaces on the current light theme.

## Workstream 1.2: Typography

Create type tokens for:

- [x] Body.
- [x] Large body.
- [x] Small text.
- [x] Caption.
- [x] Micro uppercase labels.
- [x] H2, H3, H4.
- [x] Mono small.

Rules:

- [x] Use dense, operational typography.
- [x] Keep letter spacing at `0` for normal text.
- [x] Use uppercase letter spacing only for micro labels.
- [x] Do not scale font size with viewport width.

## Workstream 1.3: Shared Primitives

Create visual primitives:

- [x] `Button`: primary, secondary, danger, ghost.
- [x] `IconButton`: for refresh, notification, expand, search, and similar icon actions.
- [x] `Badge` and `StatusBadge`.
- [x] `Panel`.
- [x] `InspectorSection`.
- [x] `Metric`.
- [x] `Table`.
- [x] `Tabs`.
- [x] `EmptyState`.
- [x] `SectionHeader`.

Rules:

- [x] Cards and panels use 8px radius or less unless modal/palette needs 12px.
- [x] Do not nest cards inside cards.
- [x] Use icons for familiar actions where available through the existing dependency set or inline only where no icon library exists.

## Workstream 1.4: Status Language

Centralize status presentation in `src/lib/status.ts`.

Cover:

- [x] `RepositoryAvailability`
- [x] `ExecutionReadiness`
- [x] `RepositoryExecutionState`
- [x] `ExecutionSessionState`
- [x] `OperationalContextProposalStatus`
- [x] `OperationalContextReviewState`
- [x] Continuity warning severity, where projected.

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

## Slice Notes

- 2026-06-21: Added `src/styles/tokens.css`, `base.css`, and `theme.css` with dark console tokens, dense type tokens, primitive class styles, and global dark color-scheme.
- 2026-06-21: Added render-only design primitives under `src/components/design`.
- 2026-06-21: Added `src/lib/status.ts` with centralized status presentation metadata for the projected status types.
- 2026-06-21: Converted the existing `App.css` surfaces from hard-coded light colors to tokens without changing hierarchy, navigation, workflow ownership, or `App.tsx` behavior.
- 2026-06-21: Status helper adoption in existing render branches remains pending; until that is wired, Workstream 1.4 certification and Milestone 1.5 remain open.
- 2026-06-21: Verification passed: `npm run lint`, `npm run build`, `npm run test`, and `npm run test:e2e`.
