# Programming-Language Architecture Audit — Language-Blind

> **Method.** This audit treats the system as an executable discovered greenfield, with its
> implementation language(s), build system, tooling, and dependency ecosystem intentionally
> removed. Nothing below is argued from what the repository "already uses," from migration cost,
> from sunk effort, or from contributor familiarity. Candidates are defined by *architectural
> properties*; representative languages are named where doing so aids the reader. Priorities are
> strictly ordered and applied **lexicographically**: **(1) capability ceiling** dominates; **(2)
> performance** separates only candidates judged comparable on capability; **(3) engineering
> effectiveness** breaks ties only.
>
> Produced by a fan-out audit: parallel subsystem readers extracted language-neutral requirements →
> a first-principles specification was synthesized → seven candidate combinations were scored by
> independent, blind evaluators → an adversarial adjudication ranked them.

---

## 1. Executive Summary

The system is a **control plane for autonomous software-project advancement**: it drives external,
non-deterministic, expensive, *streaming* automation/AI agents through **long-running, resumable
loops**, using a version-controlled repository's own filesystem as the single source of truth. Three
things are fused at its core: (a) a **reliability wrapper** around an unreliable agent subprocess
(a held-open bidirectional multi-turn session *and* a one-shot streaming invocation, behind one
facade); (b) a **durable, resumable finite-state machine** modeling a staged planning lifecycle
(roadmap → epic selection → milestone specs → execution slices → completion certification → close),
advancing one persisted transition per invocation, where *pause/block is a first-class outcome*;
and (c) a **security-enforced mediation layer** enforcing closed-world default-deny with
non-negotiable denies over what the agent may do. It is surfaced through thin CLI executables and a
long-lived backend service feeding a native desktop shell over a replayable event stream. **All
durable state is content-stable, schema-versioned, byte-deterministic artifacts** under a hidden
repository-owned directory. In-process compute is negligible; the dominant cost is agent turns and
deliberate multi-hour quota waits. The architecture therefore optimizes for **correctness,
resumability, safety, and economic decisioning — not throughput.**

**Recommendation.** Build it in **Rust as a single language, with an embedded, capability-confined
plugin layer** (WASM component model, with an embedded-interpreter fallback). Rust wins on the
dominant priority — the *capability ceiling* — because it converts the system's two most
load-bearing correctness properties into **static, compiler- and ownership-enforced guarantees**
rather than conventions: (i) *deep immutability of aliased cached object graphs*, and (ii)
*exhaustive case handling over dense closed vocabularies* so "a missing rule/route fails fast" is a
build error. Its embedded confinement boundary is a structural synergy with the product's
safety identity, letting the highest-value extension levers (vendor-neutral transport adapters,
hot-reloadable prompt/policy packs) expand the system without breaching the non-negotiable denies.
It fully covers the intricate deadlock-avoidance concurrency and imposes no ceiling on the
fleet-scale, event-sourced-durability, or interactive-steering levers.

The runner-up is a **managed CLR-family runtime pairing a multiparadigm object language with a
functional-sibling domain core and compile-time source generators** — capability-equal (tied at 9)
with the best engineering ergonomics, but it enforces the load-bearing aliased-graph immutability
only *by discipline*, and its standout advantages are lower-*effort* routes to capabilities Rust can
also reach (the engineering tiebreaker, not the capability axis). **Confidence: medium** — the #1
pick is robust (it leads on both capability and the performance tiebreaker), but the recommendation's
*rationale* should rest on the correctness identity rather than on raw performance (see §11–§12).

---

## 2. Architectural Characterization

The system is not a monolithic application but an **integration hub / control plane** whose value is
mediating between heterogeneous external actors across process and filesystem boundaries.

- **Reliability wrapper over an unreliable agent subprocess.** Two postures behind one runtime
  facade, chosen per step: a **held-open, bidirectional, multi-turn session** over the child's
  standard streams (structured request/response + notification protocol, with a capability handshake
  capturing a durable thread identifier), and a **one-shot prompt-to-completion streaming
  invocation** (reads a prompt to input-EOF, emits a terminal-delimited event log). The system's
  identity is making an unreliable, streaming, expensive external process behave reliably.

- **Durable, resumable finite-state machine.** A staged planning lifecycle advanced **one persisted
  transition per invocation**. The domain is dense with discriminated states, transition intents,
  outcomes, dispositions, drift/closure classifications, provenance kinds, request kinds, and rule
  taxonomies — many validated for *mutual completeness at construction*. Pause/block is a
  first-class, expected outcome, not an error.

- **Security-enforced mediation layer.** Closed-world default-deny: anything not explicitly allowed
  is denied, and a set of **non-negotiable denies** (privilege escalation, recursive force-delete,
  network fetch, force-publish, indirect shell execution) must survive *any* consumer policy via
  both a minimum-policy floor merge and a redundant post-evaluation invariant guard. Safety is the
  product identity.

- **Two coordinated frontends over one runtime + one artifact substrate.** Thin one-shot/resumable
  CLI executables (an unbounded execution loop, a standalone planning pipeline, a resumable
  state-machine driver), plus a long-lived backend **service** exposing a request/response API and
  **replayable server-push event streams** (single-producer broadcast, monotonic sequence ids,
  bounded replay for reconnect, exactly-once across the replay/live boundary) to a native desktop
  shell that owns windowing, native dialogs, and sidecar lifecycle, fronted by a thin passive relay.

- **Filesystem-as-database.** Durable state lives entirely as serialized artifacts under a hidden,
  repository-owned directory — an authoritative mutable snapshot, append-only journals, validated
  review/decision ledgers, projection-provenance manifests, numbered handoff/evidence files — every
  one schema-versioned, deterministically byte-stable, and *self-ignored* from the target project's
  version control so telemetry never corrupts change-detection gates or leaks into commits.

