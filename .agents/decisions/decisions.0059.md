# Decisions

## Newly Authorized

- Begin the Milestone 7 UI phase now that backend operational evolution projection is stable.
- Render backend-owned operational evolution directly in continuity and operational-context UI surfaces.
- Show modified count, identity basis, previous state, current state, modification reason, and supporting evidence from backend projections.
- Structure continuity UI presentation around semantic outcome groups: modified, added, removed, resolved, lost, and preserved.
- Keep modification identity authority centralized in `IUnderstandingDiffService`; UI, diagnostics, and certification must consume identity basis and evidence without reinterpreting identity.
- Do not introduce frontend ownership of continuity semantics or premature shared explainability abstractions during this UI slice.
