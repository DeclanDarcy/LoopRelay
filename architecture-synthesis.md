# Command Center — Architecture Synthesis & Canonical Refactor Model

**Date:** 2026-06-25
**Type:** Architectural synthesis. **Not** an audit, a bug list, or a roadmap.
**Inputs (evidence corpus):** `architecture-audit.md` (64 findings / 14 dimensions, the primary corpus), `audit.md` (runtime corroboration R1–R9), and the repository's stated architecture in `docs/` (`architecture.md`, `reasoning-*.md`, `operational-context-schema.md`, `frontend-modernization-deviations.md`).
**Question answered:** not *"what did we find?"* (the audit answered that) but *"what architecture does the evidence imply, and what is the smallest model that explains the most of it?"*

The audit's roadmap, milestones, and refactor sequence are **deliberately ignored** here per the synthesis brief. Findings are treated only as observations. Where this document cites a finding ID (`PROJ-2`, `SHELL-1`, …) it is pointing at evidence already verified in the audit, not re-deriving it.

---

## Executive Synthesis

**The latent architecture is a single unidirectional authority pipeline. The latent defect is that the pipeline is *documented and hand-maintained* rather than *derived and enforced*. Everything else is a corollary.**

The intended design — confirmed verbatim in `docs/` — is a one-directional flow of meaning:

```
Repository filesystem  (source of truth)
   → Domain services    (SEMANTIC AUTHORITY: compute every verdict, severity, eligibility, conflict)
   → Projections        (read-models: shape the authority's output for transport)
   → Transport / shell  (PASSIVE: relay opaquely, add/drop/reclassify nothing)
   → TS contracts       (the typed promise of the projection's shape)
   → React hooks        (fetch-and-store, no domain computation)
   → React components    (PRESENTATION: render the verdict verbatim, never recompute it)
```

The architecture intends that **each downstream layer be a *derivation* of the layer above it** — a projection, a generated type, a reference, a verbatim render. The repository's own vocabulary is built around this: *authority*, *projection*, *reference*, *materialization review*, "reconstructions are never source-of-truth."

What the evidence shows is that **almost every layer boundary is implemented as a hand-made *replica* instead of a *derivation*.** The C# DTO is re-typed by hand in Rust, again in TypeScript, again in the dev mock. The backend's severity verdict is re-derived by hand in six React tables. The execution session's shape is re-copied by hand across three caches. The fetch lifecycle is re-typed by hand in thirty-two hooks. A replica has no mechanical tie to its source, so **it drifts the moment either side changes** — and nothing in the build, the type system, or the test suite can see the drift, because every test's oracle is itself one of the replicas.