- **Economic sub-domain.** Process reuse-versus-recycle is governed by **cache-weighted token
  accounting**; a multi-stage read-only review pipeline classifies, semantically confirms, and
  synthesizes insight over repository changes.

- **Pervasive substitutability.** Nearly every collaborator is an injected, register-if-absent
  seam — agents, transports, cost policies, storage, workflow policy are all substitutable.

---

## 3. Fundamental Technical Constraints

Ranked by hardness. **Fundamental** constraints are inherent to the architecture; **strong** are
load-bearing design commitments; **soft** are calibration/versioning hazards.

**Fundamental**

1. **Repository filesystem is the whole database.** No private DB. Missing files/dirs are *valid
   states* to be projected, not failures. Every read must tolerate concurrent, out-of-band mutation
   by the agent, human editors, and VCS checkouts — atomic temp-write+rename, read-share with retry
   on transient sharing violations, stable-signature-guarded caching.
2. **Bidirectional-pipe deadlock avoidance.** A held-open duplex child requires a **sole writer**
   draining an unbounded outbound queue, a **strictly non-blocking read pump** that never awaits a
   write, and a **continuous error-stream drainer**, or the transport self-deadlocks. Inherent to
   duplex streaming over OS pipes.
3. **Closed-world default-deny with unoverridable denies.** Deny-by-default plus a set of
   non-negotiable denies that survive any consumer policy (floor merge + redundant post-evaluation
   guard). Weakening it is out of bounds.
4. **Subprocess invocation by argument arrays only.** External tools launched with explicit argument
   arrays scoped to a working directory — never shell-interpolated strings; the command parser must
   reject shell constructs. This is the injection-safety boundary.
5. **Resumability only at whole-iteration boundaries.** Only a small resume snapshot (durable thread
   id + accounting/health counters) is persisted after a successful iteration; in-flight turn
   progress is *not* checkpointed. The external agent owns conversation state; a mid-turn crash loses
   that turn.

**Strong**

6. **Turn serialization within a session** — a single-permit gate; deliberately no intra-session
   turn parallelism.
7. **Dual persistence defaults** — state/ledger/telemetry IO is **fail-open** (never breaks a loop
   iteration); permission/approval evaluation is **fail-closed** (defaults to DENY on any error or
   missing gateway). Both defaults are load-bearing.
8. **Deep immutability of aliased cached graphs** — a deserialization cache aliases one materialized
   graph to many callers, so every persisted type must be deeply immutable with nondestructive
   update; any in-place mutation corrupts shared state.
9. **Byte-stable, deterministic serialization** — canonical, reproducible bytes (stable ordering, no
   BOM, trailing newline, culture-invariant comparison) so content hashes, freshness provenance, and
   golden contracts never drift spuriously.
10. **Provable path containment** — all artifact IO provably within the repository root; reject
    parent-traversal, absolute escapes, and per-segment symlink/reparse-point escape.
11. **Multi-file operations are not atomic** — single-file writes are atomic; multi-file sequences
    (notably the epic-close archive, whose index is directory-count+1) have no transaction/rollback,
    so a crash or concurrent run mid-sequence can leave unrecoverable half-state.

**Soft**

12. **Static context-window capacity constant** — the runtime cannot introspect the agent's context
    window; a config constant kept manually in sync silently miscalibrates the recycle decision when
    it drifts.
13. **Protocol/event shapes pinned to one agent version** — centralized but pinned and flagged for
    live confirmation; the one-shot posture depends on a real input-EOF handshake a wrapper/shim
    would silently break.

---

## 4. Capability Requirements

Ordered by how decisively they shape the language decision.

1. **Expressive sum-type & state-machine modeling with completeness checking.** Dense discriminated
   vocabularies, many validated for mutual completeness at construction — correctness depends on
   *exhaustive, checked* case handling over closed vocabularies.
2. **Value-semantic records with nondestructive update and deep immutability.** Cached deserialized
   graphs are aliased across callers; DTO↔domain mapping requires copy-on-change so shared instances
   are never corrupted.
3. **Robust subprocess lifecycle control.** Spawn, drive, and reliably reap external agent + VCS
   processes: redirected standard streams, **whole-process-tree kill** as the single reaping path,
   bounded wait-for-exit so a stuck child cannot wedge teardown.
4. **Non-blocking concurrent IO and coordination primitives.** Decoupled read pump / sole-writer /
   per-turn consumers; correlation by monotonic id over a concurrent map; interlocked counters;
   volatile state; completion signals with async continuations.
5. **Cooperative, layered cancellation with guaranteed awaiter release.** Per-session source linked
   to the caller's per-turn token; a stream-end path that faults all pending waiters and cancels the
   session source so *no caller ever hangs* — even one that passed no token.
6. **Atomic single-file persistence tolerant of out-of-band writers** (temp-write+rename;
   read/write/delete-share reads with bounded retry; before/after stable-signature guards).
7. **Stable cryptographic content hashing** for content-addressed ledger identities, projection
   freshness/provenance, and fingerprint-keyed decision caching.
8. **Schema-versioned serialization with strict validation and forward migration** (validate on load
   *and* save; explicit schema version; parse and migrate an older tabular format forward on first
   load).
9. **Dependency-injection composition with register-if-absent override** across every seam.
10. **Build-time (or load-time) template-to-code generation with validation** — prompts are a
    compile-time contract: templates become strongly-typed, hash-pinned render units; malformed
    placeholders fail before any prompt reaches the agent; runtime render is pure concatenation with
    zero IO.
11. **Defensive streaming parsers over two framings** (structured protocol + streaming event log),
    surfacing non-protocol lines verbatim rather than dropping them.
12. **Long-duration resumable execution with orderly cancellation** — a run may block for hours
    awaiting a quota reset and must not be hard-killed; interrupts reap child trees and flush partial
    state on non-cancellable salvage paths that swallow their own errors.

