# Phase 8 - Contracts, Artifacts, and Provenance

Goal: harden the observable contracts and artifact semantics needed by the design. This is not a Repository Knowledge or adaptive intelligence milestone.

## Design note (m8)

The Plan Authoring -> Execution -> Decision loop (m2-m7) added its wire surface ad-hoc: ten endpoints, three SSE
streams (~9 event types each), the command-acknowledgement, structured-error, lifecycle-state, conversation, and
prompt-provenance shapes. Phase 8 brings that surface under the existing Contract Oracle harness (golden fixtures +
five verification families in `tests/CommandCenter.Backend.Tests`) without changing orchestration production behavior.

Key modelling decisions:
- **Stream events are merged logical events.** On the wire each frame is `id/event/data`; the UI reconstructs
  `{ type, ...data }`, which is what the TypeScript run-event variants describe. The golden for each stream is a
  representative trace of merged events; the SSE-frame split is governed separately as the stream *lifecycle* contract
  (ordering, terminal, failure, `Last-Event-ID` replay), per the docs' payload-vs-lifecycle separation.
- **Goldens are bound to the real producer.** Snapshot goldens serialize the production record types. Stream goldens
  are cross-checked against the real orchestrator: `AssertFaithfulToGolden` drives each stream through real scenarios
  and binds every emitted event's field-name set **and** JSON value-kind to the golden (so a producer retype, e.g.
  `bool -> string`, fails), with Transfer and Submit scenarios added so all nine decision events are producer-bound.
- **Consumers are verified, not redefined.** The existing `planning.ts` / `executionRun.ts` / `decisionRun.ts`
  run-event variants are bound to the goldens via the consumer-verification family (structural subset). No new TS type
  redefines a backend-owned shape.
- **Artifact durability = reconstruct-from-disk.** `FileSystemArtifactStore.WriteAsync` is not atomic; recoverability
  is achieved at the orchestrator layer (plan status from artifact existence, plan/handoff/decisions sequences
  re-derived from disk on restart, the one-way re-execution guard). The artifact-protocol tests certify this.
- **Provenance is certified, not changed.** Every turn already records all seven `PromptProvenance` fields; m8 adds a
  certification test across planning/execution/decision/transfer turns (including the intentionally-empty identity
  sets for the seed/reseed/delta turns).

The contract families were authored across three slices and hardened by a four-lens adversarial review (15 confirmed
findings remediated: fabricated golden values corrected to the producer's real strings, value-kind faithfulness binding,
the two comment-shadowed decision TS variants genuinely re-bound by hardening the consumer parser, and the
m8-introduced drift-engine duplication consolidated onto one shared engine).

## Implementation

- [x] Add or update backend contract identities for:
  - [x] plan status (`PlanStatus` + `PlanLifecycleState`; both wire strings pinned);
  - [x] plan write/revise/execute commands (`PlanRunAcknowledgement` + request-boundary);
  - [x] planning stream events (`plan-stream` golden + lifecycle);
  - [x] execution stream events used by this flow (`execution-stream` golden + lifecycle);
  - [x] decision stream events (`decision-stream` golden + lifecycle, incl. transfer + submit);
  - [x] decision submit (`DecisionSubmitRequest` + request-boundary);
  - [x] repository lifecycle state (`PlanLifecycleState` `PlanAuthoring`/`ExecutingPlan`);
  - [x] prompt provenance records (`PromptProvenance` Oracle golden + certification).
- [x] Generate or update TypeScript consumer types for the new contracts (existing run-event unions bound as verified
      consumers; generated-pipeline pilot remains `repository-dashboard`).
- [x] Add request-boundary tests for command payloads and structured errors.
- [x] Add stream contract tests for ordering, reconnect/replay behavior where supported, terminal events, and failure events.
- [x] Add artifact protocol tests for:
  - [x] `.agents/specs/roadmap.md`;
  - [x] `.agents/specs/s{n}.md`;
  - [x] `.agents/plan.md`;
  - [x] `.agents/operational_context.md`;
  - [x] `.agents/handoffs/handoff.000N.md`;
  - [x] `.agents/decisions/decisions.000N.md`;
  - [x] `.agents/operational_delta.md`.
- [x] Ensure every generated-prompt turn records prompt name, generated type, `SourceHash`, role, workflow phase, input artifact identities, and output artifact identities.
- [x] Keep decision output free text for the first implementation unless a canonical structured decision `.prompt` and contract are explicitly added.
- [x] Do not add knowledge graph, intelligence, query, or recommendation contracts in this phase.

## Certification

- [x] Contract oracle, consumer verification, generated artifact freshness, generated pipeline, and request-boundary tests pass for touched contract families.
- [x] Artifact writes are durable and recoverable (reconstruct-from-disk; certified by the artifact-protocol tests).
- [x] Prompt provenance is attached to planning, execution, decision, and transfer turns.
- [x] No UI type redefines backend-owned contract shapes.
