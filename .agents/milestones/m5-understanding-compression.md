# Milestone M5 - Understanding Compression

## Objective

Keep operational context high-signal across many revisions by preserving understanding while reducing historical detail.

M5 implements section- and tier-level compression over `OperationalContextDocument`. Decision-specific compression remains limited until decision analysis is added in M6.

## Backend Changes

- [ ] Add `IUnderstandingCompressionService`.
- [ ] Introduce information tiers:
  - [ ] `PermanentUnderstanding`: architecture, authority boundaries, fundamental constraints, stable decisions, system mental model.
  - [ ] `ActiveUnderstanding`: active risks, open questions, current tradeoffs, pending research.
  - [ ] `HistoricalUnderstanding`: resolved risks, completed investigations, retired tradeoffs.
  - [ ] `HistoricalNoise`: execution narratives, repeated status updates, superseded detail.
- [ ] Extend generation to classify proposal content by tier.
- [ ] Classify content using `OperationalContextDocument` sections and item kinds, not raw Markdown scanning.
- [ ] Extend proposal metadata with `OperationalContextCompressionSummary`:
  - [ ] Preserved item count.
  - [ ] Added item count.
  - [ ] Modified item count.
  - [ ] Removed item count.
  - [ ] Compressed item count.
  - [ ] Noise removed indicators.
  - [ ] Stable understanding retention warnings.
- [ ] Add compression rules:
  - [ ] Always preserve architecture, constraints, intent, authority boundaries, and current mental model.
  - [ ] Preserve risks, questions, tradeoffs, and research areas while active.
  - [ ] Compress resolved investigations into outcomes and current relevance.
  - [ ] Remove transient execution details and repeated information.
- [ ] Add quality warnings when:
  - [ ] Architecture disappears.
  - [ ] Constraints disappear.
  - [ ] Open questions disappear without resolution.
  - [ ] Active risks disappear without retirement.
  - [ ] Proposal growth indicates historical replay.
- [ ] Treat decision items conservatively in M5:
  - [ ] Preserve stable decision and rationale items already present in the document.
  - [ ] Do not attempt to decide which new decision artifacts deserve assimilation.
  - [ ] Defer decision taxonomy, rationale analysis, and decision-aware compression to M6.

## UI Changes

- [ ] Review panel shows:
  - [ ] Added understanding.
  - [ ] Removed understanding.
  - [ ] Compressed understanding.
  - [ ] Stable understanding retention warnings.
  - [ ] Revision summary.
- [ ] Do not expose low-level compression internals as controls.

## Tests

Add backend tests:

- [ ] Architecture survives multiple generated revisions.
- [ ] Constraints survive compression.
- [ ] Resolved questions are removed or moved to conclusions only when resolution evidence exists.
- [ ] Unresolved questions remain visible.
- [ ] Retired risks compress appropriately.
- [ ] Historical noise does not accumulate.
- [ ] Compression summary flags accidental loss of stable understanding.
- [ ] Decision-related content already present in operational context is preserved rather than aggressively compressed.

## Certification

Compression is certified when operational context can evolve through repeated proposal and promotion cycles while preserving architecture, constraints, stable decisions, open questions, and active risks without becoming a chronological archive.
