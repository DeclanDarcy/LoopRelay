# Decisions

## Newly Authorized

- Continue Milestone 6 by completing reconstruction, query, and trace transparency coverage.
- Audit every Milestone 6 backend projection field before declaring reconstruction/query/trace UI coverage complete.
- Every backend-owned confidence, scope, evidence, direction, source, target, historical cutoff, and diagnostic field introduced for Milestone 6 must be rendered by an appropriate reasoning surface.
- Characterization tests must prove each transparency branch is visible.
- The UI must remain presentation-only and must not derive confidence, reconstruction scope, or evidence relationships from counts or graph topology.
- Add a field-to-surface audit artifact as milestone evidence mapping backend transparency fields to the UI components responsible for rendering them.
- Stage, commit, and push the resulting execution-session changes after the slice is complete.
