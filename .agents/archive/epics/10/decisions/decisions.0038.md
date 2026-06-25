# Decisions

## Newly Authorized

- Accept the completed Milestone 5 handoff processing transparency slice as architecturally correct.
- Preserve the authority boundary: Execution owns handoff processing facts, Workflow observes them, and UI renders them.
- Treat `NoHandoffProcessingRecord` as the correct compatibility state for older sessions without recorded handoff processing evidence.
- Continue Milestone 5 next with semantic execution event categories and consequence text.
- Semantic execution events should cover launch, provider, monitoring, recovery, handoff, git, failure, and related state change or consequence.
- After semantic execution event transparency, continue with execution-generated versus pre-existing git change classification.
- Git change-origin classification must be backend-owned and must not be inferred in UI from path names or timestamps.
- Add UI bulk actions to select execution-generated changes and deselect pre-existing changes once backend-owned classification is available.
- After event semantics and git change-origin classification, proceed to Milestone 5 exit audit and closure.