---

## 5. Capability Opportunities

Capabilities the system does not yet possess but realistically could — the strategic surface. The
**dominant capability levers** (those that most expand what the project can *become*, and therefore
most influence the language choice) are marked ★.

- ★ **Pluggable multi-agent / multi-provider transport adapters.** Only one agent protocol is
  implemented behind already-normalized seams. Additional adapters turn a single-agent driver into a
  **vendor-neutral orchestration substrate** — the single largest expansion and the one that
  de-risks dependence on one vendor.
- ★ **Fleet-scale coordination across simultaneous loops.** The registry is in-process and state is
  last-writer-wins. Cross-process/distributed scheduling with shared quota throttling and
  leased/versioned state scales from one repository to a portfolio under central control.
- ★ **Transactional, event-sourced durability.** Staging-then-commit for multi-file archives,
  optimistic concurrency on the snapshot, and a replayable hash-chained journal foldable to
  reconstruct/repair state — the trust foundation that unlocks safe parallelism.
- ★ **Live interactive steering surface.** Bidirectional operator control mid-run (inspect, guide,
  redirect) atop the existing decision-submit gate and replayable event streams — shifting the
  product from run-and-spectate to an **interactive cockpit** (the stated roadmap direction).
- ★ **Objective-driven, adaptive cost models.** Token-only cost policy today; money-, latency-, and
  deadline-aware models with learned estimation let loops optimize against business objectives —
  essential once loops compete for shared quota.
- ★ **Runtime-authored, hot-reloadable prompt and declarative-policy packs.** Templating is
  compile-time-only and dispatch is manual; operator-authored, hot-reloadable prompts/policies make
  new workflows feasible without rebuilds and enable a shareable ecosystem.
- **Retry / backoff / circuit-breaking policy layer** (there is no automatic retry anywhere today).
- **Bounded, evictable, durable caches with back-pressure** (decision/byte/deserialization caches
  are unbounded; streaming queues have no flow control).
- **Persisted, tamper-evident audit trail** (rich decision evidence exists but has no durable sink).
- **Asynchronous human-in-the-loop approval workflow** (review-required/absent-gateway collapse to
  hard denial today).
- **Machine-readable structured output + unified command surface** (consolidate three duplicative
  entrypoints; enable dashboards and programmatic integration).
- **Progress watchdog with stall-abort** (progress observation is informational only today).

---

## 6. Performance Requirements

Performance here is almost entirely **IO/streaming- and determinism-shaped**, not
throughput-shaped — the spec explicitly states in-process compute is negligible against agent-turn
and multi-hour quota-wait cost.

- **Read-pump dispatch latency** — strictly non-blocking, O(1) correlation-by-id dispatch + enqueue
  per line; any blocking here deadlocks the turn.
- **Sole-writer output drain** — a single writer drains the unbounded outbound queue under bursty
  output; the deadlock-avoidance keystone.
- **Continuous error-stream draining** — for the child's entire lifetime, independent of turn
  activity, so a verbose turn cannot fill the buffer and wedge.
- **Streaming time-to-first-output** — reply deltas surface turn-by-turn as they arrive; per-chunk
  rendering must not stall the drain.
- **Bounded teardown and exit capture** — disposal completes within a fixed small bound (seconds)
  even against a stuck child; post-EOF exit-capture race bounded by a fixed timeout.
- **Template render cost** — pure string concatenation, zero IO, zero re-parse; all
  parsing/validation paid once at build time.
- **Read-through cache hit rate** — repeated same-invocation reads collapse to byte- and
  deserialization-cache hits keyed by a cheap `(length, last-write-tick)` signature.
- **Freshness-gating accuracy** — regenerate a derived projection only when a causal input's content
  hash/version actually changed; regeneration is an expensive agent turn, so false-fresh/false-stale
  detection is the primary cost lever and must be *exact*.
- **Per-turn economic accounting** — cache-weighted effective-token computation runs cheaply every
  turn; treats cached tokens as a subset of prompt tokens; clamps negatives; never divides by
  zero-work.
- **Permission decision latency** — on the agent's critical path: cached decisions via a
  concurrent-map fingerprint lookup; uncached costs one canonicalization + a versioned field-by-field
  fingerprint hash + rule evaluation, with per-segment filesystem checks proportional only to path
  depth.

**Implication.** The performance that matters is **pause-freedom and bounded latency on the
deadlock-avoidance and teardown paths**, plus exact freshness gating — *not* CPU throughput. A
throughput advantage is nearly worthless here; a determinism (no GC-pause jitter on teardown/dispatch)
advantage is genuinely correctness-adjacent.

---

## 7. Engineering-Effectiveness Requirements

Tie-breakers only, but demanding:

- Schema-version and validate every durable artifact on load *and* save; migrate older formats
  forward on first load.
- Keep on-disk bytes canonical and deterministic so hashes and golden contracts never drift.
- Guarantee exactly-once session teardown and cleanup of all pending waiters on stream-end (close the
  late-registration race).
- Authorization-in-depth: floor merge + redundant post-evaluation invariant guard re-asserting
  non-negotiable denies; assert fixed evaluation-stage ordering at runtime; bypass cache on terminal
  parse failure.
- Deterministic, versioned fingerprints that never collapse security-relevant distinctions.
- Snapshot/restore transaction fidelity for permitted agent writes, incl. exact rollback that deletes
  newly-created files.
- All persistence/cleanup fail-open and best-effort via warning callbacks.
- Self-ignore the state/telemetry directory from the target project's VCS.
- Cooperative cancellation through every async IO path + non-cancellable salvage paths that flush and
  swallow their own errors.
- Correlated observability: append-only transition journal (correlation ids, input hashes, evidence
  paths, outcomes) + rotating line-delimited session telemetry; snapshot authoritative, journal
  audit-only.
