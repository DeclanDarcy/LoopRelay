# Decisions

## Newly Authorized

- Continue Milestone 9 cleanup using the established pattern: retire duplicated presentation only after shared explainability components demonstrate equivalent coverage.
- Treat the recommendation and burden explanation cleanup as valid because it consolidates evidence, diagnostics, fact chips, winning-signal presentation, and rationale into shared explainability rendering without changing backend semantics.
- Preserve `DecisionGovernanceExplanation` as a valid thin wrapper because it provides grouping, severity organization, proposal navigation, and domain framing while delegating evidence and diagnostics rendering.
- Continue validating cleanup through adapter preservation tests, affected UI characterization tests, and build verification.
- Continue the next cleanup audit in this order: health, certification, diagnostics, continuity, generation certification.
- Classify remaining renderers using this disposition table: duplicate evidence renderers move to `EvidenceList`; duplicate diagnostics renderers move to `DiagnosticList`; duplicate health rendering moves to `HealthView`; duplicate certification findings move to shared certification components; domain grouping/navigation wrappers are kept; domain-specific visualizations are kept.
- Treat the remaining Milestone 9 trajectory as final cleanup: remove obsolete presentation, preserve domain-specific composition, keep shared explainability as the single rendering path, and prepare for the final cohesion audit.
