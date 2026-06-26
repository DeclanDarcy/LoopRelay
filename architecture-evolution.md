# Command Center — Architectural Evolution & Mechanism Reconstruction

**Date:** 2026-06-25
**Type:** Evolutionary reconstruction and mechanism design. **Not** an audit, a bug list, a roadmap, or implementation guidance.
**Evidence corpus (treated as history, not re-derived):** `architecture-audit.md` (64 findings / 14 dimensions), `architecture-synthesis.md` (the latent-architecture model), `audit.md` (runtime R1–R9), the `.agents/archive/epics/01/` decision + handoff records (Epic 1), and the 472-commit git chronology (Epics 1–9).

The prior documents answered two questions: *what is wrong?* (audit) and *what architecture does the evidence imply?* (synthesis — a documented-but-unenforced unidirectional authority pipeline). This document answers the next two:

> **Why did the architecture evolve into an unenforced pipeline, and what mechanisms keep it from drifting there again?**

It does not repeat findings. Where it cites `PROJ-2`, `R1`, `decisions.0008`, or a commit hash, it points at evidence already established, not new observation.

---

## 0. Central Thesis

The synthesis named the defect — *invariants documented but not enforced*. This document names its **cause** and its **cure**, both evolutionary.

**Cause.** Command Center was built by a **certification-gated, slice-by-slice, agent-authored** process. The gate at every milestone was *"is this slice demonstrably working end-to-end, now?"* — a **point-in-time** proof. It was never *"is the invariant mechanically preserved across future change?"* — a **cross-time** proof. The decision archive is dominated by the former: **10 of 20 decision records re-litigate the certification of M0–M4** (`decisions.0010`–`0020`), while every cross-time guard — the injectable config seam, the contract test, a working `tsc` — was explicitly deferred as *"future hardening, not a blocker"* (`decisions.0008`, `0018`, `0019`, `0020`). Point-in-time correctness with no cross-time guard decays exactly as fast as the code around it changes. The drift is therefore not a lapse in the process; **it is the process's predictable product.**

**Why the replica, specifically.** In a slice-by-slice generative method, building feature *N+1* that resembles feature *N* is cheapest by **replication** — copy the DTO shape into Rust, into TS, into the mock; copy the fetch hook; copy the tone table. Each replica is locally certified and locally correct. No gate spans two slices, so no gate ever sees a replica diverge from its source. **The replica-vs-derivation defect is the signature of certification-gated copy-first development**: every slice passes its own gate, and no gate spans slices.

**Cure.** You cannot fix a copy-first generative process with more documentation, because the process does not consume documentation — it consumes a cost gradient and a pass/fail gate. The only durable defense is to **change what the cheapest next action is**: make the correct path (derive from one source) cost less than the wrong path (replicate), and make a divergent replica *fail a gate*. That is what the one healthy domain already proves — see §5.

---

## 1. Evolution Narrative *(Phase 1 — Archaeology)*

The reconstructed arc, inferred from decision/handoff records and commit chronology (not assumed):

1. **Genesis — a clean three-tier intent.** The project opened with a correct, documented architecture: Tauri shell (transport), .NET backend (authority), React (presentation), with a strict one-directional authority flow (`decisions.0001`–`0002`, `docs/architecture.md`). *The design was right from day one.* Nothing that followed redesigned it; everything that followed under-enforced it.

2. **The certification reflex forms (M0).** The very first decision pulled sidecar lifecycle *into* M0 "to avoid runtime-foundation debt" (`decisions.0001`) and certified the runtime chain by hand (`decisions.0002`). The project's defining instinct — *prove it works now* — was set before any feature existed.

3. **The authority boundary is asserted, repeatedly, by hand (M1–M4).** "React must NOT own discovery/classification/readiness/path-resolution" is restated near-verbatim across `decisions.0004, 0005, 0006, 0007, 0010, 0015, 0018`. **An invariant restated seven times in prose is an invariant under continuous erosion pressure with no mechanical guard** — the restatement *is* the evidence that nothing but discipline held it.

