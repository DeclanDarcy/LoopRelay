# Decisions

## Newly Authorized

- Accept the governance recovery migration as the closing slice for Milestone 9 interaction-normalization work.
- Treat the Governance action family as complete for interaction normalization after transfer and recovery now use the shared interaction language.
- Preserve the boundary that backend services own recovery eligibility, recovery state, evidence, diagnostics, and lifecycle semantics.
- Keep governance adapters as presentation reshapers of authoritative backend projections rather than domain authorities.
- Continue using `InteractionPatternView` for action-oriented interactions that explain action, subject, eligibility, evidence, result, and diagnostics.
- Do not force non-action surfaces into `InteractionPatternView`, including diagnostics dashboards, evolution reports, evidence explorers, certification summaries, and trajectory visualizations.
- Treat the unified operational dashboard as the next Milestone 9 slice.
- Build the dashboard by composing existing projections instead of inventing new lifecycle, evidence, diagnostics, or reasoning authority.
- Keep dashboard sections high-level and navigational: concise status, counts, recent activity, highest-priority issues, and direct links into owning workspaces.
- Avoid reproducing detailed evidence, diagnostics, or lifecycle content in the dashboard when those details already have a primary workspace.
