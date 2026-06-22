# Decisions

## Newly Authorized

- Treat M5 as successfully certified and closed.
- Preserve the post-M5 authority model: `WorkspaceTab`, `ExecutionTab`, and `OperationalContextTab` own presentation/composition only, while `App.tsx` continues to own workflow authority, draft authority, readiness authority, mutation authority, backend dispatch, selected repository state, and lifecycle refresh behavior.
- Start M6 by reviewing the milestone definition and separating pure continuity presentation responsibilities from backend-owned continuity projection authority before extraction begins.
- Target M6 extraction toward `features/continuity/ContinuityTab.tsx` owning continuity presentation, diagnostic presentation, warning presentation, evolution history presentation, and cross-links.
- Keep M6 continuity state projection-driven: consume projected continuity state, diagnostics, warnings, trends, reports, and artifact paths from existing backend projections/hooks.
- Treat M6's main risk as interpretation leakage rather than mutation leakage.
- M6 may display warnings, group warnings, sort warnings, cross-link to evidence, show evolution history, and show continuity diagnostics.
- M6 must not block workflows, calculate readiness, calculate health scores, calculate continuity scores, dispatch backend actions, perform governance decisions, add promotion/execution/readiness/confidence gates, auto-correct, auto-promote, or auto-reject.
- Preserve the navigation-only cross-link rule for M6: continuity links may switch tabs, scroll to sections, focus evidence, or select valid projected artifacts, but must not invoke backend mutation endpoints.
- Certify M6 against this standard: if every backend mutation endpoint disappeared, all continuity cross-links should still function.