4. **The first temporary-made-permanent compromises.** Lightweight markdown "sufficient for Epic 1" (`decisions.0007`), the `.gitignore` un-ignore hack (`decisions.0005`), manual `AccessDenied` testing (`handoff.0003`), the injectable config seam deferred (`decisions.0008`). Each was a rational local trade. None was revisited inside the archive.

5. **The certification crisis (M0–M4).** Five decision records (`0010`–`0014`) and four handoffs re-verify the *same* flows through escalating harnesses — API smoke → DevTools → WebDriver → dev-mock → native. Git corroborates the churn: duplicate certification commits (`aa2dd10` + `b7e8948`), `faf59bb` "Rotate decisions after crash authorization", `d0307a4` "restore". **The project spent more recorded deliberation certifying that four milestones worked than building them.** This is the energy budget going into point-in-time proof.

6. **The mock is born as an anti-oracle (M5).** A dev-only Tauri mock (`devTauriMock.ts`, `decisions.0016`) is introduced *to aid certification*. It is derived from the **consumer's expected shape**, not the backend's emitted shape. Its conversion to a real test was explicitly left open (`handoff.0015`) and it kept growing instead. This is the moment a verification surface was installed *pointing the wrong way* — the defect that later lets the mock render perfectly while the real app white-screens (`R4`).

7. **Feature accretion across Epics 2–8.** Execution, reasoning, decisions, workflow, sessions arrive epic by epic. Each new domain replicates the established shapes. The 06-21 "frontend modernization" epic is **~60 sequential "Extract …" commits** peeling hooks and panels out of monolith files — *accreted copy-paste being decomposed after the fact.* A parallel `origin/refactor-backend` branch merges at `1700ddd` — two truths developed in parallel and reconciled by hand.

8. **The reasoning domain quietly does it right.** Somewhere in Epics 2–3, reasoning ships with **1:1 contract fidelity and falsifiable authority-regression tests** (`workflowAuthority.test.ts`, `decisionTransparencyAuthority.test.ts`). It is the only domain that installs a cross-time guard. It is, not coincidentally, the healthiest contract domain in the audit. *The cure already exists in the codebase, applied once.*

9. **Heroic manual re-stabilization (MVP, 06-25).** The final epic is a **de-duplication wave** — `937ae69` "Transfer workflow UI to backend projection", `10207a5`/`d3125cc`/`23b2e44` "Retire duplicate … renderer", a long "Consolidate…/Normalize…" run. Parallel implementations that accreted across epics are collapsed *by hand* back toward the original backend-authority invariant. **This is the architecture re-asserting its own design manually** — which is the opposite of self-stabilization, and which is why the synthesis and these audits exist: the manual wave revealed how much had drifted.

The shape of the whole history: **a correct design, asserted continuously in prose, eroded continuously by copy-first slices, certified continuously at points in time, and re-stabilized heroically at the end.** Every actor did the locally rational thing.

---

## 2. Drift Mechanism Catalog *(Phase 2)*

Why drift occurred — ranked by causal weight in the evidence, not by severity.

