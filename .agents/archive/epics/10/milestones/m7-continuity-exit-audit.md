# Milestone 7 Continuity Exit Audit

## Projection Coverage

- Decision assimilation analysis is projected through `DecisionAssimilationProjection` and rendered by `OperationalContextAssimilationPanel`.
- Taxonomy basis is projected on each `DecisionAssimilationRecord.taxonomyBasis` and rendered by `OperationalContextTaxonomyPanel`.
- Assimilation limits are projected through `DecisionAssimilationLimit` and rendered by `OperationalContextAssimilationLimitPanel`.
- Decision consequences remain linked to `ContinuityDecisionReference` and are rendered by `OperationalContextConsequencePanel`.
- Contradictions remain linked to both decision references and are rendered by `OperationalContextContradictionPanel`.
- Operational evolution is projected through `OperationalEvolutionSummary.semanticChanges` and `timelineEntries`; proposal review can consume semantic changes, while continuity diagnostics consumes revision-history timeline entries.
- Compression is projected through `OperationalContextCompressionSummary` and `OperationalContextCompressionOutcome`, including rule, threshold, rationale, and evidence.

## UI Reconstruction Audit

- React does not compute assimilation eligibility, taxonomy, contradiction severity, compression outcomes, or operational evolution.
- `OperationalContextProposalComparison` now receives backend semantic changes and displays modification facts before raw markdown comparison.
- `OperationalContextSemanticChangeList` now places modification facts before other grouped semantic outcomes while preserving backend-provided labels and fields.
- Compatibility markdown panes remain only as raw current/proposed content previews; they are no longer the primary modification explanation.

## Compression Taxonomy Audit

- Current compression item outcomes are `Retained`, `Added`, `Removed`, `Compressed`, `DuplicateRemoved`, `TransientRemoved`, `ResolvedQuestion`, and `RetiredRisk`.
- No backend continuity service performs a distinct merge operation, so no `Merged` outcome is emitted for Milestone 7.
- Noise removal is represented by aggregate `NoiseRemovedIndicators` plus item-level `DuplicateRemoved`, `TransientRemoved`, and window-limit `Compressed` outcomes.
- No distinct item-level `NoiseRemoved` outcome is emitted because current backend semantics classify the specific removal cause instead.

## Exit Criteria Mapping

- Every analyzed decision explains assimilated or rejected state through `DecisionAssimilationRecord.status`, `isAssimilated`, `exclusionReason`, `omissionReason`, and source evidence.
- Taxonomy classifications expose matched evidence, matched rules, heuristic fallback state, fallback reason, and diagnostics.
- Assimilation limits expose total analyzed, qualifying, assimilated, omitted, limit, and reason.
- Consequences retain originating decision identity, source path, statement, taxonomy, supporting evidence, and operational impact.
- Operational evolution distinguishes added, modified, removed, preserved, lost, and resolved understanding through typed semantic changes and timeline entries.
- Compression explains item-level outcomes through outcome, item kind, item text, rule, threshold, rationale, and evidence.
