# Phase 6 - Continuous Repository Conversation Loop

Goal: compose operational turns, decision turns, and human review into one continuous governed repository conversation. If Repository Run remains independent, it owns the conversation progression; if Repository Run collapses into Repository Runtime, the same conversation model moves intact under Repository Runtime.

## Implementation

- [ ] Add a unified turn model:
  - `OperationalTurn`
  - `DecisionTurn`
  - `HumanReviewTurn`
- [ ] Every turn has:
  - identity
  - owner
  - start timestamp
  - stream references
  - completion condition
  - transition result
  - diagnostics
- [ ] Add conversation state to the accepted progression owner, initially Repository Run:
  - current turn
  - previous turn
  - iteration
  - current owner
  - conversation history
  - checkpoint references
- [ ] Implement deterministic transitions:
  - operational execution produces handoff
  - handoff starts decision turn
  - structured decision proposal starts human review
  - human submission advances continuation
  - continuation starts next operational turn or completes the run
- [ ] Use `CommandCenter.Workflow` state machine and continuation services as the semantic workflow/progression authority. Repository Runtime coordinates only.
- [ ] Persist conversation progress and checkpoints in the run journal.
- [ ] Add continuous conversation stream that merges operational, decision, human review, lifecycle, and health events into one repository-centric timeline.
- [ ] Add UI conversation timeline that presents planning, execution, decisions, and continuation as one flow while preserving turn identity and authority.
- [ ] Add generated contracts for conversation state, turns, iteration, lifecycle, stream events, and timeline projections.

## Certification

- [ ] Runs advance through repeated operational/decision/human turns without hidden transitions.
- [ ] Human governance happens only at decision review/submit boundaries.
- [ ] Recovery resumes from durable conversation state.
- [ ] The UI presents one continuous repository conversation, not fragmented feature streams.