| # | Mechanism | Evidence | Why it produced drift |
|---|---|---|---|
| D1 | **Certification-as-gate (point-in-time, not cross-time)** | `decisions.0010`–`0020` (10/20 records) | Proved each slice worked *now*; nothing proved it kept working. Drift is invisible to a point-in-time gate by construction. |
| D2 | **Copy-first slice generation** | `HOOK-1` (32 copies), 6 tone tables, 26 mirror structs, `1700ddd` parallel branch | Replication is the cheapest way to build a slice resembling the last. Each copy is a future divergence with no tie to its source. |
| D3 | **The one cross-time guard left broken** | `R9` (`tsc -b` fails before app code) | The type system — the only mechanism that *could* see shape drift — was inert, so divergent contracts compiled and shipped silently. |
| D4 | **Verification pointed at the consumer, not the source** | `R4`, `MOCK-1/2/3` | The mock encodes expected shapes, so tests validate replicas against themselves. An anti-oracle manufactures false confidence — worse than no oracle. |
| D5 | **Deferred hardening that never returned** | `decisions.0008/0018/0019/0020` (config seam), `0007` (markdown) | "Future hardening, not a blocker" is a rational local call that, absent a re-entry mechanism, is permanent. |
| D6 | **Ownership collapse upward** | `COMP-1` (2184-line App.tsx), `STATE-6` (no feature seam) | With no per-feature seam to hold state, the path of least resistance is "add another hook to the root." The root became a gravity well (see §6). |
| D7 | **Tooling absence normalized** | `decisions.0002/0012` (`cargo tauri build` unavailable), `handoff.0007/0008` (`rustfmt` never installed) | Missing tools were waived as "environmental, not functional." Each waiver removed a potential automatic guard. |
| D8 | **Ownership ambiguity for shared artifacts** | stale `CommandCenterRuntimeRepo-*` in prod config across `handoff.0012/0013/0018`; `R8` | Unowned state persists and corrupts downstream (the corrupt repo drives the `R2` null-summary class). No actor owned cleanup. |

D1 and D2 are the **root pair**; D3–D8 are conditions that let the pair run unchecked. Premature abstraction is notably *absent* as a driver — the backend's over-abstractions (`ABS-1/2/3`) are a minor inverse failure, not a drift engine.

---

## 3. Architectural Force Diagram *(Phase 3)*

For each concern, what pulls it toward order vs. disorder. The decisive pattern is in the final column.

| Concern | Force → Order (negentropic) | Force → Disorder (entropic) | Dominant force & why |
|---|---|---|---|
| **Contracts (shape)** | C# DTOs own serialization (a natural source) | Hand-replication ×3–4; `tsc` broken (`R9`) | **Disorder.** The source exists but nothing derives from it; the one guard is off. |
| **Authority (meaning)** | Domain services; documented boundary | UI/adapter re-derivation (6 tone tables); restated-in-prose-only | **Disorder**, except reasoning (guarded → order). |
| **Transport** | `Value` passthrough convention (~140 cmds) | 26 hand-mirror structs (`SHELL-1/3`) | **Mixed/frozen** — two philosophies coexist; neither force has won. |
| **State** | `useShellState` (navigation done right) | Root collapse; 23 raw `setData`; 32 copied hooks | **Disorder.** No feature seam = gravity pulls state up. |
| **Composition** | DI composition root (`AddX()` + `CreateApp`) | — (backend healthy) | **Order.** A mechanism, not discipline, holds it. |
| **Static structure** | Acyclic project DAG on a real `Core` base | — | **Order** (rated `strong`). |
| **Testing/Assurance** | Reasoning authority-regression tests | Mock anti-oracle; broken `tsc`; replica oracles | **Disorder**, except reasoning. |
| **Explainability** | `lib/status.ts` total `Record` | 6 hand-rolled tone scanners | **Disorder.** The correct primitive exists, under-applied. |

**The decisive asymmetry.** The order-producing forces are almost all **passive and discretionary** — documentation, conventions, primitives that must be *chosen* on each use (`lib/status.ts`, `Value` passthrough, the DTOs). The disorder-producing forces are **active and automatic** — copy is the default, drift accrues by inertia. *Order here requires continuous energy input (discipline); disorder is the ground state.* The only concerns at rest in order (composition, static structure) are the two where a **mechanism**, not a discipline, holds the line. This is the whole force diagram in one sentence: **wherever order depends on a choice it decays; wherever it depends on a mechanism it holds.**

---

## 4. Mechanism Inventory *(Phase 4 — the lifecycle of every invariant)*

For each load-bearing invariant, which lifecycle stages exist. `✓` present, `~` partial/manual, `✗` absent, `⊘` present-but-inverted.

