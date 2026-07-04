# m3 Plan Authoring UI — Design

Date: 2026-06-27. Status: approved (no human available; decisions reasoned and recorded per the milestone brief).

## Intent

A focused authoring surface where a developer drafts a epic (plus optional specs), watches an
implementation plan stream back from the backend agent, then revises it with feedback or executes it.
This is the first screen a repository shows when no plan exists yet. The screen is a *drafting table*,
not a dashboard: a single editorial column whose top-to-bottom order mirrors the authoring lifecycle.

## Hard constraints (from the brief — restated to bind the implementation)

- Frontend NEVER composes prompt text or selects a prompt class. It sends only `{ epic, specs[], newCodebase }`
  on write and `{ feedback }` on revise. The backend renders prompts.
- Changes confined to `src/CommandCenter.UI/**` and `src/CommandCenter.Shell/src/main.rs`.
- Mirror existing conventions: `invokeCommand` boundary, EventSource SSE pattern (`useExecutionEvents`),
  hand-written types, `devTauriMock` invoke switch, vitest characterization style, `cc-*`/global CSS, design tokens.

## Wire contract (already served by the backend; do not change)

Repository-scoped, `{id}` GUID "D" format.

- `GET  /plan/status` → `{ planExists: boolean, state: 'PlanAuthoring' | 'ExecutingPlan' }`
- `POST /plan/write`  `{ epic, specs[], newCodebase }` → 202 `{ phase: 'WritePlan' }` (404/400 empty epic/409 running)
- `POST /plan/revise` `{ feedback }` → 202 `{ phase: 'RevisePlan' }` (404/400 empty feedback/409 no warm session/running)
- `POST /plan/execute` (no body) → 202 `{ phase: 'ExecutePlan' }` (404/409 no plan/running)
- `GET  /plan/stream` → SSE via EventSource directly against backend URL. Events:
  - `turn-started` `{ phase: 'WritePlan' | 'RevisePlan' }`
  - `delta` `{ text }`
  - `completed` `{ plan, promptTokens, outputTokens }`
  - `failed` `{ reason, detail? }`

## State machine

`Authoring → Planning → PlanReady → Revising → PlanReady → Executing`

Transitions are driven by SSE events and command responses (not by optimistic guessing):
- `Authoring`: inputs editable; Write Plan enabled iff epic has non-whitespace text.
- POST `write` (202) → optimistic `Planning` (so the UI responds even before the first event); `turn-started{WritePlan}` confirms.
- `delta` events accumulate into a live streamed buffer (read-only).
- `completed` → `PlanReady` (store `plan`, token counts). Copy button + Revise + Execute become available.
- In `PlanReady`, POST `revise` (202) → `Revising`; `turn-started{RevisePlan}` confirms; deltas re-stream; `completed` → `PlanReady`.
- In `PlanReady`, POST `execute` (202) → `Executing` (terminal for m3; no decision UI).
- `failed` event (from any live turn) → `Failed` view showing `reason` (+ `detail`), with a path back to `Authoring`.

