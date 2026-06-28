# Phase 9 - Product Integration

Goal: make the concrete Plan Authoring -> Execution -> Decision Loop feel like one repository workflow.

## Design note (m9)

Most of this milestone's surface was already scaffolded by the incremental m3-m7 UI slices: the workspace gate
(`App.tsx`: `selectedRepository && planStatus && (!planStatus.planExists || isAuthoringSessionActive)`), the single
in-place `PlanAuthoringScreen` lifecycle (the `isAuthoringSessionActive` latch + one-way `decisionPhase`, with
`onExecuted` as the sole navigation exit), the action gating, the clipboard Copy, and the continuation loop. Phase 9
therefore unified and hardened that surface rather than building it anew. It is **UI-only** (`src/CommandCenter.UI`):
no backend, prompt, Rust, or wire/contract change, and the three m8-frozen run-event type files
(`types/planning.ts`, `types/executionRun.ts`, `types/decisionRun.ts`) were left byte-unchanged so the m8 contract
goldens / consumer-verification / SHA-256 freshness stay green.

Key decisions:
- **Reusable stream render layer, distinct sources.** Extracted presentational primitives under `features/streams/`
  (`StreamOutputPanel`, `PhaseTimeline` + `CheckGlyph`, `StreamFailurePanel`, shared `StreamPrimitives.css` with
  neutral `cc-stream-*` classes). The three views render them; the three sources
  (`usePlanStream`/`useExecutionStream`/`useDecisionStream` + their reducers) stay fully separate. ~150 duplicated
  timeline CSS lines were removed.
- **Diagnostics are secondary.** The m5 sandbox/approvals/seeded frame, the router transfer indicator, and raw
  handoff/decision filesystem paths moved into a closed-by-default `<details>` "Diagnostics" disclosure; primary
  timeline steps were relabelled to product vocabulary (Repository / Plan / Execution / Decision / Status). No prompt
  names, source hashes, or session ids appear in the primary surface.
- **Execute is gated on the backend-verified plan.** `canExecute` now requires `planStatus.planExists` (not just the
  in-memory streamed plan), refreshed in place when the plan stream reaches `PlanReady`. Because the backend writes
  `plan.md` before emitting `completed`, the refresh enables Execute promptly; the `isAuthoringSessionActive` latch
  keeps the screen mounted across the refresh (no m3 unmount). The refresh retries on transient status-fetch failure.
- **Stream robustness.** `EventSource.onerror` (previously a no-op) now surfaces a "Reconnecting…" pill on transient
  drops and, via a bounded reconnect window (armed once on `CONNECTING`, cleared on any frame, escalated on expiry to
  `close()` + a recoverable `StreamFailurePanel`), a real connection-lost surface for mid-stream drops that the
  browser would otherwise retry forever. A cancelled/disposed turn (graceful server close) escalates through the same
  window - no invented backend event. Replayed frames are deduped by SSE `id` at the subscriber layer; `handoffPath`
  is empty-guarded.

Built by a frontend-design+impeccable+superpowers UI subagent (TDD), then a five-lens adversarial review (21 findings,
8 confirmed after default-refute verification) whose remediations are folded in: plan-stream transport-failure now
rendered; bounded-reconnect-window so the failure surface is reachable for mid-stream drops; the App-level happy-path
E2E de-flaked (await Execute enablement) and strengthened with DOM-node identity assertions; the decision primary-path
scrub finished; an onerror-through-hooks integration test added. Deferred (pre-existing, out of scope): the decision
`route`/`transferred`/`numberedPath` fields are not parsed by the real SSE subscriber (mock-only until the backend
producer is wired) - noted in code.

## Implementation

- [x] Integrate `PlanAuthoringScreen` into the selected-repository workspace when `plan/status.planExists == false`.
- [x] Keep the same screen active through planning, revision, execution startup, decision review, submit, continuation, and next decision.
- [x] Add reusable stream views for planning, execution, and decision output while preserving distinct stream sources.
- [x] Show implementation diagnostics only in secondary details. Primary UI terminology is Repository, Plan, Execution, Decision, and Status.
- [x] Disable actions based on backend-owned state and local input completeness:
  - [x] Write Plan requires Roadmap text;
  - [x] Revise Plan requires Feedback text and an open planning session;
  - [x] Execute Plan requires verified `.agents/plan.md`;
  - [x] Submit requires editable decisions from a completed decision turn.
- [x] Add clipboard support for the plan Copy icon through Tauri clipboard or `navigator.clipboard`.
- [x] Present decision output as read-only while streaming and editable only after completion.
- [x] Keep prompt names, source hashes, session ids, and router mechanics out of the primary workflow unless shown in diagnostics.
- [x] Add E2E coverage for the full happy path with mocked or test-provider streams.

## Certification

- [x] Users can author, revise, execute, review decisions, submit, and continue without navigating away from the workflow.
- [x] UI never composes prompt text or infers prompt selection.
- [x] UI handles stream failure, retry/reconnect if supported, cancelled turns, and missing artifacts.
- [x] Accessibility and layout checks cover the Roadmap/Specs editor, stream regions, editable decisions, and action buttons.