| Invariant | Created | Propagated | Verified | Observed | Enforced | Recovered | Versioned | Deprecated |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| **Shape derives from one source** | ✓ docs | ~ copy | ⊘ mock | ✗ | ✗ (`tsc` off) | ~ white-screen→debug | ✗ | ✗ (dead fields persist) |
| **Meaning computed once at authority** | ✓ docs | ~ copy | ✗ | ✗ | ✗ | ~ | n/a | ✗ |
| **Transport is passive** | ~ (bimodal) | ~ | ✗ | ✗ | ✗ | ~ | n/a | ✗ (mirror tier lingers) |
| **One owner per state slice** | ✓ docs | ✗ | ✗ | ✗ | ✗ | ~ | n/a | ✗ |
| **One failure seam per tier** | ✓ docs | ✗ | ✗ | ✗ | ✗ | ✗ | n/a | n/a |
| **Acyclic project layering** | ✓ | ✓ (`.csproj`) | ~ disposition test | ~ | ~ | n/a | n/a | n/a |
| **Reasoning: no downstream recompute** | ✓ | ✓ generated-fidelity | ✓ regression test | ✓ | ✓ | ✓ | ~ | ~ |

**The diagnosis is one column-shape.** Almost every invariant has **Created** (it is documented) and nothing reliable after it. The two stages uniformly missing are **Observed** (can drift be seen?) and **Enforced** (does the build reject it?). Composition and static structure are healthy precisely because they reach **Propagated/Enforced** via real mechanisms (DI extensions, the `.csproj` graph, the disposition test). The reasoning row is the **only complete lifecycle** — and it is the only domain without drift. *The missing mechanisms are not novel inventions; they are the absent lifecycle stages (Observed + Enforced) of invariants that already exist.*

---

## 5. Self-Stabilization Model *(Phase 5)*

A self-stabilizing architecture is one where deviation triggers an automatic correction *without a human noticing*. Inventory of what currently self-stabilizes:

**Exists (the architecture maintains these without heroics):**
- **The project dependency DAG** — a back-edge would not compile; `BackendEndpointDispositionTests` locks the route surface. Layering self-stabilizes.
- **DI composition** — the `AddX()` + `CreateApp` pattern makes the composition root the one place wiring happens; deviation is visible and awkward.
- **The reasoning authority boundary** — `workflowAuthority.test.ts` / `decisionTransparencyAuthority.test.ts` are source-scanning guards that *fail* if any React/shell layer recomputes a backend semantic. Deviation → red test → correction. **This is the only behavioral invariant in the system that self-stabilizes**, and it is the control group proving the whole thesis: where a mechanism enforces the invariant, it holds; everywhere it is only documented, it drifts.

**Absent (these are maintained only by heroics, if at all):**
- Shape fidelity (no generation, no contract test, `tsc` off).
- Meaning placement outside reasoning (no regression guard).
- State ownership (no ownership validation; App.tsx regrows every epic).
- Transport passivity (no convergence forcing function).

**The decisive evidence:** the 06-25 de-duplication wave was the architecture being **re-stabilized by hand** — an agent noticing accreted drift and collapsing it. Heroic re-stabilization is not self-stabilization; it is a debt payment that recurs because the loop that produced the drift is still running. **The architecture is self-stabilizing in exactly the three places it has a mechanism, and nowhere else.**

---

## 6. Architectural Gravity *(Phase 8)* + Positive/Negative Loops *(Phases 6–7)*

### Where complexity wants to accumulate
- **App.tsx (2184 lines) is the dominant gravity well.** Because no feature seam exists (`STATE-6`), the lowest-energy place to put new state is the root. It became a dumping ground not by neglect but by *topology*: it is the only object with the gravitational mass (the hooks, the shared state) that new code needs to bind to.
- **The hand-mirror struct tier (`main.rs`) attracts new typed structs** — each new endpoint copies the pattern rather than the adjacent `Value` passthrough.
- **The explainability adapters attract new tone tables** — each new domain copies the six-table shape.
- **Naturally stable (low-energy) concepts:** the C# DTOs (they *own* serialization — the natural source of truth), the `Core` base layer, the navigation state in `useShellState`.
- **Naturally unstable concepts:** the TS types and the mock (downstream replicas, structurally always trailing their source), the App.tsx root.