The machine is implemented as a pure reducer `planAuthoringReducer(state, action)` (TDD'd) consuming
two action sources: SSE events (`turn-started`/`delta`/`completed`/`failed`) and command lifecycle
(`write-submitted`/`revise-submitted`/`execute-submitted`/`command-failed`). Keeping it pure makes it
unit-testable without React or a backend.

## Component / module layout (mirrors existing feature folders)

- `src/api/planning.ts` — `getPlanStatus(repositoryId)`, `writePlan(repositoryId, {epic,specs,newCodebase})`,
  `revisePlan(repositoryId, feedback)`, `executePlan(repositoryId)` via `invokeCommand`; plus
  `subscribeToPlanEvents(backendUrl, repositoryId, onEvent)` mirroring `subscribeToExecutionEvents` (EventSource,
  `addEventListener` per event type, returns `{ close }`).
- `src/types/planning.ts` — extend with `PlanStatus`, `PlanPhase`, `PlanTurnPhase`, the four SSE event payload
  types + a discriminated `PlanStreamEvent`, and `PlanAuthoringState`/`PlanAuthoringStatus`.
- `src/hooks/usePlanStatus.ts` — fetch `getPlanStatus`, expose `{ data, isLoading, error, refresh }` (mirrors `useRepositories`).
- `src/hooks/usePlanStream.ts` — own the reducer + SSE subscription (mirrors `useExecutionEvents`: load backend URL,
  subscribe keyed on `[backendUrl, repositoryId]`; in mock mode short-circuit EventSource and consume the
  mock plan-stream bridge). Exposes machine state + `submitWrite/submitRevise/submitExecute` actions.
- `src/features/planning/PlanAuthoringScreen.tsx` — composition root; owns epic/specs/feedback local input state.
- `src/features/planning/EpicField.tsx`, `SpecList.tsx`, `PlanStreamView.tsx`, `RenderedPlanView.tsx`,
  `PlanFailureNotice.tsx` — small focused units.
- `src/features/planning/planAuthoringMachine.ts` — pure reducer + initial state (unit-tested).
- `src/features/planning/PlanAuthoring.css` — `cc-plan-*` classes built only on existing tokens.

## Visual direction (within the committed dark token system; identity preserved)

- Layout: single editorial column, ~760px max, centered — a "sheet" distinct from the multi-panel dashboards.
- Sections divided by hairline `--border-subtle` rules with small uppercase labels (Epic / Specifications / Plan).
  No numbered markers (the form is not a fixed sequence).
- Type: UI in `--font-body`; the streamed plan and rendered plan in `--font-mono-small` (generated-source feel).
  No markdown library exists in the repo, so the plan renders as preformatted monospace text in a `<pre>` —
  faithful, legible, and zero new dependencies.
- Signature: the streaming Plan view — a monospace document with a blinking ink-caret at the tail while a turn
  is live, and a phase pill ("Writing plan…" / "Revising plan…") that pulses. On `completed` the caret resolves
  and an icon-only copy button (aria-label "Copy plan") fades in at the document corner with a transient "Copied" state.
- Accent used once: phase pill + caret use `--accent-fg`/`--accent-emphasis`. Failure uses `--status-danger-*`.
- States: empty (no plan yet — invites authoring), loading (status fetch), live streaming, completed, failed.
- Accessibility: stream region is an `aria-live="polite"` log; copy button has an accessible name; visible focus;
  `prefers-reduced-motion` disables the caret blink and pill pulse (static dot instead).

## App gate

In `App.tsx`, for the selected repository fetch `usePlanStatus(repositoryId)`. While loading, keep current behavior;
when `planExists === false`, render `<PlanAuthoringScreen repositoryId=… onPlanReady=…/>` in place of the workspace
Panel; otherwise render the existing dashboard. On `completed`/`execute`, refresh plan status so the dashboard
reappears when appropriate.

## Rust shell

Add `get_plan_status` (GET), `write_plan` / `revise_plan` / `execute_plan` (POST) as `reqwest::blocking`
proxies returning `serde_json::Value` passthrough (mirroring `backend_get_value` / `backend_post_json_value`),
and register them in `tauri::generate_handler![…]`. SSE has no Rust proxy (EventSource hits the backend directly).

## Mock

`get_plan_status` returns `{ planExists, state }` from per-repo mock state (default repo: `planExists: false`).
`write_plan` / `revise_plan` drive a simulated plan stream through a `window.__COMMAND_CENTER_MOCK_PLAN_STREAM__`
bridge that `usePlanStream` consumes when `backendUrl === 'mock'` (since EventSource can't reach the mock) —
emitting `turn-started`, a few `delta`s, then `completed`. `execute_plan` flips mock plan state to `ExecutingPlan`.

## Testing (vitest characterization, mirrors existing style)

- `planAuthoringMachine.test.ts` — write→turn-started→deltas→completed→PlanReady; revise path; failed path; execute.
- `planAuthoringValidation.test.tsx` — Write disabled until epic non-empty; Revise disabled until feedback non-empty.
- `planAuthoringScreen.test.tsx` — renders inputs/states; copy button has accessible name; failed event surfaces reason.
- App gate covered via the workspace-certification mock (planExists false → authoring screen) where feasible.
