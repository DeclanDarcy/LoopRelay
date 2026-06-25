# Decisions

## Newly Authorized

- Continue Milestone 7 with decision assimilation transparency as the next dependency.
- Extend backend projections where needed to expose immutable taxonomy basis, matched rules, matched evidence, heuristic fallback information, ambiguity diagnostics, durable assimilation status, `Excluded` versus `OmittedByLimit`, and consequence relationships back to originating decisions.
- Preserve decision assimilation transparency fields through persistence, transport, and TypeScript contracts.
- Render backend-authored assimilation and taxonomy fields directly in `OperationalContextAssimilationPanel`, `OperationalContextTaxonomyPanel`, `OperationalContextAssimilationLimitPanel`, and `OperationalContextConsequencePanel`.
- Add authority regression tests proving React does not determine assimilation status, classify taxonomy, decide omission versus exclusion, or reconstruct consequence relationships.
- After decision assimilation transparency, leave Milestone 7 primarily for projection-gap verification and exit audit before starting the shared explainability work in Milestone 8.