### Positive feedback loops (reinforce correctness)
Generation (one source → many free derivations; every fix propagates), authority-regression guards (drift → red → fix → invariant holds → next dev inherits the guard), shared primitives (`lib/status.ts` used → next dev reuses → reinforced). **Their property:** each use makes the next correct action *cheaper*.

### Negative feedback loops (manufacture entropy)
Copy-paste (copy → diverge → two truths → next dev copies one → three truths), the mock anti-oracle (mock matches consumer expectation → tested against itself → green → false confidence → real drift ships → mock updated to the *new* expectation, still self-referential), parallel branches, the broken-`tsc` ratchet (off → drift ships → reality diverges further from the types → re-enabling `tsc` gets more expensive → stays off). **Their property:** each turn *raises* the cost of the correct action — once 32 hooks are copied, extracting the factory costs more than copying a 33rd.

**The engine of drift, stated precisely:** the negative loops are **auto-catalytic** (each iteration lowers the cost of the next wrong move and raises the cost of the right one); the positive loops require **activation energy** (someone must build the generator or the guard *first*, before any payoff). An architecture left to inertia therefore drifts — not because anyone chose drift, but because entropy is downhill and order is uphill, and only a mechanism installs a ratchet on the uphill side.

---

## 7. Stability Classification *(Phase 9)*

The 14 audited dimensions mapped onto evolutionary phase. (Classification interprets the dimension ratings + chronology; it is not a re-rating.)

| Class | Subsystems | Why |
|---|---|---|
| **Stable** | Project DAG (`strong`), DI composition, `Core` base, `Value`-passthrough shell tier, **reasoning** contract domain | Held by mechanisms, not discipline. Will survive growth. |
| **Growing** | Execution / decisions / workflow / sessions domains | Added epic-by-epic; still accreting features and replicas. Each addition currently multiplies future cost (§8). |
| **Volatile** | App.tsx root state (`mixed`, actively churned by the "Extract" wave), the 32-hook tier, the mock (`at-risk`) | Under active hand-rework; no mechanism has stabilized them. |
| **Transitional** | The contract tier (caught mid-migration: hand-mirror ⟷ `Value` passthrough — "two philosophies frozen mid-argument"), explainability adapters | A migration is underway but unfinished; direction is set, convergence is not enforced. |
| **Legacy** | The 26 hand-mirror structs, dead segregation interfaces (`ABS-2/3`), dead projection fields (`PROJ-4`) | Superseded by a better pattern that already exists; lingering only because nothing deprecates them. |
| **Emergent** | The authority-regression test pattern (in one domain, wants to spread), the 06-25 consolidation instinct | A correct mechanism / discipline that has appeared but not yet generalized. The system's own immune response, nascent. |

The healthy classes are *mechanism-held*; the unhealthy classes are *discipline-held*. Stability tracks mechanism presence with near-perfect fidelity.

---

## 8. Architectural Economics *(Phase 11)*

Conceptual cost of each architectural action, split into **essential** (irreducible) vs **accidental** (an artifact of replication).

| Action | Essential cost | Accidental cost | Signature |
|---|---|---|---|
| Add a projection | Define the read-model shape once | Re-type it in Rust + TS + mock (≈3×); risk silent drift | ~3–4× multiplier |
| **Change a contract** | Change the source shape | Find every hand-replica with **no compiler help** (`tsc` off) and **no contract test** | **Unbounded** — you cannot know when you are done |
| Add a feature | The domain logic | Wire another hook into App.tsx; copy fetch template; copy tone table; add to all-mount tree | Grows with App.tsx mass |
| Add a domain | New domain service | New contract replicas ×3; new tone table; new mock entries; new authority re-derivations | Highest accidental load |
| Change authority | Change the verdict computation | Reconcile the 6 downstream re-derivations (`UIAUTH-*`) | Drift-surface-proportional |

