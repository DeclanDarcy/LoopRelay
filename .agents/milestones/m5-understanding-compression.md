# Milestone M5 - Understanding Compression

## Objective

Keep operational context high-signal across many revisions by preserving understanding while reducing historical detail.

M5 implements section- and tier-level compression over `OperationalContextDocument`. Decision-specific compression remains limited until decision analysis is added in M6.

## Backend Changes

- [x] Add `IUnderstandingCompressionService`.
- [x] Introduce information tiers:
  - [x] `PermanentUnderstanding`: architecture, authority boundaries, fundamental constraints, stable decisions, system mental model.
  - [x] `ActiveUnderstanding`: active risks, open questions, current tradeoffs, pending research.
  - [x] `HistoricalUnderstanding`: resolved risks, completed investigations, retired tradeoffs.
  - [x] `HistoricalNoise`: execution narratives, repeated status updates, superseded detail.
- [x] Extend generation to classify proposal content by tier.
- [x] Classify content using `OperationalContextDocument` sections and item kinds, not raw Markdown scanning.
- [x] Extend proposal metadata with `OperationalContextCompressionSummary`:
  - [x] Preserved item count.
  - [x] Added item count.
  - [x] Modified item count.
  - [x] Removed item count.
  - [x] Compressed item count.
  - [x] Noise removed indicators.
  - [x] Stable understanding retention warnings.
- [ ] Add compression rules:
  - [x] Always preserve architecture, constraints, intent, authority boundaries, and current mental model.
  - [x] Preserve risks, questions, tradeoffs, and research areas while active.
  - [ ] Compress resolved investigations into outcomes and current relevance.
  - [x] Remove transient execution details and repeated information.
- [x] Add quality warnings when:
  - [x] Architecture disappears.
  - [x] Constraints disappear.
  - [x] Open questions disappear without resolution.
  - [x] Active risks disappear without retirement.
  - [x] Proposal growth indicates historical replay.
- [x] Treat decision items conservatively in M5:
  - [x] Preserve stable decision and rationale items already present in the document.
  - [x] Do not attempt to decide which new decision artifacts deserve assimilation.
  - [x] Defer decision taxonomy, rationale analysis, and decision-aware compression to M6.

## UI Changes

- [ ] Review panel shows:
  - [x] Added understanding.
  - [x] Removed understanding.
  - [x] Compressed understanding.
  - [x] Stable understanding retention warnings.
  - [ ] Revision summary.
- [x] Do not expose low-level compression internals as controls.

## Tests

Add backend tests:

- [ ] Architecture survives multiple generated revisions.
- [x] Constraints survive compression.
- [ ] Resolved questions are removed or moved to conclusions only when resolution evidence exists.
- [x] Unresolved questions remain visible.
- [ ] Retired risks compress appropriately.
- [x] Historical noise does not accumulate.
- [x] Compression summary flags accidental loss of stable understanding.
- [x] Decision-related content already present in operational context is preserved rather than aggressively compressed.

## Certification

Compression is certified when operational context can evolve through repeated proposal and promotion cycles while preserving architecture, constraints, stable decisions, open questions, and active risks without becoming a chronological archive.