- Golden-fixture regression over serialized shapes with cross-consumer verification; enforce
  architectural layering (leaf components reference no higher layers).
- Build-time prompt-template validation (malformed placeholders → build errors); hash-pin per change;
  keep name→render and name→projection dispatch tables in lockstep and fail fast on drift.
- Prompt-injection discipline: fence embedded artifact content as *evidence, not authority*; validate
  generated prompts contain no raw project-context markers.
- Harden usage-limit retry-time parsing (timezone anchoring, past/stale detection, bounded fallback).
- Startup prerequisite doctor with severity-tagged diagnostics; layered config precedence
  (env override → consumer file → bundled default).

---

## 8. Candidate Language Combinations

Seven candidates spanning the plausible design space were each scored 1–10 on capability,
performance, and engineering by an independent blind evaluator against the §2–§7 specification.

### 8.1 Rust (single language) + embedded, capability-confined plugin layer  — cap 9 / perf 9 / eng 8

- **Strengths.** Compile-time algebraic sum types with **exhaustive match** map almost perfectly
  onto the dense discriminated vocabularies — "missing rule/route fails fast" becomes a *compiler
  guarantee*. Ownership/borrow makes **deep immutability of aliased cached graphs a static
  property** (the single best-fit requirement): a shared reference-counted handle to one immutable
  graph is safe by construction; nondestructive update is idiomatic struct-update. The
  within-session concurrency triad maps directly onto a mature async runtime (unbounded/bounded
  channels, concurrent maps for O(1) correlation dispatch, atomic counters, oneshot signals).
  Build-time codegen (build scripts + procedural macros) matches the compile-time prompt contract,
  and the name→render table can be *generated and drift-checked*. Best-in-class deterministic
  serialization; stable content hashing. Single static native binary per tool, near-instant cold
  start, distinct exit code per outcome. **GC-free determinism** meets every fixed teardown/latency
  bound with no pause jitter, and bounded channels make the unbounded-back-pressure gap trivially
  closable. The **embedded capability-confined plugin layer** composes with closed-world
  default-deny: untrusted transport adapters, cost models, and policy packs load into a deny-by-default
  sandbox that cannot escalate, fetch, or shell out unless granted.
- **Weaknesses.** No reflective DI container — the register-if-absent composition across many seams
  is hand-wired via traits/generics (compile-checked but verbose). Async cancellation is *drop-based*,
  not purely token-cooperative, so the "fault all waiters, hang no caller" requirement and
  non-cancellable salvage paths demand careful design around drop-on-cancel. Whole-process-tree kill
  and full file-share-mode control require platform-specific APIs (job objects / process groups).
  Nondestructive update over deeply nested graphs is verbose without a persistent-collection library.
  The native desktop UI is not written in this language (strong fit is the sidecar/backend).
- **Architectural fit.** Maps cleanly onto all three fused subsystems; the embedded-sandbox bet is
  where the safety identity and the top two extension levers align without eroding the deny floor.
  Principal friction is *ergonomic/platform-specific*, not a ceiling.
- **Risks.** Drop-on-cancel state left mid-transition if a future is dropped at an await point;
  cross-platform process-tree-kill and file-share testing burden; embedded-sandbox maturity risk (see
  §12).

### 8.2 CLR-family managed runtime: multiparadigm object language + functional-sibling domain core, compile-time source generators, AOT option  — cap 9 / perf 8 / eng 9

- **Strengths.** Near-exact domain-core fit: the functional sibling's **exhaustively-checked
  discriminated unions and immutable-by-default records** natively serve the densest capability
  requirement (missing-case → compile error). The concurrency profile reads as a *specification of
  this platform's primitives* — channel for the sole-writer drain, non-blocking correlation pump,
  single-permit async semaphore, concurrent map, interlocked/volatile counters, completion signals.
  Idiomatic layered cancellation with guaranteed awaiter release. Argument-array process launch,
  process-tree kill, bounded wait — the injection-safety hinge met precisely. **Compile-time source
  generation** maps 1:1 onto the prompt-template-to-code contract (hash-pinned render units,
  build-failing malformed placeholders, zero-IO render). **Register-if-absent DI is a named ecosystem
  idiom.** High ceiling for roadmap levers: a mature distributed **virtual-actor** framework for
  fleet-scale leased/versioned state; a best-in-class streaming web stack for live steering;
  functional sibling for event-sourced journal folding.
- **Weaknesses.** Deep immutability of aliased graphs is **enforced only when every persisted type
  lives in the functional sibling** — a discipline boundary where one mutable field on an
  object-language record silently corrupts shared cached state. Object↔functional interop seam; the
  exhaustiveness guarantee degrades to best-effort switch analysis when closed vocabularies are
  matched from the object language. Self-contained AOT conflicts with reflection-based
  serialization/DI, forcing source-generated serializers and compile-time registration (achievable —
  the platform provides exactly those tools — but a trim-safety risk surface). Hot-reloadable
  prompt/policy packs cut against the compile-time generator, requiring a second dynamic render path
  that does not inherit build-time validation.
- **Architectural fit.** Exceptionally high; workload makes managed-runtime overheads irrelevant.
  Reservations are conventional (immutability enforcement, boundary friction, AOT/reflection
  trade-offs), none blocking.
- **Risks.** AOT/trimming silently breaking reflective paths; a mutable field corrupting the aliased
  cache; exhaustiveness eroding at the language seam; roadmap levers requiring bolt-on frameworks or
  an ungated second render path.

### 8.3 JVM-family managed runtime: modern statically-typed language with structured-concurrency coroutines (optional functional sibling)  — cap 8 / perf 7 / eng 8