**The economic signature of the whole system:** every accidental cost **scales with the replica count**, and the replica count **grows with every slice**. Therefore the **marginal cost of every architectural action is monotonically increasing**. A derivation-based architecture has flat or *declining* marginal cost (define the source once; derivations are free, and the generator amortizes). A replication-based architecture has *rising* marginal cost (each addition multiplies the cost of future additions). This is the economic statement of the entropy thesis, and it is why the 06-25 consolidation wave was necessary: the team was paying down accumulated marginal cost before it compounded past the point of affordability. The worst cell — **changing a contract has *unbounded* accidental cost** — is also the most-shipped change class and the source of every confirmed runtime defect (`R1`, all of C2). That is not coincidence; it is the cell where the missing mechanism (Observed + Enforced shape) bites hardest.

---

## 9. Mechanism Architecture *(Phase 10 — the minimal self-maintaining set)*

Ignoring all current implementation, the **minimal set of mechanisms** that would make the architecture self-maintaining. These are *mechanisms* (what must exist and what lifecycle stage it fills), **not** implementations or refactor steps. The framing from §4 makes the set fall out directly: **every load-bearing invariant needs its missing Observed and Enforced stages installed.** Six mechanisms cover all of them.

1. **An Oracle (the keystone).** One mechanism that emits truth *from the authority* and is the source every verifier consumes — so tests and mocks are **derived, not replicated**. This is the precondition for all others: it is the single mechanism whose absence disables verification entirely, and whose presence the reasoning domain already proves works. It fills **Observed** for every invariant at once. *Without the oracle, every other mechanism is itself unverifiable.*

2. **A Generation mechanism (shape).** One that makes every downstream shape a *derivation* of the canonical source, so a divergent contract is **unrepresentable** — there is no replica to write wrong. Fills **Propagated + Enforced** for the shape invariant. Note: **versioning becomes possible only once generation exists** — you cannot version four hand-copies, only one generated source.

3. **An Authority-Regression mechanism (meaning).** Generalize the reasoning-domain guard to every domain: a falsifiable check that *fails* when any layer recomputes a verdict. Fills **Enforced** for the meaning invariant. The pattern is already written; the mechanism is its generalization, not its invention.

4. **An Ownership-Validation mechanism (state).** A guard that fails when server-derived state is owned above its feature seam — the ratchet that prevents App.tsx from regrowing every epic. Fills **Enforced** for the state invariant.

5. **A Layering/Isolation mechanism.** Make the already-stable acyclic DAG and the one-seam-per-tier rule explicit guards, so they survive growth rather than relying on the current happy accident. Fills **Enforced** for structure and resilience.

6. **A Drift/Deprecation detector (negative space).** Surfaces dead fields, dead seams, and replicas that have lost their live source — the only stage that reads *absence*. Fills **Deprecated** across all invariants.

**Minimality argument.** Mechanisms 1–3 retire the two drift axes that share the replica cause (meaning + shape); 4 retires state; 5–6 are the hygiene that prevents the specific regressions this history actually exhibited. The set is irreducible: drop the Oracle and nothing can be observed; drop Generation and shape stays hand-held; drop Authority-Regression and meaning re-derivation returns. **The keystone is the Oracle** — it is the mechanism that lets the architecture *see itself*, and self-observation is the precondition for self-stabilization. The reasoning domain is the existence proof that the keystone + a regression guard is sufficient to hold a domain at rest in order indefinitely.

---

## 10. Canonical Evolution Model *(Phase 12 — the five-year architecture)*

Not features — the trajectory of each concern, and the single force that should drive all of them.

