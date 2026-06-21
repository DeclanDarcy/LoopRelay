# M0 Execution Context Preview Inventory

## Scope

Audits the remaining unextracted execution context preview rendering in `src/CommandCenter.UI/src/App.tsx` after the summary rows, artifact list, and missing optional artifact list were extracted.

This is an inventory only. It classifies extraction candidates for Milestone 0.5 without authorizing workflow, readiness, validation, or diagnostic authority to move into React components.

## Remaining Regions

| Region | Current Lines | Presentation-Only Status | Interpretation Risk | Extraction Candidacy |
| --- | --- | --- | --- | --- |
| Validation list | `App.tsx` execution context preview validation column | Mostly presentation-only if it only renders backend-provided validation errors in order plus the existing empty text. | Medium. Validation text can imply launch blocking, severity, or required action if labels or grouping are added. | Good candidate for a narrow `ExecutionContextValidationList` that accepts `validationErrors: string[]` only. |
| Repository snapshot summary | `App.tsx` repository snapshot block | Mixed. Branch, captured time, and path buckets are projection rendering; clean/dirty display is still a direct backend-provided dirty-state label. | Medium. Snapshot UI can become a health assessment if it starts explaining whether dirty state should block execution. | Candidate only if the component receives `repositorySnapshot` and preserves current labels/fallbacks without adding readiness language. |
| Artifact size diagnostics | `App.tsx` artifact diagnostics block | Mixed. It renders backend-provided byte counts and threshold flags. | High. `hard limit` and `warning` labels are diagnostic authority-adjacent and can influence launch/readiness interpretation. | Extract only with characterization that preserves exact labels, ordering, and absence of additional recommendations. |
| Artifact content previews | `App.tsx` artifact content details block | Mostly presentation-only if it maps artifact content to markdown preview and preserves the current empty fallback. | Medium. Default-open behavior for `OperationalContext` encodes current review emphasis, but not workflow authority. | Candidate after validation and snapshot only; keep it as `content -> preview`, not sufficiency or completeness assessment. |

## Findings

- The safest next extraction is the validation list because it can be constrained to `string[] -> list or "No validation errors"` and covered by simple characterization tests.
- Repository snapshot extraction is reasonable after validation, but it should keep using existing `GitPathBucket` and avoid deriving any execution readiness from clean or dirty state.
- Artifact diagnostics should be treated as the highest-risk remaining extraction because threshold labels sit close to launch-blocking semantics.
- Artifact content previews are low workflow risk but larger surface area because they rely on markdown rendering, empty fallback behavior, and default-open artifact role behavior.
- No remaining region should receive `canStartExecution`, blocked reason calculation, launch readiness, or threshold interpretation logic as part of a presentation extraction.

## Recommended Order

1. Extract `ExecutionContextValidationList` with characterization coverage for empty state, provided order, and duplicate-key risk if duplicate messages are possible.
2. Extract repository snapshot rendering only as a projection display component.
3. Reassess artifact diagnostics before extraction; if extracted, preserve exact current labels and ordering.
4. Extract artifact content previews last, using existing markdown characterization as supporting coverage.