- **Strengths.** **Structured-concurrency coroutines** are a near-ideal match for the intricate
  intra-session model and the "no awaiter ever hangs" cancellation requirement; channels give a
  natural sole-writer/non-blocking-pump decomposition. The platform's **distributed-systems ecosystem
  is the strongest lever for the single biggest *unmet* need — fleet-scale coordination** (leases,
  versioned/optimistic state, shared-quota throttling, scheduling). First-class **reactive-streams
  back-pressure** closes the flagged unbounded-queue gap and underpins replayable server-push +
  live-steering. Mature DI + build-time symbol processing; native argument-array subprocess control +
  process-tree kill; sealed hierarchies with exhaustive analysis; off-the-shelf event-sourcing.
- **Weaknesses.** Deep immutability and **byte-stable serialization are convention, not guarantee**
  (default serializers do not canonicalize ordering/BOM/newline/culture). The **native desktop shell
  is the platform's softest spot**. Atomic-rename + share-retry semantics need platform-aware care.
  The optional functional sibling adds a two-toolchain interop cost.
- **Architectural fit.** Excellent on the two hardest concurrency axes (intra-session + the unmet
  cross-process coordination); friction is in unenforced guarantees and the desktop edge.
- **Risks.** A nested-mutable field or non-canonical serializer silently corrupting the cache /
  drifting hashes; per-repository multi-process footprint; native-image mitigation conflicting with
  reflection-heavy libraries; desktop shell requiring a non-platform component.

### 8.4 Functional-first strongly-typed compiled language (ML/Haskell family) with ADTs + exhaustive pattern matching  — cap 8 / perf 7 / eng 6

- **Strengths.** **Exhaustive pattern matching over ADTs** is a near-exact match for the #1 capability
  requirement — the strongest possible "missing rule/route fails fast." **Immutability-by-default**
  structurally satisfies the aliased-cache and nondestructive-update constraints almost for free. The
  transactional/event-sourced-durability lever maps onto pure reducers over immutable state (canonical
  functional substrate); STM in some members directly supplies the missing optimistic-concurrency
  primitive. Compiler/DSL strength serves template codegen, the two defensive streaming parsers,
  format migration, and a DSL-hosted hot-reloadable prompt/policy lever. Property-based testing is
  ideal for serialization-shape and fingerprint contracts.
- **Weaknesses.** **Thin, less battle-tested ecosystem for the literal core of the system** —
  cross-platform subprocess-tree spawn/reap, duplex-pipe deadlock avoidance, error-pipe draining —
  so the *most safety-critical* code would rest on bespoke FFI. Weak native-GUI story (mitigated: the
  shell is already separated behind an API boundary). No native hot-reload of compiled code (must be
  DSL-interpreted). The DI register-if-absent idiom is less conventional (functors/typeclasses).
  Concurrency fit is family-member-dependent.
- **Architectural fit.** Exceptional for the correctness-dominant heart; the integration/OS surface —
  which *is* the product's identity — lands precisely on the family's thinnest libraries.
- **Risks.** Betting the subprocess-reliability core on hand-built FFI; space leaks across multi-hour
  runs in a lazy member; async-ecosystem fragmentation; native-shell has no strong path.

### 8.5 Hybrid: native, static, memory-safe systems core + gradually-typed scripting frontend across a stable API  — cap 7 / perf 8 / eng 6

- **Strengths.** The spec's own topology already prescribes a two-tier split (backend sidecar + native
  shell), so the hybrid maps ~1:1 onto deployment reality. Every load-bearing correctness/concurrency
  constraint lives in the native core (best-in-class there). The scripting half directly raises two
  dominant levers — live interactive steering (richest interactive-UI ecosystem) and hot-reloadable
  prompt/policy packs (dynamic loading). Hot paths stay native; the scripting runtime's slowness is
  off the critical path.
- **Weaknesses.** Introduces a **correctness-sensitive marshaling seam exactly where the spec is most
  exacting** — exact numeric-vs-string correlation-id echo, byte-stable canonical bytes, deep
  immutability of aliased graphs. The "AI-SDK glue" rationale is weak: the agent is driven as a
  held-open subprocess over OS pipes (a systems concern), not high-level SDK territory. **Three of six
  dominant levers are pure native-core work** the scripting half cannot touch — boundary cost without
  capability gain across half the strategic surface. Two toolchains double the engineering tax;
  gradual typing leaves static-analysis holes on the glue consuming exactly-once replay.
- **Architectural fit.** Fits the coarse topology well; carries real boundary risk and contributes
  nothing to roughly half the strategic surface.
- **Risks.** Marshaling silently coercing correlation-id type / dropping byte-stability; domain logic
  leaking into the mutable scripting layer; API boundary becoming a lockstep-evolution chokepoint;
  effort concentrating on the UI ceiling while the correctness core gets proportionally less.

### 8.6 Dynamically-typed scripting runtime with a gradually-typed superset, event-loop async, native addons  — cap 7 / perf 7 / eng 8

- **Strengths.** **Directly amplifies the #1 lever** — native access to the richest external-agent SDK
  ecosystem makes new transport adapters the cheapest to ship behind the normalized seams. Runtime
  code loading makes **hot-reloadable prompt/policy packs and live steering architecturally natural**.
  The event-loop model is an excellent structural match for the non-blocking pump / sole-writer drain
  / turn-by-turn streaming, and single-threading removes data races on the correlation map. Weak CPU
  throughput is nearly cost-free here. Best-in-class iteration speed and desktop-shell/sidecar tooling.
- **Weaknesses.** **Erasable, unsound gradual typing cannot runtime-enforce** the exhaustiveness, deep
  immutability, and default-deny guarantees the spec calls the product identity — they must be
  reconstituted via hand-written validators/immutability libraries. Byte-stable serialization is not
  the default emit behavior. The **non-preemptive event loop** means a blocking/untrusted chunk
  consumer can *wedge the entire loop* — in direct tension with insulating the transport from a
  misbehaving callback.
