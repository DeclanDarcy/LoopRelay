# M0 Execution Context Preview Inventory

## Scope

Audits the remaining unextracted execution context preview rendering in `src/CommandCenter.UI/src/App.tsx` after the summary rows, artifact list, missing optional artifact list, validation list, and repository snapshot panel were extracted.

This is an inventory only. It classifies extraction candidates for Milestone 0.5 without authorizing workflow, readiness, validation, or diagnostic authority to move into React components.

## Remaining Regions

| Region | Current Lines | Presentation-Only Status | Interpretation Risk | Extraction Candidacy |
| --- | --- | --- | --- | --- |
| Validation list | Extracted to `ExecutionContextValidationList` | Presentation-only. It accepts `validationErrors: string[]`, renders the existing empty fallback, and renders backend-provided messages in order. | Medium if future edits add severity, grouping, launch impact, or readiness interpretation. | Complete for M0.5; do not expand beyond `string[] -> list/fallback`. |
| Repository snapshot summary | Extracted to `ExecutionRepositorySnapshotPanel` | Presentation-only. It accepts `ExecutionRepositorySnapshot | null`, renders nothing for missing snapshots, preserves branch fallback, clean/dirty label, captured timestamp formatting, and `GitPathBucket` usage. | Medium if future edits add readiness, health, risk, or blocking language. | Complete for M0.5; keep constrained to direct snapshot rendering. |
| Artifact size diagnostics | `App.tsx` artifact diagnostics block around current lines 2219-2233 | Mixed. It renders backend-provided byte counts and threshold flags. | High. `hard limit` and `warning` labels are diagnostic authority-adjacent and can influence launch/readiness interpretation. | Extract only with characterization that preserves exact labels, ordering, and absence of additional recommendations. |
| Artifact content previews | `App.tsx` artifact content details block around current lines 2235-2247 | Mostly presentation-only if it maps artifact content to markdown preview and preserves the current empty fallback. | Medium. Default-open behavior for `OperationalContext` encodes current review emphasis, but not workflow authority. | Candidate after snapshot only; keep it as `content -> preview`, not sufficiency or completeness assessment. |

## Findings

- The validation list extraction is complete and remains constrained to `string[] -> list or "No validation errors"`.
- Repository snapshot extraction is complete and remains constrained to direct projection rendering with existing `GitPathBucket` labels/fallbacks.
- Artifact diagnostics should be treated as the highest-risk remaining extraction because threshold labels sit close to launch-blocking semantics.
- Artifact content previews are low workflow risk but larger surface area because they rely on markdown rendering, empty fallback behavior, and default-open artifact role behavior.
- No remaining region should receive `canStartExecution`, blocked reason calculation, launch readiness, or threshold interpretation logic as part of a presentation extraction.

## Recommended Order

1. Reassess artifact diagnostics before extraction; if extracted, preserve exact labels, ordering, threshold displays, and absence of recommendations.
2. Extract artifact content previews last, using existing markdown characterization as supporting coverage.
3. After those two regions, stop M0.5 execution-context preview extraction unless a new audit identifies another clearly presentation-only surface.
