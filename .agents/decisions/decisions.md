# Decisions

## Newly Authorized Decisions

- M2A is accepted as complete from a certification perspective.
- The next authorized slice is M2B.1: Execution Prompt Construction.
- Do not implement Codex launch immediately.
- Add `ExecutionPrompt` and `ExecutionPromptMetadata`.
- Add `IExecutionPromptBuilder` and `ExecutionPromptBuilder`.
- Prompt construction must be a deterministic transformation from `ExecutionContext` to `ExecutionPrompt`.
- Providers should receive `ExecutionPrompt`, not `ExecutionContext`.
- Prompt construction must include repository path, selected milestone, plan, milestone, current handoff when present, current decisions when present, Git branch, Git status summary, and dirty-state diagnostics.
- Prompt construction must include instructions to work only in the repository, update `.agents/handoffs/handoff.md`, avoid committing, avoid pushing, and leave acceptance to Command Center.
- Prompt construction must exclude provider-specific formatting, executable arguments, and process launch behavior.
- Prompt certification must verify required inputs, optional artifact handling, dirty repository inclusion, and stable output with no timestamps, GUIDs, or ordering drift.
- Before provider launch, add an invariant test that providers receive an `ExecutionPrompt` and do not read repository artifacts directly for prompt construction.
- M2B.2 is deferred until prompt construction is certified.
- M2B.2 should introduce only Codex provider launch concerns: executable resolution, process launch, PID capture, start failure handling, and orphan detection metadata.
- Continue avoiding stdout capture, stderr capture, event streams, and monitoring until M3.

## Next Authorized Slice

- Proceed into M2B.1: deterministic execution prompt construction and certification tests.
