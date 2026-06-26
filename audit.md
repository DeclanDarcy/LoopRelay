# Command Center — Runtime / Integration Audit

**Date:** 2026-06-25
**Scope:** Why the desktop app rendered briefly then went blank under `cargo tauri dev`, the
underlying frontend↔backend integration problems, and the proposed remediation paths.
**Author:** Pairing session (Claude Code).

---

## 1. How to run the app (corrected)

```
cd src/CommandCenter.Shell
cargo tauri dev
```

The Tauri shell (`src/CommandCenter.Shell/src/main.rs`) spawns the .NET backend
(`CommandCenter.Backend.exe`) on `http://127.0.0.1:5000`, waits for `/api/ping`, then loads the
Vite UI. Toolchain present and working: `tauri-cli 2.11.3`, `dotnet 10.0.301`, `node v25.8.2`.

---

## 2. Methodology (so every finding below is reproducible)

Findings are evidence-based, not inferred. Three harnesses were used:

1. **Live backend inspection.** The backend spawned by the running shell was queried directly over
   HTTP (`http://127.0.0.1:5000/api/...`) to capture *real* response shapes.
2. **Faithful headless reproduction.** A Playwright page (Chromium) was given a shim
   `window.__TAURI_INTERNALS__.invoke` that proxies each command to the real backend, using a
   command→URL map parsed from `main.rs`. This drives the *real* React app against *real* data
   with no Tauri window, surfacing the exact uncaught errors and component stacks. The app's own
   dev mock (`?mock=workspace-certification`) was used as the "expected shape" control.
3. **Automated contract-drift scan.** For every load-time `GET` command, the real response's
   top-level keys were diffed against the declared frontend TypeScript type
   (`invokeCommand<T>` in `src/api/*.ts` → type body in `src/types/*.ts`).

**Coverage limits (important — see §7):** the drift scan compares **top-level keys only**, on a
**single repository** in one degenerate state, over **load-time GET endpoints only**. Nested-object
drift, array-element drift (most arrays were empty for this repo), POST/action paths, and
healthy-repository states were **not** exhaustively verified.

---

## 3. Executive summary

The app starts correctly; the backend comes up. The blank screen is a **frontend runtime crash**.
Three compounding causes:

- **C1 — No error isolation.** There was no React error boundary anywhere, and **all primary tabs
  mount simultaneously** (`App.tsx` toggles visibility with `hidden=`, not conditional rendering).
  So a throw in *any* mounted tab unmounts the entire React root → blank window with no message.
- **C2 — Frontend↔backend contract drift.** The UI and its in-repo mock encode shapes that the real
  backend does not always emit. Confirmed structurally for **decision context**; plus a
  **null/degenerate-data** robustness gap class (the original crash).
- **C3 — Corrupt registered repository.** The only registered repo is a leftover test artifact in
  `…\Temp\CommandCenterRuntimeRepo-…` whose decision-session snapshots are failing to rebuild
  ("file used by another process"), maximizing the amount of degenerate data the UI must tolerate.

The drift (C2) is **concentrated, not pervasive**: of ~40 load-time endpoints scanned, only
`get_decision_context` shows a true top-level structural mismatch. The widespread *appearance* of
breakage is C1 amplifying a small number of real defects.

---

## 4. Fixes already applied in this session

| # | File | Change | Status |
|---|------|--------|--------|
| F1 | `src/CommandCenter.Shell/tauri.conf.json` | Dropped the erroneous `../` from `beforeDevCommand`/`beforeBuildCommand` (Tauri runs them from `src/`, so `../CommandCenter.Backend` overshot). | Verified — build hooks resolve. |
| F2 | `src/CommandCenter.UI/src/components/AppErrorBoundary.tsx` (+ wired in `main.tsx`) | Top-level error boundary that renders the error + component stack instead of a blank screen. | Verified — this is what surfaced the real errors. |
| F3 | `src/CommandCenter.UI/src/features/repositories/SelectedRepositorySummary.tsx` | `governanceSummary` / `reasoningSummary` now fall back to fully-populated empty objects, so a missing/null summary degrades to "Not projected". | Verified via headless repro — original crash gone. |

> Note: F3 fixed the **first** crash the user hit. Because of C1, the next mounted tab's crash
> (R1 below) then surfaced. F3 is necessary but not sufficient on its own.

---

## 5. Remaining issues

Severity: **High** = causes white-screen on load for the current repo state; **Medium** = real
defect, conditional or non-fatal; **Low** = informational / hygiene.

### R1 — Decision context structural contract drift  **[High] — confirmed crash**
- **Crash:** `TypeError: Cannot read properties of undefined (reading 'items')` at
  `src/features/decisions/DecisionLifecycleTab.tsx:268` →
  `context?.context.items.length` (the `?.` guards `context`, not `context.context`).
