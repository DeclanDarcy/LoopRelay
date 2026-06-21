# M0 Execution Context Preview Inventory

## Scope

Audits the remaining unextracted execution context preview rendering in `src/CommandCenter.UI/src/App.tsx` after the summary rows, artifact list, missing optional artifact list, validation list, repository snapshot panel, artifact diagnostics list, and artifact content previews were extracted.

This is an inventory only. It classifies extraction candidates for Milestone 0.5 without authorizing workflow, readiness, validation, or diagnostic authority to move into React components.

## Remaining Regions

| Region | Current Lines | Presentation-Only Status | Interpretation Risk | Extraction Candidacy |
| --- | --- | --- | --- | --- |
| Validation list | Extracted to `ExecutionContextValidationList` | Presentation-only. It accepts `validationErrors: string[]`, renders the existing empty fallback, and renders backend-provided messages in order. | Medium if future edits add severity, grouping, launch impact, or readiness interpretation. | Complete for M0.5; do not expand beyond `string[] -> list/fallback`. |
| Repository snapshot summary | Extracted to `ExecutionRepositorySnapshotPanel` | Presentation-only. It accepts `ExecutionRepositorySnapshot | null`, renders nothing for missing snapshots, preserves branch fallback, clean/dirty label, captured timestamp formatting, and `GitPathBucket` usage. | Medium if future edits add readiness, health, risk, or blocking language. | Complete for M0.5; keep constrained to direct snapshot rendering. |
| Artifact size diagnostics | Extracted to `ExecutionContextArtifactDiagnosticsList` | Presentation-only. It accepts `ExecutionContextArtifactDiagnostic[]`, preserves backend-provided order, renders relative paths and byte counts, preserves the existing ` / hard limit` and ` / warning` suffixes, and adds no empty fallback. | High if future edits add severity ranking, grouping, recommendations, readiness, or blocking interpretation. | Complete for M0.5; keep constrained to direct diagnostic rendering. |
| Artifact content previews | Extracted to `ExecutionContextArtifactContentPreviews` | Presentation-only. It accepts `ExecutionContextArtifact[]`, preserves backend-provided order, summary labels, `OperationalContext` default-open behavior, shared markdown rendering, and the existing `Empty artifact.` fallback. | Medium if future edits add sufficiency, completeness, priority, recommendation, or launch-readiness interpretation. | Complete for M0.5; keep constrained to direct artifact content rendering. |

## Findings

- The validation list extraction is complete and remains constrained to `string[] -> list or "No validation errors"`.
- Repository snapshot extraction is complete and remains constrained to direct projection rendering with existing `GitPathBucket` labels/fallbacks.
- Artifact diagnostics extraction is complete and remains constrained to backend-provided order, byte counts, and the existing threshold suffix labels.
- Artifact content previews are complete and remain constrained to direct artifact content rendering.
- No remaining region should receive `canStartExecution`, blocked reason calculation, launch readiness, or threshold interpretation logic as part of a presentation extraction.

## Recommended Order

1. Stop execution-context preview extraction for M0.5 unless a new audit identifies another clearly presentation-only surface.
2. Treat remaining execution-context regions in `App.tsx` as workflow coordination, readiness display composition, or broader shell rendering until proven otherwise.