- **Architectural fit.** Strong on the IO-bound, streaming, interop-heavy core and the two largest
  levers; the trust foundation (durability, non-collision fingerprints, invariant guards) is defended
  by convention rather than the type system.
- **Risks.** In-place mutation of a shared graph corrupting state; non-canonical serialization
  poisoning hashes; a blocking callback stalling the loop; an unhandled case slipping past the deny
  floor; unbounded queues + GC growth under fast-producer/slow-consumer.

### 8.7 Go (single language)  — cap 6 / perf 9 / eng 8

- **Strengths.** **Concurrency is a near-perfect fit for the keystone constraint** — goroutines +
  channels + `select` make the non-blocking pump / sole-writer drain / error-drainer triad *idiomatic*,
  and buffered channels give natural back-pressure exactly where the spec flags unbounded queues.
  `context.Context` is a first-class idiom for the layered cancellation requirement. **Structural
  interfaces are best-in-class for the dominant extensibility lever** — transport adapters and every
  seam drop in with no registration ceremony. First-class subprocess/IO control; easy deterministic
  byte-stable encoding (sorted map keys, byte-wise comparison). The **race detector** turns the
  three-role concurrency's invariant violations into reproducible failures. Fast static binaries, cheap
  goroutines for the idle multi-hour sidecar and in-process fleet-of-loops.
- **Weaknesses.** **No algebraic sum types, no compile-time exhaustiveness** — the dense discriminated
  domain must be emulated via interface+type-switch or const-enum+validation, so adding a variant does
  *not* fail the build at unhandled sites; the safety net is tests/init-validation, not the type system.
  **No language-enforced deep immutability** — nested slices/maps/pointers stay mutable; the
  aliased-graph invariant rests on discipline + defensive copying. **Weak native-desktop/GUI story**
  under a single-language mandate. Verbose nondestructive-update ergonomics; no portable dynamic
  code-plugin story on the workstation OS; manual discriminated-union serialization.
- **Architectural fit.** Fits the *runtime spine* extremely well and the *domain-correctness identity*
  only adequately — it lacks exactly the two guarantees (tagged-union exhaustiveness, enforced
  immutability) the spec elevates, and the single-language mandate caps the native desktop shell.
- **Risks.** Correctness regressions from non-exhaustive case handling shipping silently; latent
  shared-state corruption from an accidental in-place mutation; high-risk single-language desktop
  shell; drift-prone manual dispatch tables; platform-specific process-tree-kill/file-share care.

---

## 9. Comparative Analysis

Applying the lexicographic rule sorts the field into four **capability tiers**. Capability does the
real separating work; performance and engineering only order candidates already judged comparable on
ceiling, and never override a weaker ceiling.

| Tier | Candidates (capability score) |
|---|---|
| **A (cap 9)** | Rust + embedded sandbox · CLR-family (object + functional sibling) |
| **B (cap 8)** | JVM-family (structured concurrency) · Functional-first (ML/Haskell) |
| **C (cap 7)** | Hybrid (native core + scripting frontend) · Dynamically-typed scripting |
| **D (cap 6)** | Go |

**Tier A.** Both satisfy the densest capability requirement (checked sum-types with construction-time
completeness) natively, and both realize the compile-time prompt contract, register-if-absent seams,
argument-array subprocess invocation, and the deadlock-avoidance triad. They separate on the two
things the spec elevates to *product identity* — correctness and safety. Rust makes the single most
load-bearing correctness constraint (deep immutability of aliased cached graphs) a **static compiler
guarantee**; the CLR-family enforces it only when every persisted type lives in the functional sibling,
leaving a discipline boundary where one mutable field silently corrupts shared state. Rust's embedded
confined plugin surface is a **structural synergy with closed-world default-deny** (untrusted
extensions load into a boundary that cannot escalate/fetch/shell), whereas the CLR-family's hot-reload
path bifurcates into an ungated runtime render. The CLR-family's genuine edges — a mature
distributed virtual-actor framework for the unmet fleet lever, a best-in-class streaming web stack for
live steering, register-if-absent DI as a native idiom — are largely **lower-*effort* routes** to
capabilities Rust can also reach, i.e. engineering-effectiveness advantages (the *last* tiebreaker),
not ceiling advantages. With capability comparable-to-slightly-favoring Rust, the performance key
(9 vs 8; GC-free execution meeting every fixed teardown/latency bound with margin) settles it:
**Rust #1, CLR-family #2.**

**Tier B.** The JVM-family and the functional-first family each maximize a *different half* of the
strategic surface. Functional-first has the higher correctness-*modeling* ceiling (compile-time
exhaustiveness + immutability-by-default rival Rust on the domain core, make event-sourced durability
idiomatic) — but its ecosystem is thinnest **exactly at the system's fundamental identity**, the
subprocess-reliability wrapper, where the most safety-critical code would rest on bespoke FFI. JVM is
near-ideal on the intra-session concurrency keystone and **owns the top unmet lever** (fleet-scale
coordination) plus reactive back-pressure, at the cost of immutability and byte-stable output being
convention-not-guarantee. Comparable on ceiling and tied on performance (7/7), the final key —
engineering — separates them: JVM's turnkey libraries (8) beat functional-first's bespoke-integration
burden (6). **JVM #3, Functional-first #4.**

**Tier C.** Both the explicit hybrid and the scripting runtime unlock the transport, hot-reload, and
live-steering levers cheaply, but neither raises the correctness ceiling. The scripting runtime cannot
runtime-enforce exhaustiveness, deep immutability, or the default-deny identity under erasable gradual
typing. The hybrid keeps those guarantees inside its native core but introduces a correctness-sensitive
marshaling seam atop the spec's most fragile invariants and contributes nothing to three of six
dominant levers. The performance key separates them: the hybrid's native core carries the hot paths
(8) vs a single-threaded event loop a blocking consumer can wedge (7). **Hybrid #5, scripting #6.**