- **Root cause:** real vs expected shape mismatch.
  - Frontend type `DecisionContextSnapshot` (`src/types/decisions.ts`): `{ snapshotId, repositoryId, createdAt, fingerprint, context: DecisionContext, diagnostics, validation }`.
  - Real backend (`GET /api/repositories/{id}/decisions/context`): `{ repositoryId, fingerprint, items, diagnostics, validation }` — **`items` is top-level**, the nested `context` wrapper is absent, and `snapshotId`/`createdAt` are missing.
- **Impact:** `DecisionLifecycleTab` is mounted on initial load (C1), so this white-screens the
  whole app for *any* repo, not just the corrupt one. A `?.` guard would only mask it (always "0
  context items"); the real fix is contract alignment (see §6, P2).
- **Drift-scan line:** `[DRIFT] get_decision_context <DecisionContextSnapshot> MISSING: snapshotId, createdAt, context | EXTRA: items`.

### R2 — Degenerate-data / nullability robustness gaps  **[High] — class of defect**
- **Confirmed instance (now fixed, F3):** `decisionSessionSummary` / `reasoningSummary` can arrive
  `null`/absent. The backend computes these from snapshots that **transiently fail to rebuild**
  (observed in the live payload: *"…snapshot.json … being used by another process"*). On first
  paint `workspace` is also `null`, so code fell back to a projection lacking the field.
- **Why it's a class, not a one-off:** the UI was authored against an always-complete mock, so many
  components dereference projection sub-objects without guards. F3 closed one; others likely exist
  on tabs not yet reached (the headless repro stops at the first throw, and C1 makes each one
  fatal). **Not exhaustively enumerated** — see §7.

### R3 — No per-tab error isolation + all-tabs-mount  **[High] — systemic amplifier**
- **Evidence:** `App.tsx:1765` renders the workspace panel with `hidden={activePrimaryTab !== 'workspace'}`; the decisions/reasoning/etc. panels are siblings mounted the same way. All tab
  subtrees execute on load regardless of which tab is active.
- **Impact:** turns any single component defect (R1/R2) into a full-app white-screen. This is the
  reason the symptom is "blank screen" rather than "one broken panel".

### R4 — Mock encodes expected shapes, not real backend shapes  **[Medium] — defect-hider**
- `src/devTauriMock.ts` (installed only with `?mock=workspace-certification`) returns data matching
  the **frontend types**, so the mock renders the entire app perfectly while the real backend
  crashes it. Dev/visual testing against the mock cannot catch C2. The mock is a fidelity risk: it
  validates the UI against itself.

### R5 — 404s on lifecycle-policy endpoints for sessionless repos  **[Medium]**
- `GET …/decision-sessions/lifecycle/policy` and `…/policy/diagnostics` returned **HTTP 404** for
  this repo (no active decision session). These feed `useDecisionSessions`. A 404 becomes a thrown
  `invokeCommand` → relies on hook-level catch. Needs confirmation that every consumer treats
  "no active session" as a normal empty state rather than an error or a hard dereference.

### R6 — Empty-body "null" responses  **[Medium]**
- `get_active_decision_session` and `get_decision_session_certification` returned an **empty body**
  (modelled as `T | null`). Direct `JSON.parse` fails on empty string. Confirm the api/transport
  layer (`src/api/tauri.ts` / Rust `Option<T>` serialization) yields JS `null` rather than throwing
  for these.

### R7 — Backend emits fields the frontend type omits  **[Low] — informational]**
- `get_continuity_diagnostics` → EXTRA `revisionFrequency`, `evolutionLedger`;
  `get_decision_session_certification_report` → EXTRA `governance`, `health`.
- Harmless at runtime (ignored by the UI) but indicates the type definitions trail the backend;
  the UI may be missing data it could show. Symptom of the same drift as R1, opposite direction.

### R8 — Corrupt registered test repository  **[Medium] — operational]**
- A single repo is registered: `…\Temp\CommandCenterRuntimeRepo-b4541eae…` (`readiness: MissingPlan`),
  almost certainly a test/runtime fixture that was persisted to app config and never deregistered.
  Its `.agents/decision-sessions/analysis/metrics/snapshot.json` is repeatedly locked during
  rebuild, producing the null summaries behind R2. Deregistering it (registration removal only;
  files are not deleted) removes the worst data source.

### R9 — `tsc -b` is broken in this checkout  **[Medium] — tooling]**
- `npx tsc -b` fails *before* reaching app code: `Unknown compiler option 'erasableSyntaxOnly'`,
  `tsBuildInfoFile` without `incremental/composite`, and parse errors inside
  `node_modules/@vitejs/plugin-react/dist/index.d.ts`. This points to a TypeScript version vs
  `tsconfig`/dependency mismatch (`typescript ~6.0.2` in `package.json`). Consequence: **type
  checking cannot currently gate changes** — Vite/esbuild transpiles without full type checks, so
  contract drift like R1 compiles and ships silently. This should be fixed early; otherwise the
  type system (our main defense for C2) is inert.

---

## 6. Proposals

Three workstreams. They are complementary; the recommended order is P1 → P3 → P2.

### P1 — Contain the blast radius (per-tab error isolation)  *(recommended first; ~0.5 day)*
Wrap each mounted primary tab/panel in its own error boundary (reuse `AppErrorBoundary` or a
lighter inline variant). A broken tab then renders an error card while the rest of the app stays
usable.
- **Pros:** immediately makes the app usable regardless of remaining R1/R2 defects; converts
  white-screens into localized, legible failures; low risk; no contract decisions required.
- **Cons:** symptomatic — hides drift behind error cards if not paired with P2; touches `App.tsx`
  JSX (mechanical but should be done carefully).
- **Risk:** low.

### P2 — Fix the contract drift at the source  *(the real fix; effort depends on §9 answer)*
Align the two sides so types == real backend JSON, endpoint by endpoint, starting with
decision context (R1), then audit nested/array-element shapes the scan couldn't reach (§7).
- **Decision required (blocking):** *which side is canonical?* See §9. If the **backend** is the
  source of truth, update `DecisionContextSnapshot` (and the mock, and `DecisionLifecycleTab`) to
  read top-level `items`. If the **frontend** is, fix the backend serializer to emit the nested
  `context` wrapper + `snapshotId`/`createdAt`.
- **Pros:** eliminates the defect class rather than masking it; restores the type system as a real
  guard (once R9 is fixed); fixes R7 in passing.
- **Cons:** requires the canonical-contract decision; larger surface; needs the mock updated in
  lockstep or it keeps hiding regressions (R4).
- **Risk:** medium — touching serialization or shared types can ripple.

### P3 — Restore the safety nets  *(parallelizable; ~0.5–1 day)*
- Fix `tsc` (R9) so type checking runs in CI/build and would have caught R1.
- Make `devTauriMock` a **contract fixture** generated from / validated against real backend
  responses (R4), or add a thin contract test that diffs live responses vs types (the §2 scanner
  can be promoted to a test).
- Deregister the corrupt repo (R8) and confirm the **no-repository empty state** renders cleanly
  (not yet verified — see §7).

### Recommendation
Do **P1 now** to unblock day-to-day use, **P3's tsc fix** next (cheap, high leverage), then **P2**
once you confirm the canonical contract. P1 without P2 leaves real bugs behind error cards; P2
without P1 means each fix is gated behind a white-screen during development.

---

## 7. What this audit did NOT establish (honesty / residual risk)

- **Only the first crash per render is observed.** The headless repro halts at the first throw;
  R1 is confirmed, but additional R2-class crashes on decisions/reasoning/governance/workflow tabs
  are **likely but unenumerated**. P1 (per-tab boundaries) is the cheapest way to flush them all out
  at once.
- **Top-level keys only.** Nested-object and array-element drift were not diffed; most list
  endpoints returned empty arrays for this repo, so their element shapes are unverified.
- **One repository, one state.** Everything was observed against a single `MissingPlan`, corrupt
  temp repo. Healthy repos and the **zero-repository empty state** were not validated end-to-end.
- **GET/load paths only.** POST/action flows (register, start execution, commit, push, proposal
  review, transfers) were not exercised against the real backend.

---

## 8. Evidence appendix

**Real decision-context response (R1):**
```
GET /api/repositories/{id}/decisions/context
→ { repositoryId, fingerprint, items, diagnostics, validation }
Expected by UI (DecisionContextSnapshot): { snapshotId, repositoryId, createdAt, fingerprint, context, diagnostics, validation }
```

**Contract-drift scan — only the structural drift flagged (rest matched top-level):**
```
[DRIFT] get_decision_context <DecisionContextSnapshot>  MISSING: snapshotId, createdAt, context | EXTRA: items
[ok]    get_continuity_diagnostics            EXTRA: revisionFrequency, evolutionLedger
[ok]    get_decision_session_certification_report  EXTRA: governance, health
404     get_decision_session_lifecycle_policy(/_diagnostics)        (no active session)
empty   get_active_decision_session, get_decision_session_certification  (T | null, empty body)
~30 other load-time GET endpoints: top-level keys matched their declared types.
```

**Original crash (R2, fixed by F3):**
```
TypeError: Cannot read properties of undefined (reading 'healthDimensions')
  at SelectedRepositorySummary (…:governanceSummary.healthDimensions)
```

**Backend snapshot contention (drives R2/R8):** live `decisionSessionSummary.healthDimensions[].findings`
contained repeated *"snapshot.json … being used by another process"* rebuild failures.

---

## 9. Open questions for the owner (blocks P2)

1. **Which contract is canonical** for `decisions/context` (and by extension the drift class) — the
   **real backend JSON** (`items` top-level) or the **frontend types/mock** (nested `context`,
   `snapshotId`, `createdAt`)? This determines whether P2 edits the C# serializer or the TS types.
2. Is the `…Temp\CommandCenterRuntimeRepo-…` registration **disposable** (safe to deregister)?
3. Do you want **P1 (isolate now)** first, or go **straight at P2 (contract alignment)**?

---

## 10. Housekeeping

A second Vite dev server was started on `localhost:5174` during testing (your `cargo tauri dev`
owns `5173`). It can be stopped; it does not affect the app.
