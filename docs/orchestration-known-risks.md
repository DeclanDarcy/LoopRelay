# Orchestration Known Risks

This inventory records accepted migration risks that must be distinguished from canonical refactor regressions.

## Storage And Authority Risks

- Stale filesystem exports can conflict with canonical SQLite state.
- Mixed filesystem and SQLite repositories require explicit authority reporting before mutation.
- Unsupported schema versions must block rather than silently downgrade or repair.
- Partial workflow transactions can leave repository evidence requiring explicit retry or repair.
- Verification must not mutate filesystem exports or SQLite state.

## Archive And Completion Risks

- Partial archive materialization can leave completion evidence split across live artifacts and archive records.
- Reruns after live artifact archival can fail if product references are not resolvable from archive evidence.
- Archive index collisions can corrupt completion discovery.
- Roadmap completion-context updates can be partially completed after certification.
- Completion closure currently spans roadmap and execute authorities and must be made singular.

## Permission And Publication Risks

- Permission bypass can occur if scoped artifact operations are replaced by generic prompt execution.
- `.agents` publication failures can leave local artifacts ahead of published evidence.
- Parent gitlink recording can be missed after `.agents` publication.
- Commit evaluation can mistake bookkeeping, milestone checkbox progress, or publication-only changes for implementation progress.

## Recovery Risks

- Prompt output can exist without validated products.
- Product files can exist without durable transition completion.
- Cancellation after prompt execution but before effects can leave recoverable evidence that must not be discarded.
- Cancellation during effects can leave partial side effects that must be surfaced as partial state.
- Legacy decision-session resume files can conflict with SQLite resume rows.

## EvalRoadmap Risks

- Eval analysis artifacts can be mistaken for Plan entry products if product validation is skipped.
- `CreateNextEpicImplementationSpec` must write the canonical prepared epic at `.agents/epic.md`, not a milestone spec artifact.
- EvalRoadmap and TraditionalRoadmap must converge on the same `PreparedEpic` and `MilestoneSpecificationSet` products so Plan remains producer-agnostic.
