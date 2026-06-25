# Decisions

## Newly Authorized

- Treat the revision-history-backed `OperationalEvolutionTimelineEntry` projection as a significant Milestone 7 increment that closes the gap between summary-level operational evolution and revision-history-backed evolution.
- Continue the authority split where `UnderstandingDiffService` owns semantic change detection, `ContinuityDiagnosticsService` enriches semantic facts with revision-history context, and React renders typed timeline projections without interpreting operational-context meaning.
- Keep normalized item-state preservation as an intentionally conservative interim identity fallback until durable cross-revision operational-context item identifiers exist.
- Make the remaining Milestone 7 work reconciliation-focused rather than broad feature expansion.
- Reconcile `OperationalContextProposalComparison` and `OperationalContextSemanticChangeList` so they consistently consume the richer typed semantic model.
- Review `Merged` and item-level `NoiseRemoved` compression outcomes against real backend operations; model them explicitly only if genuine backend semantics exist, otherwise leave them intentionally absent.
- Before formal Milestone 7 exit, perform a projection-gap audit covering backend projection consumers, absence of UI semantic reconstruction, retirement of compatibility-string surfaces where typed projections exist, and field-to-surface mapping against exit criteria.