- **Authority** evolves **centripetally**: every semantic the UI currently *infers* (tone tier, "Healthy" verdict, lifecycle gate, certification outcome) becomes a backend-*emitted* typed tier. The long-run end state is that the UI computes no meaning at all. *Sustained by:* authority-regression guards spreading domain-by-domain until every domain looks like reasoning.
- **Contracts** evolve **replicated → generated → unrepresentable-when-wrong**. The destination is a state where a divergent contract simply cannot be authored. Only then does versioning (and safe deprecation of fields) become possible.
- **Transport** evolves toward **total convergence on passive `Value` passthrough**; the hand-mirror tier asymptotes to zero. Transport's correct evolution is to *cease being a possible source of meaning* entirely.
- **Composition / State** evolves toward the **feature-seam topology**: App.tsx asymptotes to a pure composition root; each new domain arrives carrying its own container and owning its own slice. The gravity well drains as the seam appears.
- **Testing / Assurance** is the **master move**: replica-oracle → authority-derived oracle → contract-test-as-gate. It evolves first because it is the mechanism that makes every other evolution *safe to attempt*.
- **Explainability** evolves from **re-derivation adapters → verbatim renderers** of backend-emitted explanation tiers.
- **Runtime** evolves from **un-instrumented → instrumented** — the one axis every audit was forced to defer. The five-year arc makes runtime cost *observable* so it stops being a structural blind spot.

**The unifying five-year statement.** The architecture's long-term work is **not building features — it is building the immune system that lets features be built without drift.** Concretely: the systematic conversion of every invariant from *Created-only* to *fully-lifecycled* (§4), domain by domain, until the whole system looks like the reasoning control group. And because the development method is **agent-authored and copy-first**, the immune system cannot be discipline — it must be **mechanism that reshapes the cheapest next action**. The deepest and most durable claim in this document: *you cannot fix a generative process by telling it the rules; you fix it by making the rule-following path the path of least resistance.* The reasoning domain is the proof that this destination exists and is reachable, because it is already there.

---

## 11. Confidence Assessment

Strictly tiered, never blurred. Each tier is more interpretive than the last.

**(a) Evidence — observed, inherited, solid.** The nine-epic chronology (git, decisions, handoffs); the 14 dimension ratings; the finding IDs and their `file:line` anchors; `R1`–`R9`; the certification-record counts (10/20); the existence and health of the reasoning guards. This document adds no new observation.

**(b) Interpretation — well-supported readings of the evidence.** The force diagram's order/disorder assignments; App.tsx as a *gravity well* (topological reading of `COMP-1`+`STATE-6`); the stability classification; the rising-marginal-cost economics. A different architect could draw some boundaries differently, but each is tightly anchored to a cited finding.

**(c) Historical reconstruction — inference about *causes*, not just facts.** That **certification-pressure (point-in-time gating) was the dominant evolutionary force**; that **copy-first slice generation** is why replicas specifically dominate; the inferred ordering of inflection points (the records are dated but their causal links are reconstructed); that the mock was *born* as an anti-oracle rather than degrading into one. These are plausible and evidence-consistent, but they are reconstructions of *why*, and the records do not state motive explicitly.

**(d) Mechanism design — proposed, not validated.** The six-mechanism minimal set and its irreducibility argument; the "install Observed + Enforced" framing of §4; the keystone claim for the Oracle. This is design reasoning, supported by the reasoning-domain existence proof but not validated by building it.

**(e) Speculation — carry forward only as hypotheses.** That the heroic consolidation wave **will recur** without mechanisms (probable by the loop analysis, unproven). That the agent-authored method *specifically* (vs. any fast human team) caused copy-first — the signature is identical for both, so this is under-determined. The entire five-year trajectory in §10. That the reasoning domain's health is *caused by* rather than *correlated with* its guards — the synthesis's load-bearing inferential leap, inherited here. None of these has been exercised against a running system.

**Falsifiers.** This model is wrong if either appears in a future instrumented pass: a domain that is **fully mechanism-enforced yet still drifts**, or a domain that is **fully unenforced yet stable across multiple epics of change**. The evidence to date shows neither — reasoning is enforced and stable; everything discipline-held has drifted.