So the master pattern is not "contract drift" or "god component" or "authority leak" as separate themes (the audit's framing). It is **one principle, violated in three media and hidden by two missing mechanisms**:

| What flows down the pipeline | Owned by | Correct mechanism | Codebase implements it as | Result |
|---|---|---|---|---|
| **Meaning** (verdicts, severity, eligibility, conflicts) | Authority | computed once, rendered verbatim | re-derived downstream (UI, adapters, a mislabeled "projection" service, the shell) | **meaning drift** |
| **Shape** (the wire contract) | Projection | one canonical source, generated outward | hand-replicated in 3–4 places | **shape drift** |
| **State** (the UI's live mirror of projections) | the owning feature | one owner per slice, at the feature seam | ownership collapsed to the root, reconciled by hand | **state drift** |

…and two meta-mechanisms that would make drift survivable are absent:

| Missing mechanism | Consequence | Evidence |
|---|---|---|
| **Verification derived from authority** | drift is *invisible* — every test validates a replica against itself; the mock is an anti-oracle (derived from the consumer's expectation, not the source) | `TEST-1/2`, `MOCK-1/2/3` |
| **Failure isolation, one seam per tier** | drift is *un-contained* — a single re-derivation throw blanks the whole app; one `IOException` escapes as a raw 500 | `RES-1` (backend), `RES-2`/`C1` (UI) |

Every one of the 64 findings lands in exactly one of these five cells. That is the claim of this document: **five cells explain the corpus.** Three are drift-axes (meaning, shape, state); two are amplifiers (invisibility, un-containment). The god component, the white-screen, the silent field-strip, the six tone tables — all are the same event (a hand-replica diverged from its source) seen in a different medium, made invisible by the same missing oracle and un-contained by the same missing seam.

**The single most important corroborating observation:** the codebase already contains the *correct, derived* version of nearly every primitive it violates — and where the derived version is used, the drift is absent. The `Value`-passthrough shell tier (~140 commands) cannot drift and doesn't; the 26 hand-typed mirror structs do. `lib/status.ts`'s total `Record<Enum,…>` cannot silently degrade; the six hand-rolled tone scanners do. The **reasoning domain** — the one domain with both 1:1 contract fidelity *and* falsifiable authority-regression tests (`workflowAuthority.test.ts`, `decisionTransparencyAuthority.test.ts`) — is rated the healthiest contract domain in the audit. Reasoning is the **control group**: where the invariant is mechanically enforced, it holds; everywhere it is only documented, it drifts. The difference is not care or talent. It is the presence of a mechanism.

This reframes the entire remediation question. The work is not "fix 54 items." It is **"convert each hand-replica into a derivation, using the canonical primitive the codebase already contains, after installing the oracle that makes the conversion safe."** The architecture does not need to be invented or even redesigned — it needs to be made *structurally true* instead of *aspirationally true*.

---

## Canonical Concern Model

Organized by architectural concern (not file, project, or finding). Six concerns explain the corpus; the first three are the pipeline's *substance*, the last three are its *guarantees*.

### The concerns and what each owns

1. **Authority** — owns *meaning*. The single place a given semantic (a severity, an eligibility, a conflict verdict, a health status, a push outcome) is computed. Intended home: domain services. Observed leaks: into a read-model service (`PROJ-2`), into the transport (`SHELL-2`), into the view layer (`UIAUTH-1/2/3/4`), into explainability adapters (`EXPL-1/2/3`).

2. **Projection** — owns *shape*. The derivation of a transport-ready read-model from the authority. Intended to be a thin, named, single-owner view. Observed pathologies: the word "Projection" names *three disjoint roles* (read-model, markdown renderer, and verdict authority) (`PROJ-1`); one read-model is re-projected by two layers (`PROJ-5`); one is a god-projection fanning to six domains (`STRUCT-1`); freshness of one is owned by the wrong actor (`PROJ-6`); one carries permanently-dead fields (`PROJ-4`).

3. **Contract** — owns the *typed promise* of a projection's shape across a process boundary. Intended to be single-sourced. Observed: zero generation, zero `System.Text.Json` contract attributes, 3–4 hand-maintained copies per DTO, and the one tool that could catch TS-side drift (`tsc -b`) is itself broken (`CONTRACT-2`, `audit R9`). Every confirmed runtime defect in the corpus is a *contract* defect, not a *logic* defect.

4. **Transport** — owns *carriage*, and must own *nothing else*. The Tauri shell. Intended passive. Observed bimodal: ~140 commands relay `serde_json::Value` opaquely (correct), ~26 deserialize through hand-mirrored typed structs that silently drop fields (`SHELL-1/3`), and one reclassifies an HTTP status (`SHELL-2`). Two contradictory transport philosophies coexist in one file with no boundary.

5. **Composition & State** — owns *who holds what, where*. On the backend: the DI composition root (`AddX()` extensions + `Program.CreateApp` override) — largely healthy, the template. On the frontend: ownership of server-derived state. Observed: ownership has collapsed *upward* to a 2184-line root (`COMP-1`) because no per-feature seam exists to hold it (`STATE-6`), so state is triplicated and hand-reconciled (`STATE-1`), escape-hatched via 23 raw `setData` exports (`STATE-2`), and the fetch template is replicated 32× (`HOOK-1`). Simultaneously the backend errs the *opposite* way — abstractions (interfaces, nullable-optional DI) exist with no second realization (`ABS-1/2/3`).

6. **Assurance** (Verification + Resilience) — owns the *guarantees* that the other five concerns actually hold at runtime. Two halves: **Verification** (can drift be *seen*?) — currently no, the oracle is a replica (`TEST-1`, `MOCK-3`); and **Resilience/Isolation** (can a failure be *contained*?) — currently no, one root boundary and no global exception handler (`RES-1/2`).

### Concern interaction graph (Phase 3)

```
                         ┌─────────────────────────────────────────────┐
                         │  ASSURANCE (verification + isolation)        │
                         │  — cross-cuts every seam below —             │
                         │  its absence AMPLIFIES every drift:          │
                         │   • no oracle  → drift invisible             │
                         │   • no seam    → drift un-contained          │
                         └───────────────▲─────────────────────────────┘
                                         │ observes / contains
   AUTHORITY ──owns──▶ PROJECTION ──crosses──▶ TRANSPORT ──realized as──▶ CONTRACT ──consumed by──▶ STATE
   (meaning)          (shape)               (passive)              (typed promise)          (UI mirror)
       │                                                                                        │
       └──────────────── intended: meaning computed ONCE, flows strictly left→right ───────────┘
            observed inversions (the disease):
            • PROJECTION computes meaning (PROJ-2)         ← authority flows UP into a read-model
            • TRANSPORT computes/drops meaning (SHELL-1/2) ← authority flows INTO the pipe
            • STATE/UI recomputes meaning (UIAUTH-*, EXPL-*) ← authority flows DOWNSTREAM past its owner
```

**Reading the graph.** Authority *owns* Projection; Projection *crosses* Transport; Transport is *realized as* a Contract; Contract is *consumed by* State. The pipeline is meant to be acyclic and left-to-right — exactly mirroring the backend's clean acyclic project DAG (the one dimension rated `strong`). The **disease is back-flow**: meaning computed at any node other than Authority. The **amplifier is Assurance's absence**: with no oracle the back-flow is invisible, and with no isolation seam the back-flow's failure is total.

**Which concerns are independent vs coupled.** Authority↔Projection↔Contract are tightly coupled (a single canonical source could collapse all three drifts at once — this is why one mechanism, codegen-from-authority, retires findings across four audit dimensions). Transport is *separable*: converging it on `Value` passthrough removes it from the contract-drift surface entirely, leaving only two ends to reconcile instead of four. State (frontend) is coupled to Contract (it mirrors projections) but its *ownership* pathology is independent and self-inflicted (the god component would exist even with perfect contracts). Assurance is orthogonal to all and must be addressed first because it is the precondition for safely touching any other.

---

## Root Cause Tree

```
SURFACE EVIDENCE (what was observed)
  white-screen on load · decisionSessionSummary missing · six tones disagree ·
  2184-line App.tsx · IOException → raw 500 · execution state copied 3× · mock passes while app crashes
        │
        ▼  why?
LOCAL DESIGN (the immediate cause at each site)
  context.context.items deref · Rust struct omits a field · substring-matched conflict gates launch ·
  all tabs mount · 32 copied hooks · per-endpoint try/catch
        │
        ▼  why does each recur across unrelated sites?
SUBSYSTEM CAUSE (the three drifts + two amplifiers)
  MEANING re-derived downstream  ·  SHAPE hand-replicated  ·  STATE ownership collapsed upward
  amplified by → no authority-derived ORACLE  ·  no per-tier ISOLATION seam
        │
        ▼  why do all five exist together?
ARCHITECTURAL CAUSE
  Every layer boundary is built as a hand-made REPLICA, not a derivation.
  A replica has no mechanical tie to its source, so it drifts; and the tie's
  absence is also why the drift cannot be seen or contained.
        │
        ▼  why was it built that way?
FUNDAMENTAL PRINCIPLE (violated)
  The architecture's defining invariant — "everything downstream of an authority is a
  DERIVATION of it" — was expressed in documentation and developer discipline, but
  given NO ENFORCING OR GENERATING MECHANISM. An unenforced invariant is, over time,
  indistinguishable from no invariant. The design is correct; only its enforcement is missing.
```

The tree collapses to a single sentence: **the codebase replicates where its own architecture says to derive, and enforces the difference nowhere.**

---

## Primitive Catalog

The minimal architectural primitives the model requires. The striking finding: **for almost every primitive, a correct reference implementation already exists in the repo.** The catalog is therefore mostly *"apply the primitive you already have,"* not *"invent one."*

| Primitive | Role | Status | Canonical reference already in the repo | Gap |
|---|---|---|---|---|
| **Canonical Contract Source** | one definition of every wire shape, generated outward | **MISSING** | the C# DTOs *own* serialization but nothing derives from them | no codegen, no contract test, no attributes |
| **Passive Transport** | carry `Value`, interpret nothing | **PARTIAL** | the ~140 `Value`-passthrough commands (`main.rs`) | 26 typed mirror structs violate it |
| **Read-Model Projection** | derived view, one owner, named `*Projection` | **PARTIAL** | `RepositoryDashboardProjection` (the Middle aggregation pattern is sound) | name overloaded; one god-projection; one duplicated |
| **Domain Authority** | the one home of a verdict, named `*Assessment`/`*Verdict` | **PARTIAL / MISLABELED** | the thin eligibility services the UI renders verbatim | `DecisionProjectionService` is authority wearing a projection's name |
| **Presentation Map** | total enum→appearance `Record`, no computation | **EXISTS, under-applied** | `lib/status.ts` total `Record<Enum,StatusPresentation>` | six hand-rolled tone tables ignore it |
| **Feature Controller / Ownership Seam** | one owner per server slice at the feature boundary | **MISSING (frontend)** | `useShellState` (navigation done right); `AddX()` DI (backend done right) | App.tsx owns everything; no per-tab container |
| **Resource/Action Factory** | single-source fetch & mutation lifecycle | **MISSING** | the 51-line hook template that was copied 32× *is* the spec | never extracted; one copy already diverged |
| **Isolation Seam** | one failure boundary per tier | **MISSING (both tiers)** | the Tauri *error* contract (`response_error` ↔ typed `TransportError`) shows the project can do this | no global `IExceptionHandler`; one root-only React boundary |
| **Authority-Regression Guard** | a test that *falsifies* "no layer recomputes meaning" | **EXISTS in one domain only** | `workflowAuthority.test.ts`, `decisionTransparencyAuthority.test.ts` | applied to reasoning/workflow only; not to tone, lifecycle gates, certification |
| **Authority-Derived Oracle** | test/mock data emitted *by the source* | **MISSING (is inverted)** | the typed `certification.ts` fixture hints at the right shape | the dev mock is derived from the *consumer's* type — an anti-oracle |

**Primitives to reject (the inverse failure).** The evidence also shows over-mechanization: single-impl, single-consumer micro-interfaces never substituted (`ABS-3`), a dead segregation interface (`ABS-2`), nullable-optional DI that lies about the production graph to serve tests (`ABS-1`), and a hand-mirrored 166-route table in the shell (`SHELL-4`). These are *abstractions without a second realization* — a seam paid for with no drift to prevent. They belong in the catalog as **primitives to remove**, and they prove the governing rule is two-sided (below).

---

## Architectural Tensions

Real tradeoffs the evidence exposes, with the resolution the evidence itself implies.

1. **Backend semantic authority ⟷ frontend responsiveness.** The UI re-derives meaning (tone, "Healthy" verdict, lifecycle gates) partly to avoid a round-trip per presentation decision, and in one case (`UIAUTH-2`, live markdown preview of an *unsaved* draft) it genuinely needs a client capability the backend can't serve for uncommitted text. **Resolution the evidence implies:** split *meaning* from *appearance*. The backend emits a typed semantic tier (a severity enum, `isAllowed`, `blocksExecution`); the UI owns only the enum→color/label map. `lib/status.ts` is the resolved form already in the tree; the six tone tables are the unresolved form. The draft-preview case is the one legitimate client computation — and it argues for a backend *parse endpoint*, not for the UI owning the grammar.

2. **Strong typing ⟷ transport flexibility.** The shell's two strategies *are* this tension, frozen mid-argument. Hand-typed structs give compile-time safety but become a third lossy copy; `Value` passthrough is flexible but untyped in the middle. **Resolution:** *type the ends, not the pipe.* Safety belongs at the authority (C#) and at the consumer (generated TS); the transport should be opaque. This dissolves the tension rather than splitting the difference.

3. **One canonical contract ⟷ specialized read models.** `DecisionContext` (live) and `DecisionContextSnapshot` (persisted) are two *legitimately different* shapes for two lifecycle stages; the defect (`PROJ-3`) is that they were *conflated under one TS type*, not that they differ. **Resolution:** *canonical ≠ singular.* One canonical source may emit many named projections; the rule is one *owner* per shape, not one shape per concept.

4. **Substitutability ⟷ honesty.** Nullable-optional DI (`ABS-1`) buys test-time substitutability at the cost of a production contract that lies (the null branch is dead in prod). **Resolution:** substitutability belongs in the *composition root* (`Program.CreateApp`'s override hook — which already exists), not in nullable parameters. Pay for a seam only where it is exercised.

5. **DRY/centralization ⟷ isolation/locality.** The frontend manages to violate *both* poles on one axis: over-centralized at the root (App.tsx owns 28 hooks) and under-centralized at the leaves (32 copied hooks). **Resolution:** ownership belongs at the **domain/feature seam** — neither at the root nor at each call site. The same boundary (the tab/feature) that should *own* state should *consume* the shared factory. There is one correct altitude, and both pathologies are deviations from it in opposite directions.

The unifying meta-tension behind all five: **mechanism must match the invariant it has to hold — no more, no less.** The codebase is overwhelmingly *under*-mechanized (replicas where derivations belong) but locally *over*-mechanized (`ABS-2/3`, `SHELL-4`). Both are the same error: mechanism mismatched to need.

---

## Refactoring Philosophy

General rules implied by the model. Not steps — *dispositions*. Stated as when-appropriate / when-harmful so future work can apply them to findings not yet known.

- **Instrument before you touch.** The defining property of this codebase is that *drift is invisible*. Therefore the first move is always to make the relevant invariant *observable* (a contract test, an authority-regression scan, a golden fixture) — not to change behavior. *Appropriate:* always, as the precondition for any other verb. *Harmful:* never, but instrumenting a *replica* (e.g. asserting the mock against itself) is worse than nothing because it manufactures false confidence.

- **Derive, don't replicate.** Replace any hand-maintained copy (of a shape, a verdict, a template, a config flag) with a single source the copies are *generated/projected/referenced* from. *Appropriate:* when ≥2 copies exist *and* one has drifted or can. *Harmful:* before the source is genuinely canonical — generating from a buggy source bakes the bug in (capture goldens from a known-good state, not the corrupt fixture).

- **Centralize meaning; localize state.** Push every *verdict* up to its one authority; push every *state slice* down to its one feature. *Appropriate:* for meaning re-derived downstream, and for state owned above its feature. *Harmful:* if it relocates a legitimate *presentation* concern into the backend (don't emit colors) or a legitimately shared derivation into a single tab.

- **Collapse names onto roles.** One name, one role: `*Projection` = read-model, `*Renderer` = markdown, `*Assessment`/`*Verdict` = authority. *Appropriate:* where a word names disjoint responsibilities (`PROJ-1`). *Harmful:* as a cosmetic pass with no role-separation behind it.

- **Isolate failure at exactly one seam per tier.** *Appropriate:* always — it is additive and low-risk and it is what converts the corpus's white-screens into legible local failures. *Harmful:* if used as a *substitute* for fixing the drift (an error card that hides a contract bug is debt, not a fix).

- **Defer / de-mechanize.** Remove a seam that has no second realization; do not build one speculatively. *Appropriate:* `ABS-2/3`, `SHELL-4`, dead exports. *Harmful:* collapsing a seam that *does* have real alternates (`IArtifactStore`, `IExecutionProvider`, `IGitService` — load-bearing, leave them).

The whole philosophy reduces to one verb applied recursively: **make the invariant structural** — first visible (instrument), then enforced by construction (derive/centralize/isolate), then guarded against regression (authority-regression test). Where there is no invariant to hold, remove the mechanism.

---

## Canonical Refactor Programs

These are **architectural transformations**, not milestones — no sequence, no effort sizing, no per-finding tracking (the audit owns those). Each is defined by the *invariant it makes structural*. The audit's eight programs (A–H) collapse into four, because four of the audit's programs share one root mechanism.

### Program 0 — Make the pipeline observable *(the precondition for all others)*
- **Principle:** No invariant is trusted to documentation; each load-bearing one gets an executable guard.
- **Problems solved:** the *invisibility* amplifier — the reason all other drift persisted. Restores `tsc` (the dead type-checker), adds a contract test that diffs authority-emitted JSON against the consumer's types, and extends the reasoning domain's authority-regression scan to the other domains.
- **Problems intentionally ignored:** every behavioral change. This program touches no production code.
- **Dependencies:** none — it is the root. **Risk:** near-zero; it can only *reveal*, not break. **Outcome:** drift becomes a red test instead of a white screen. The reasoning domain's health becomes the *enforced* default, not a lucky exception.

### Program 1 — Single-source the shape (Authority + Projection + Contract + Transport)
- **Principle:** Every wire shape is *derived* from one canonical source (the C# DTOs); the transport carries it opaquely.
- **Problems solved:** all *shape drift* — collapses the contract surface from four hand-copies to two derived ends, and converges the transport on `Value` passthrough so a field can no longer be dropped in the middle. Subsumes audit Programs B + the shape half of A.
- **Problems intentionally ignored:** *meaning* (Program 2 owns that) and read-model *naming* until the source is canonical.
- **Dependencies:** Program 0 (goldens must exist and be captured from a healthy state first). **Risk:** the canonical source may currently encode bugs; emission fidelity for nested polymorphic records is unverified. **Outcome:** drift of shape becomes structurally impossible, not merely tested.

### Program 2 — Re-ground meaning to its authority (Authority + the back-flow inversions)
- **Principle:** Every semantic is computed once, at its owning domain; no projection, transport, adapter, or view recomputes it.
- **Problems solved:** all *meaning drift* — relocates execution-gating out of a "projection" (`PROJ-2`), stops the shell deciding push outcomes (`SHELL-2`), and replaces the UI/adapter re-derivations (tone, markdown grammar, lifecycle gates, materialization thresholds, certification verdicts) with backend-emitted typed tiers the UI renders verbatim. Subsumes audit Programs H + the authority half of A. Requires the backend to *emit* the tiers it currently leaves the UI to infer.
- **Problems intentionally ignored:** presentation mapping (legitimately the UI's), and the markdown *renderers* (already passive — rename only).
- **Dependencies:** Program 0 (characterization tests must pin current verdict output before relocation). **Risk:** must preserve identical verdict output across the move; the conflict heuristic's *accuracy* is unverified and out of scope (relocate it faithfully, don't fix it blind). **Outcome:** meaning flows strictly left-to-right; the back-flow edges in the concern graph are deleted.

### Program 3 — Restore the ownership topology (Composition & State)
- **Principle:** One owner per state slice, placed at the feature seam; the UI's ownership tree mirrors the projection tree.
- **Problems solved:** all *state drift* — per-feature containers own their hooks and state, tabs mount conditionally, one resource/action factory replaces 32 copies, one error model replaces two. The god component dissolves as a *consequence* of correct ownership, not as a goal pursued for its own sake. Includes the backend's inverse correction: required DI via the composition root, and removal of seams with no second realization. Subsumes audit Programs C + D + G.
- **Problems intentionally ignored:** runtime/render-performance (the audit defers this to an instrumented pass; this program changes *ownership*, not measured cost).
- **Dependencies:** Program 0's tests; benefits from Program 2 (once meaning is backend-owned, feature containers hold far less). **Risk:** moving always-mounted → conditional changes fetch timing and effect lifecycles; shared cross-tab derivations must be hoisted, not duplicated. **Outcome:** ownership lives at the one correct altitude; App.tsx becomes a composition root.

The fifth concern half — **Resilience/Isolation** — is not a program but a *standing invariant* applied within every program (one exception handler at the backend tier, one boundary per UI feature). It is additive, independent, and the cheapest blast-radius reduction available; treat it as a property every program preserves, not a phase.

---

## Architectural Invariants

Rules future work must preserve. Each is justified by the evidence that its violation produced a finding; none is included on taste alone.

1. **Meaning is computed exactly once, at its domain authority.** No projection, transport, hook, adapter, or component recomputes a verdict, severity, eligibility, conflict, or outcome. *(Justified by `PROJ-2`, `SHELL-2`, `UIAUTH-1/2/3/4`, `EXPL-1/2/3`.)*
2. **Every cross-layer shape is derived from one canonical source — never hand-replicated.** *(Justified by `CONTRACT-1..5`, `SHELL-1`, `STRUCT-4`, `MOCK-1`, `ABS-4`, `STRUCT-3`.)*
3. **Transport is passive.** The shell relays `Value`; it adds, drops, and reclassifies nothing. *(Justified by `SHELL-1/2/3`.)*
4. **`Projection` names a derived read-model only.** Authorities are `*Assessment`/`*Verdict`; renderers are `*Renderer`. One name, one role. *(Justified by `PROJ-1`.)*
5. **One owner per state slice, at the feature seam.** No raw `setData` escape hatch; the UI ownership tree mirrors the projection tree; the root composes, it does not own. *(Justified by `STATE-1/2/6`, `COMP-1`, `STRUCT-1`.)*
6. **Exactly one failure-isolation seam per tier** — one backend exception handler producing the typed envelope, one error boundary per UI feature. *(Justified by `RES-1`, `RES-2`, `C1`.)*
7. **No load-bearing invariant is trusted to documentation; each has an executable guard.** The reasoning domain's authority-regression tests are the template, not the exception. *(Justified by the corpus existing at all — every drift survived because no guard observed it; and by reasoning's health, which is *caused* by its guards.)*
8. **No seam without a second realization.** Do not introduce an interface, nullable-DI parameter, or mirror layer to serve a need that has only one implementation or no observed drift. *(Justified by `ABS-1/2/3`, `SHELL-4` — the inverse failure.)*

Invariant 7 governs the other seven: a rule the build cannot check is, on a long enough timeline, not a rule.

---

## Competing Models Considered

The brief requires reconstructing rival models and justifying the winner.

**Model A — "Maturity debt."** *The findings are ordinary cleanup any fast-moving codebase accrues; there is no deep principle, just unfinished work.* — **Rejected.** It explains the copy-paste (`HOOK-1`, `ABS-4`) but under-predicts two things the evidence shows clearly: (i) the *directionality* of the authority leaks (they flow specifically *against* the documented boundary, which immaturity would not bias), and (ii) the **coexistence of the canonical primitive and its violation side by side** (`Value` passthrough next to mirror structs; `lib/status.ts` next to six tone tables; reasoning's guards next to ungoverned domains). An immature codebase has not yet *built* the correct primitive; this one has built it and then not applied it. That is a maturity *of design* with an absence of *enforcement* — a different phenomenon.

**Model B — "Two unrelated problems" (the audit's implicit framing).** *A backend authority-placement problem and a frontend god-component problem that merely coexist.* — **Rejected as non-minimal.** It treats as separate what share a cause: the god component exists *because* no feature-ownership seam exists (`STATE-6`), which is the same missing-mechanism cause as the contract drift; and contract drift (shape) and authority leak (meaning) are the *same event* — a replica diverging from its source — in two media. Model B needs two principles where one suffices, and it cannot explain why the *mock* and the *tests* drift too (they are neither backend-authority nor god-component problems; they are replicas of replicas).

**Model C — "Documented-but-unenforced derivation" (this document).** *One pipeline, one principle ("derive, don't replicate"), violated in three media (meaning, shape, state) and hidden by two missing mechanisms (no oracle, no isolation).* — **Selected.** It is the smallest model that places *every* finding — including the mock, the tests, the broken `tsc`, and the *inverse* over-abstraction findings — in a single causal frame, and it makes a falsifiable prediction the evidence already confirms: **the one domain with an enforcement mechanism (reasoning) is the one domain without drift.** A model that predicts its own control group is stronger than one that merely catalogs.

---

## Confidence Assessment

Kept in strict tiers, per the corpus's own discipline. Each tier is *more interpretive* than the last; they are never blurred.

**(a) Observed evidence — inherited, solid.** This synthesis adds no new observations. It rests entirely on the audit's verified `file:line` facts (22/22 high-impact findings independently re-verified, zero refuted) and the runtime audit's live captures. Where this document asserts a fact, it is the audit's fact.

**(b) Interpretation — well-supported, mine.** That the 64 findings partition cleanly into exactly five cells (meaning/shape/state × invisibility/un-containment); that "replica vs derivation" is the shared mechanism; that the codebase contains the correct primitive for nearly every violation. These are strongly supported by the evidence pattern but are a *reading* of it — a different architect could draw the concern boundaries differently (e.g. split Assurance into two top-level concerns, or treat Transport as a sub-case of Contract).

**(c) Synthesis — the model itself.** That the latent architecture is a single unidirectional authority pipeline, and that the master defect is *unenforced derivation*. This is the document's central claim. It is the *best* explanation of the evidence (see Model C vs A/B) but it is an explanatory construct, not an observation. Its strongest support — and its most load-bearing interpretive leap — is the **reasoning-as-control-group** claim: that reasoning's health is *caused* by its enforcement mechanism rather than correlated with it. The correlation is observed; the causation is inferred.

**(d) Recommendation — the four programs and eight invariants.** Proposed directions, not validated by implementation. They are this document's opinion on how to make the model structural. Their internal dependencies (0 before 1–3) are argued, not measured.

**(e) Speculation — flagged, carry forward only as hypotheses.** That single-sourcing the contract will surface *further* latent drift (probable, by the model — but the count is unknown). That the conflict heuristic produces false positives on real prose (the audit flagged this; this synthesis neither confirms nor needs it). That codegen fidelity for nested polymorphic records is achievable without heavy annotation. That the over-abstraction findings (`ABS-3`, `SHELL-4`) have no *future* second realization — "no current substitution" does not prove "no future need." None of these were exercised against a running system; the entire runtime axis remains, as in the source audit, deferred to an instrumented pass.

**Where this synthesis is strong vs thin.** *Strongest* where the audit is strongest and most verified — the contract/transport/authority/state findings, which are exactly the ones that drove the model; the model is essentially a compression of the corpus's densest, best-verified regions. *Thinnest* in the same places the audit is thin — the over-abstraction judgment calls and anything touching runtime behavior — and in its one irreducible inferential leap, the causal reading of the reasoning control group. The model would be *falsified* by either of two observations a future instrumented pass could make: a domain that is fully enforced yet still drifts, or a domain that is fully unenforced yet stable over time. The evidence to date shows neither.