**Tier D.** Go is the clearest illustration of the no-rescue rule. Its runtime spine is excellent and
it ties for **best performance (9)** with solid engineering (8). But the absence of algebraic sum
types, compile-time exhaustiveness, and enforced immutability collides head-on with the correctness
identity, pushing "missing rule/route fails fast" and aliased-graph safety down to runtime validation,
while the single-language mandate caps the native desktop shell. A capability ceiling of 6 keeps Go
last despite category-leading performance — exactly the outcome the lexicographic rule demands.

### Adversarial check (and how it refines the recommendation)

A dedicated skeptic attacked the #1 pick. Its strongest points, and the resolution:

- **The plugin layer partly fights itself on the #1 lever.** Transports are OS-level subprocess
  lifecycle + held-open bidirectional pipes; a capability-confined WASM component cannot hold those
  pipes or reap process trees — it must proxy every syscall to the host. **Resolution:** keep
  *pipe-holding transports as native trait seams* and reserve the confined plugin surface for
  cost models, policy packs, and prompt packs. This *narrows* the plugin story rather than negating
  it.
- **Two "engineering" edges are actually spec-named *capability* requirements** — register-if-absent
  DI and build-time template-to-code generation. The CLR-family serves both *natively*; Rust serves
  them *bespoke* (hand-wiring; build scripts/macros). So the capability tie at 9 is **soft**, and the
  CLR-family's fleet-coordination edge (a mature distributed virtual-actor runtime packaging solved
  leasing/versioned-state correctness) is more than "less effort" — hand-rolling it in Rust
  reintroduces distributed-correctness risk on the largest scale lever.
- **The decisive 9-vs-8 performance margin sits on throughput, which the spec calls negligible.** The
  margin matters *only* where pause-freedom bleeds into the deadlock keystone and bounded teardown —
  which is genuinely correctness-adjacent, but is not a throughput story.

**Verdict:** the Rust choice **survives**, but its rationale must change. It wins **not on the
performance tiebreaker** but *structurally on the correctness identity*: ownership makes deep
immutability of aliased cached graphs enforceable (a strong constraint the runner-up meets only by
discipline), and gives pause-free teardown. Accordingly, demote the performance argument and **narrow
the plugin story to native seams for pipe-holding transports and a single execution model** (not
"WASM plus interpreter"), shrinking the very security surface the spec makes the identity.

---

## 10. Final Ranking

| Rank | Candidate | Capability | Performance | Engineering | One-line verdict |
|---:|---|:---:|:---:|:---:|---|
| **1** | **Rust + embedded capability-confined plugin layer** | **9** | **9** | **8** | Highest ceiling and top performance; compiler-enforced sum types + a **static** deep-immutability guarantee for aliased caches, plus a deny-by-default confined extension surface that expands the levers without breaching the safety identity. |
| **2** | CLR-family managed (object + functional-sibling domain core, source generators) | 9 | 8 | 9 | Capability-equal runner-up with the best engineering ergonomics and strong roadmap frameworks, but enforces the load-bearing aliased-graph immutability only *by discipline* and loses the performance tiebreak. |
| **3** | JVM-family (structured-concurrency coroutines) | 8 | 7 | 8 | Near-ideal on the intra-session concurrency keystone and best-in-class on the top *unmet* fleet-scale lever, but immutability & byte-stable output are convention-not-guarantee; wins Tier B on engineering. |
| **4** | Functional-first compiled (ML/Haskell family) | 8 | 7 | 6 | Best-in-class correctness core undercut by a thin ecosystem *exactly* at the subprocess-reliability identity that **is** the product; loses Tier B on engineering. |
| **5** | Hybrid: native core + scripting frontend | 7 | 8 | 6 | Fits the coarse topology and preserves correctness in the native core, but a type-lossy marshaling seam sits atop the most fragile invariants and it aids only half the levers; edges scripting on performance. |
| **6** | Dynamically-typed scripting (gradually-typed superset) | 7 | 7 | 8 | Cheapest path to the transport, hot-reload, and live-steering levers, but erasable typing cannot enforce the exhaustiveness, immutability, and default-deny guarantees the spec calls the product identity. |
| **7** | Go | 6 | 9 | 8 | Excellent runtime/concurrency spine and category-leading performance, but no sum types, no enforced immutability, and a weak single-language UI story cap the correctness ceiling — the textbook case of strong perf/engineering failing to rescue a lower ceiling. |

---

## 11. Recommendation

**Build the system in Rust as a single language, with an embedded, capability-confined plugin layer
(WASM component model, with an embedded-interpreter fallback) reserved for cost/policy/prompt
extensions — and keep pipe-holding transport adapters as native trait seams.**

**Why it best satisfies the ordered priorities:**

1. **Capability (dominant).** It converts the spec's two most load-bearing correctness properties
   from *conventions* into *guarantees the compiler and ownership model enforce*: deep immutability of
   aliased cached graphs shared across many callers (in-place mutation is statically impossible on a
   shared borrow), and exhaustive case handling over dense closed vocabularies (a missing rule/route
   is a **build error**, not a runtime fault caught by validation or tests). These are precisely the
   two guarantees the spec elevates to *product identity*. It fully covers the intricate
   deadlock-avoidance concurrency (sole-writer drain, non-blocking correlation pump, single-permit
   gate, bounded exactly-once teardown) and imposes **no ceiling** on the fleet-scale, event-sourced
   durability, or interactive-steering levers. Its embedded confined extension surface composes with
   closed-world default-deny so the hot-reloadable prompt/policy and pluggable cost-model levers can
   expand the system while untrusted code loads into a boundary that cannot escalate, fetch, or shell
   out — the non-negotiable denies survive.

