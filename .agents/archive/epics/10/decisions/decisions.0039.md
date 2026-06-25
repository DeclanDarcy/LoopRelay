# Decisions

## Newly Authorized

- Accept the completed Milestone 5 semantic execution event slice as architecturally correct.
- Preserve the semantic event authority chain: Execution service -> semantic event projection -> transport -> React grouping/rendering.
- Keep execution event `category` and `consequence` as distinct backend-authored fields.
- Continue Milestone 5 with execution-generated versus pre-existing Git change classification.
- Git change classification must be execution-owned and deterministic.
- Project per-path Git change classification as `ExecutionGenerated` versus `PreExisting`.
- Include classification basis where applicable.
- Include eligibility for execution-focused bulk actions in the backend-owned projection.
- Update `GitPathBucket` and `GitWorkflowEvidence` to consume backend classification directly.
- Add backend-driven bulk actions to select execution-generated changes and deselect pre-existing changes.
- Do not rely on UI path heuristics, timestamps, or client inference for Git change classification.
- Git operations should emit execution events only when they represent execution lifecycle transitions.
- Appropriate Git execution events include commit preparation created, commit preparation invalidated, commit succeeded, push attempted, push succeeded, push failed, retry state established, and retry completed.
- Do not emit execution events that merely restate static repository state, such as modified file counts or selected file counts.
- Keep static Git state in projections such as Git eligibility or Git workflow evidence, not in the execution event stream.
