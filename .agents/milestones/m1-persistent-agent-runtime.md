# Phase 1 - Persistent Agent Runtime

Goal: make Codex-backed sessions interactive and reusable across multiple turns.

## Implementation

- [ ] Add `IAgentProcess` with operations for open, submit turn, stream output, observe turn completion, cancel, interrupt, dispose, and query health.
- [ ] Add `IAgentRuntime` and an implementation that owns live process creation, stream subscription, prompt queueing, cancellation, interruption, disposal, and shutdown.
- [ ] Add an `AgentSessionRegistry` keyed by `SessionIdentity`, with ownership, lookup, enumeration, removal, health, and disposal.
- [ ] Add session stream support:
  - turn start
  - output
  - diagnostics
  - completion
  - failure
  - cancellation
  - reconnect/replay metadata
- [ ] Replace Execution's one-shot provider implementation with a compatibility adapter:
  - open operational session
  - submit one prompt
  - stream output through existing monitoring
  - dispose after completion
  - preserve current API behavior
- [ ] Improve process supervision:
  - retain reader/exit tasks
  - observe task failures
  - replace fixed process-start delay with deterministic exit probing
  - detect hung sessions, protocol violations, unexpected exits, cancellation, and timeout.
- [ ] Add role-aware sandbox and effort handling to `AgentSessionSpec`.
- [ ] Add prompt turn input support to Agent Runtime:
  - rendered prompt text
  - prompt name
  - generated prompt type
  - `SourceHash`
  - session role
  - workflow phase
  - input artifact identities
- [ ] Keep Agent Runtime prompt-neutral. It accepts rendered prompts and provenance, but never chooses templates, edits prompt text, formats domain payloads, or owns prompt semantics.
- [ ] Add runtime diagnostics and metrics:
  - session count
  - prompt count
  - prompt identities and source hashes by turn
  - turn count
  - lifetime
  - current state
  - failures
  - cancellation/disposal reason
- [ ] Add generated contracts and UI types for runtime status, session diagnostics, stream event payloads, prompt provenance, and turn completion where they cross backend boundaries.

## Certification

- [ ] Multiple prompts execute in the same live agent process.
- [ ] Streaming continues across turns.
- [ ] Existing one-shot execution remains behaviorally equivalent.
- [ ] Session cleanup occurs on cancellation, failure, disposal, and application shutdown.
- [ ] Concurrent sessions are isolated.
- [ ] A persistent session can execute multiple generated-prompt turns while retaining distinct prompt provenance for each turn.
- [ ] Agent Runtime tests prove prompts are opaque runtime inputs and no template selection happens inside the runtime layer.
- [ ] Runtime tests cover long output, rapid prompts, cancellation, disposal, failure recovery, and registry ownership.