2. **Performance.** Deterministic, GC-free execution meets every fixed teardown/latency bound with
   margin and eliminates pause jitter on the deadlock-avoidance and teardown critical paths; bounded
   channels close the unbounded-back-pressure gap natively. *(Note: this is a determinism/latency
   argument on the correctness-adjacent paths — not a throughput argument, which the workload makes
   nearly irrelevant.)*

3. **Engineering effectiveness.** The weakest of Rust's three axes (still an 8): register-if-absent DI
   is hand-wired rather than reflective, and the template-to-code contract is served by build
   scripts/macros rather than a first-class generator. These are real ergonomic costs — but they are
   the *last* tiebreaker and do not touch the capability ceiling.

**Runner-up and why it does not win.** The **CLR-family managed runtime** ties on capability (9) and
posts the best engineering score (9), with genuinely superior lower-effort paths to two roadmap levers
(a distributed virtual-actor framework for fleet-scale coordination; a best-in-class streaming web
stack for live steering) and register-if-absent DI as a native idiom. It loses for two reasons
consistent with the priority order: **(1)** on the correctness/safety identity, it enforces deep
immutability of aliased cached graphs only when every persisted type lives in the functional sibling —
a discipline boundary where a stray mutable field silently corrupts shared state — and its hot-reload
lever bifurcates into an ungated runtime render path; **(2)** its standout advantages are largely
engineering-*effectiveness* (lower effort to reach capabilities Rust can also reach), which is the
last tiebreaker and cannot outweigh Rust's capability parity-plus-edge and its win on the first
tiebreaker. *(A fair caveat from the adversarial pass: two of the CLR-family's edges — native DI and
native template codegen — are actually spec-named capability requirements, and its packaged
distributed-actor correctness is more than mere effort. This makes the capability tie genuinely soft
and is the primary reason confidence is held at medium, not high.)*

**On hybrids.** A *frontend/backend language hybrid* is **not** justified (it ranks only #5): splitting
the correctness core from a separate gradually-typed frontend introduces a type-lossy marshaling seam
exactly where the spec is most exacting (numeric-vs-string correlation-id echo, byte-stable canonical
bytes, deep immutability of aliased graphs), roughly doubles the engineering surface, and aids only
half the levers. **However**, the recommended candidate is itself a *controlled, in-process hybrid*: a
single memory-safe core with an embedded confined plugin layer — capturing the one real benefit of a
two-tier split (safe operator-authored, hot-reloadable extensions and a multi-provider ecosystem)
**without** a cross-language marshaling boundary, because the plugin edge is a confinement boundary
*inside one runtime* carrying no domain vocabulary across a lossy seam. The native desktop presentation
shell remains a separate UI toolchain for *every* candidate, consuming the backend over the
already-specified API/event-stream boundary — a pre-figured architectural seam, not a language hybrid,
and it does not change the recommendation.

---

## 12. Confidence Assessment

**Confidence: Medium.**

**What makes the #1 pick robust.** Rust leads on the primary key (capability ceiling — via a static
immutability guarantee for aliased caches and a deny-by-default confined extension surface, both
aligned to the safety/correctness the spec names as product identity) **and** on the first tiebreaker
(performance), so it wins whether its capability is judged strictly higher than or merely comparable to
the runner-up's. The bottom of the table is equally firm: Go's capability ceiling of 6 keeps it last
despite the best performance — precisely the no-rescue outcome the lexicographic rule mandates.

**What holds confidence to medium (what would change the ranking):**

1. **Framework maturity as ceiling vs effort.** If mature framework support for fleet-scale
   coordination and live steering were counted as capability *ceiling* rather than engineering
   *effort*, the CLR-family could edge above Rust and take #1. This audit classified those as effort
   per the lexicographic definition (they are capabilities Rust can also reach), which favors Rust —
   and the adversarial pass legitimately contests that classification for packaged distributed-actor
   correctness.
2. **Embedded-sandbox maturity risk.** If the embedded-plugin maturity risk were judged severe with no
   acceptable interpreter fallback, Rust's *realized* capability drops and the Rust↔CLR gap narrows or
   inverts. The recommendation's mitigation — narrowing the confined surface to cost/policy/prompt
   packs and keeping pipe-holding transports native — deliberately reduces this exposure.
3. **Tier B is sensitive.** JVM vs Functional-first flips if the correctness-*modeling* ceiling is
   weighted decisively over the subprocess-reliability/intra-session-concurrency identity (judging
   functional-first's capability strictly higher than JVM's), in which case it ranks above JVM
   regardless of the engineering tiebreak.
4. **Reweighting the dominant axis.** If the roadmap levers (fleet-scale, live steering) were reweighted
   as *the* dominant capability axis over the correctness identity, the whole middle of the table
   compresses toward the managed/JVM platforms.

**Net.** The recommendation is stable across every reasonable reading of the ordered priorities; the
residual uncertainty is concentrated in *how strictly to separate "capability ceiling" from
"engineering effort"* for framework-provided capabilities — the one interpretive choice that, resolved
the other way, would elevate the managed CLR-family runner-up to first.

---

### Appendix — Audit provenance

Fan-out audit over the system's subsystems: parallel language-neutral readers (orchestration/loop
engine; domain core + event-sourced projections + completion; permissions/trust/version-control/
external-process integration; CLI surface + prompt templating + code generation; stated intent &
roadmap) → a synthesized first-principles specification → seven independent, blind candidate
evaluations against that spec → adversarial adjudication with a skeptic pass on the top pick. Fifteen
agents in total; one subsystem reader failed its structured-output retries, and its scope (stated
future direction) was recovered in synthesis — all seven candidate evaluations and the full
specification completed. No step referenced, inferred, or reasoned from the system's actual
implementation language, build system, tooling, or dependency ecosystem.
