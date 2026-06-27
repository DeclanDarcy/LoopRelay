# Phase 5 - Decision Runtime

Goal: make Decision Sessions persistent Codex-backed runtime participants while preserving separation from operational execution.

## Implementation

- [ ] Add `IDecisionRuntime` and `DecisionRuntimeService` to `CommandCenter.DecisionSessions`.
- [ ] Decision Runtime uses `CommandCenter.Agents` with:
  - Decision role
  - read-only or zero-permission sandbox
  - high reasoning effort
  - repository-scoped context
- [ ] Decision session lifecycle:
  - `Created`
  - `Active`
  - `Waiting`
  - `Transferred`
  - `Retired`
  - `Failed`
- [ ] Implement decision conversation protocol:
  - consume operational context and current handoff
  - request next decisions
  - stream decision output
  - preserve warm conversation state
  - allow multiple turns
- [ ] Replace free-form decision output in runtime paths with structured `DecisionProposal` JSON.
- [ ] Parse structured output into existing Decisions domain services for validation, fallback, lifecycle, relationships, evidence, quality, governance, and persistence.
- [ ] Add human review flow:
  - proposal display
  - evidence and tradeoffs
  - editable decision content
  - submit/ratify
  - preserve human edits and revision history
- [ ] Repository Run starts and maintains the active Decision Session, tracks iterations, and coordinates submissions.
- [ ] Add decision streams for proposal output, lifecycle, reasoning-safe diagnostics, completion, failure, and review readiness.
- [ ] Add recovery for pending structured proposals and review state from durable records.
- [ ] Add architecture tests preventing Execution from referencing Decision Runtime and Decision Runtime from referencing operational Execution orchestration.
- [ ] Add generated contracts for decision runtime lifecycle, proposal, submission, revisions, streams, and metadata.

## Certification

- [ ] Decision Sessions are backed by live Agent Runtime processes.
- [ ] Decision Runtime never performs code, Git, commit, push, workflow, or planning operations.
- [ ] Every submitted decision passes Decisions domain validation.
- [ ] Human review is required before a decision advances the run.
- [ ] Deterministic decision services remain available as fallback.
