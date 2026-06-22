# M8 Final Validation

Date: 2026-06-21

## Scope

This audit validates Milestone 8 after the remaining presentation-only execution review surfaces were extracted from `App.tsx`.

The audit question was not whether `App.tsx` is small. The question was whether any remaining `App.tsx` responsibility can move without relocating workflow authority, draft authority, readiness authority, backend dispatch, or post-mutation reconciliation into presentation features.

## App Authority Boundary

Result: Natural Authority Boundary Reached.

Remaining `App.tsx` responsibilities are authority-oriented:

- Repository selection, artifact selection, milestone selection, and cross-workspace navigation coordination.
- Local draft ownership for artifact edits, commit messages, commit scope, operational-context proposal edits, and review notes.
- Readiness derivation for execution launch, handoff review, commit, push, operational-context review, and promotion.
- Backend command dispatch for repository registration/removal, workspace refresh, artifact save/rotate, execution start, handoff accept/reject, commit, push, operational-context proposal lifecycle, and continuity report generation.
- Post-mutation projection reconciliation across repository dashboard, selected workspace, execution session, selected artifact, git status, operational-context content, continuity diagnostics, and reports.
- Stream-driven execution session reconciliation while backend projections remain authoritative.

The last audited candidates were the Execution Context panel factory and open-section helpers. They are intentionally retained because they bind reusable presentation surfaces to selected repository, selected milestone, workflow command callbacks, and shell navigation state. Extracting them now would hide authority behind container glue rather than remove presentation ownership.

## UX Validation

- Layout: Persistent sidebar, header, tab row, workspace grid, feature tabs, right inspector rail, and dense panel system are present.
- Navigation: Repository selection, workspace tabs, cross-workspace links, discovery targets, and command palette targets are wired through shell navigation state.
- Interaction: Mutations remain explicit actions. The command palette remains navigation-only.
- Workspace structure: Workspace, Execution, Operational Context, and Continuity workspaces are present and backed by existing projections.
- Information density: Dense operational panels and inspector summaries are retained without marketing or fake dashboard content.
- Projection ownership: DTOs live in `types`, transport in `api`, projection loading in hooks, and backend projections remain the source of displayed workflow state.
- Workflow ownership: React feature components receive state and callbacks; backend commands and readiness orchestration remain centralized in `App.tsx`.
- Responsive behavior: Existing Playwright certification covers desktop and narrow/mobile mock rendering.
- Keyboard behavior: Command palette keyboard opening remains covered by certification.
- Error/loading/empty states: Feature surfaces render explicit loading, disabled, and empty states without synthetic values.

## Remaining Deviations

Every intentional M8 deviation found in this validation is recorded in `docs/frontend-modernization-deviations.md`.

Capability-based deferrals remain backend-owned gaps:

- User-invokable abort execution.
- Global Overview.
- Global Executions.
- Insights.
- Notifications.
- Repository git summary for all repositories.
- Ahead/behind counts outside selected repository status.
- Milestone criteria progress.
- Cross-repository execution views.
- Cross-repository continuity and insight rollups.

The only architecture/product deviation added by this slice is:

- `App.tsx` is not a physically thin composition root because the current frontend boundary deliberately centralizes workflow, draft, readiness, mutation, and reconciliation authority.

## Certification Result

M8 final UX validation is complete.

Certification is complete subject to the standard verification gate passing after this documentation update.
