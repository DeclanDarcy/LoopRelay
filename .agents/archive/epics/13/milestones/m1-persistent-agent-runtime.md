# Phase 1 - Persistent Agent Runtime

Goal: make Codex-backed sessions reusable across multiple turns so planning revisions and decision reuse can run in a warm process.

## Implementation

- [ ] Validate the Codex protocol that can support held-open interaction. Preferred route: Codex app-server, `codex proto`, or MCP/server session owned by `CommandCenter.Agents`.
- [ ] Add `IAgentProcess` operations for open, `WritePromptAsync(text)`, per-turn stream subscription, turn completion, cancellation, interruption, health, and disposal.
- [ ] Ensure persistent mode does not close stdin after the first prompt.
- [ ] Add an `IAgentRuntime` implementation that owns process creation, prompt queueing, stream fan-out, turn state, cancellation, disposal, and shutdown.
- [ ] Add an `AgentSessionRegistry` keyed by session id and repository id.
- [ ] Support both lifetimes:
  - [ ] one-shot process: prompt in, stream out, exit;
  - [ ] held-open process: sequential prompts, per-turn completion, process retained.
- [ ] Add role-aware sandbox and effort arguments to process launch:
  - [ ] Operational: workspace-write, approvals off;
  - [ ] Decision: read-only, approvals never, no MCP/tools where supported;
  - [ ] ExtraHigh and Medium reasoning effort, with exact Codex values verified.
- [ ] Preserve existing one-shot execution through a compatibility adapter over the new runtime.
- [ ] Add transcript and token accounting hooks. Deterministic `(len + 3) / 4` estimates may remain as fallback until real accounting is available.
- [ ] Document the governed fallback if persistence is infeasible: planning revision reruns one-shot with current plan plus feedback, and decision routing becomes transfer-only.

## Certification

- [ ] Two or more prompts can execute in the same live planning process.
- [ ] Two or more decision prompts can execute in the same live Decision process.
- [ ] Streaming emits ordered output and explicit turn completion for each prompt.
- [ ] One-shot compatibility preserves existing provider behavior.
- [ ] Session cleanup occurs on cancellation, failure, disposal, repository shutdown, and app shutdown.
- [ ] Tests cover long output, rapid prompts, cancellation, unexpected exit, timeout, and concurrent repository isolation.
