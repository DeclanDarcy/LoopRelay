# Implementation Preservation Platform — Architectural Opportunity & Integration Triage

**Date:** 2026-07-07
**Inputs:** current repository at HEAD `442b8e94`; `audit.md` (treated as evidence, not authority — every load-bearing claim independently re-verified against HEAD); `semantic-constitution.md`; `secondary-state-machine-audit.md`; `third-state-machine-audit.md`; `state-machine-refactor-audit.md`; new issues 009/010.
**Repository delta since `audit.md`:** three commits (`6f9f9986`, `d58ef892`, `442b8e94`) — verified documentation-only (`git diff ef41ac15..HEAD -- '*.cs'` is empty). Code claims re-validated at HEAD by two adversarial passes; the new documents were synthesized as newer architectural understanding.
**Nature of this document:** an observation inventory. It classifies likely timing for each opportunity (Pre-Implementation / Implementation / Immediate Follow-up / Deferred) but does not sequence, estimate, decompose, or prescribe solutions. The roadmap remains the authority for converting these observations into strategy.

---

## 1. Validation of `audit.md`

Every `audit.md` finding was checked against HEAD. Verdict summary, corrections first.

### 1.1 Corrections (claims refuted or materially refined)

| audit.md claim | Verdict | Independent evidence |
|---|---|---|
| File classification "is the promotion-boundary classification generalized (shared lineage, single policy authority)" (F-1.3, F-2.1, F-11.2) | **REFUTED as unification; valid as pattern** | `ArtifactPromotionService` (`ArtifactPromotion.cs:72-129`) classifies **structural validity/disposition of one agent-authored candidate** (`ArtifactOutputKind {Promotable, Blocked, Malformed, Ambiguous}`) via a **per-call injected classifier** — orthogonal to a semantic implementation-vs-non-implementation file-role axis. The two share only the classify → route → preserve-evidence *shape*, not taxonomy, inputs, or a single policy authority. Consequence: the opportunity is pattern reuse, **not** a merged classifier (see OPP-7, Risk R-2). |
| Dead prompts include `GenerateSystemPromptForFirstExecutionAgent` (F-12.5) | **PARTIALLY VALID** | It has a **live call site** (`DecisionSession.cs:124`, rendered when no handoff exists). `technical-debt.md` TD-9 argues that path is loop-unreachable (first execution bypasses the decision session), so runtime reachability is contested — but it is not confirmably dead. `GetNextDecisions` and both `StartDecisionSession*` prompts remain confirmed dead (zero non-comment references). |
| Prompt corpus "~20,133 lines" (F-6.2) | **CORRECTED** | 41 `.prompt` files confirmed; actual total is **13,404 lines**. The 12 `ProjectionFor*` mirrors and the duplication argument stand. |
| Ledger doctrine "JSON source + MD projection" (F-9.1) | **REFINED** | `DecisionLedgerStore` writes **JSON only**; `.agents/decision-ledger.md` is read once for legacy migration, never written (`DecisionLedgerStore.cs:82-116`). A Markdown projection for a preservation ledger would be *new* behavior justified by the review workflow, not existing doctrine. |
| Scoped-operation pattern is "ready-made" for preservation turns (F-4.2, F-10.1) | **REFINED — now carries fresh debt** | New issues **009** (sandbox-copy fallback removed before app-server approval behavior was live-certified; proving tests skipped) and **010** (`CodexPermissionAdapter.ExtractPathArguments` treats document *content* — markdown links, `.md` suffixed strings — as paths, causing `OperationPermissionHandler` to nondeterministically decline valid scoped writes) landed after `audit.md`. The substrate the platform would reuse is currently uncertified and flaky (see OPP-3). |
| "Dual" completion orchestration (F-12.1) | **SHARPENED** | The archive implementation is a **single shared** `ICompletedEpicArchiveService`; what is duplicated is the certify→validate→route→archive→update-context *sequencing*, in **~three** places: `CompletionCertificationService.cs:162-260`, `RoadmapStateMachine.RunCompletionCertificationAsync` (`:1052-1110`), and `RecoverCompletionCertificationAsync` (`:320-333`), plus duplicated context-update helpers. |
| Provenance pattern exists "as `ProjectionProvenance` and `DerivedArtifactProvenance`" (F-9.2) | **SHARPENED — three copies** | `Roadmap.Cli/ProjectionProvenance.cs` is a near-verbatim copy of the Projections trio, and `DerivedArtifactProvenance` is a **superset** (content-hash drift, `Superseded` lifecycle, ~23 vs 7 stale reasons). Consolidation folds a 3× diff engine but must preserve genuine policy differences — savings are partial (see OPP-4). |
| Injection via optional placeholder is versioned "because SourceHash covers prompt edits" (F-6.1, implied) | **REFINED — provenance gap found** | `SourceHash` covers the **raw template only**; placeholder *input* is never hashed. No rendered-prompt hash exists anywhere (telemetry records char/byte counts only). The projection subsystem hashes its one `projectContext` input; generic runtime prompts hash nothing injected. Injected Implementation-First guidance would be **provenance-invisible** without a new recording obligation (see OPP-9, Risk R-5). |
| `TempSandboxWorkspaceFactory` provides "isolated seeded workspaces" (F-4.2) | **REFINED** | The factory creates an **empty** workspace (`TempSandboxWorkspaceFactory.cs:12-38`); seeding is entirely the caller's responsibility. Any preservation-analysis turn design must own its seeding contract explicitly. |
| Platform as "shared library like LoopRelay.Completion" (F-8.1) | **REFINED by newer understanding** | The secondary/third audits and `semantic-constitution.md` supply a capability/protocol vocabulary that reframes (without contradicting) this: the platform is a **capability** — declared purpose, owned decisions/evidence/artifacts/invariants/recovery semantics (constitution Article 15) — whose *code shape* may well be a shared library. See §1.3. |

### 1.2 Confirmations (spot-checked adversarially at HEAD; all held)

- Archive is destructive-before-synthesis with no staging/rollback; `epic.md` copied, everything else moved (`CompletedEpicArchiveService.cs:55-70`). Issue 004 present.
- No durable epic-completion marker; post-archive rerun falls through to execution against a moved plan (`LoopRunner.cs:34`, `MilestoneGate.cs:87-97`). Issue 005 present.
- Archive index is `directories.Count + 1` with hard-blocking collision guards (`CompletedEpicArchiveService.cs:43-52,85`). Issue 006 present.
- `WorkingTreeChangeDetector` returns paths only, filters `.agents/`, has exactly two consumers (`WorkingTreeChangeDetector.cs:23-34`; `ExecutionStep.cs:83`; `CommitGate.cs:58`). A detector-anchored classifier **cannot see `.agents/` files** — this forces the scope decision in U-2.
- Zero interactive HITL: decision proposals auto-persist verbatim (`DecisionSession.cs:102-103`); `ILoopConsole` is output-only; the sole `ReadKey` is a pause-to-exit.
- One-shots bypass the permission gateway (`AgentRuntime.cs:78-84`); redirection is not rejected and `echo/cat/tee/printf/find` are safe-listed, so `echo x > file.md` auto-approves (issues 007/008 present); `SettingsDocument` is permissions-only and binary-directory-relative; Roadmap CLI wires no policy (issue 003); `--elevated` parses into `RoadmapExecutionOptions` but never reaches the state machine, and `RoadmapExecutionBridge` is dead code.
- Persistence is 100% file-based (zero SQLite/Dapper); `.LoopRelay/` self-gitignores; `.agents/` is the committed submodule; `StructuredDocumentStore` vs `StructuredJsonDocumentStore` are near-identical (differing only in exception type/options/visibility); JSON options and the Markdown table parser are byte-duplicated per assembly; `TransitionJournalStore` rewrites the whole file per append while the telemetry sink true-appends.
- Zero `Reasoning*/Insight*/Knowledge*/Preservation*` types in `src/`; `SynthesizeCompletedEpic` is a free-form synthesis with a Source Disposition section run at archive time; the `{details}` empty-string idiom and the WritePlan variant-consolidation precedent both stand.

### 1.3 Superseded-by-newer-understanding

The secondary and third state-machine audits (post-`audit.md`) are pure architecture-recovery documents about the Roadmap CLI — grep-confirmed to say **nothing** about preservation, insight, HITL, or Implementation-First. Their effect on `audit.md` is a **vocabulary refinement**, not contradiction:

- They recover a canonical machine (21 states, 32 transitions, 6 archetypes) and a canonical architecture: **9 capabilities** (Run Control, Machine Memory, Strategic Memory, Initiative Selection, Epic Formation, Preparation Materialization, Execution Disposition, Completion Certification, Evidence Recovery), a 16-field **canonical transition contract**, **4 decision classes**, **13 artifact families**, **9 invariant categories**, and an evidence lifecycle in which validated, bound evidence can become transition authority.
- `semantic-constitution.md` compresses all of this into 16 semantic primitives and states explicitly that **preservation platform concepts map onto existing primitives** (authority, evidence, artifact, decision, recovery, distillation) — no new primitives. Its Distillation Pattern names "preservation insight extraction" as an instance; Article 15 defines the entry contract any new capability must satisfy; Law 5 ("permission to perform an action is not authority to accept the result") is the constitutional grounding for `audit.md`'s post-mutation enforcement posture; Law 17 ("artifact deletion is a lifecycle event, not erasure of semantic history") is the preservation doctrine itself.
- Net reading: `audit.md`'s "platform decomposes onto existing lifecycle points" and the constitution's capability model **agree** — the coherent synthesis is *one owning capability, decomposed attachment*: classification surfaces as a Decision class at a boundary; the ledger is Machine-Memory-shaped record-keeping; HITL review is an Evidence-Recovery-shaped flow.

All other `audit.md` findings (staleness map, in-flight plan collisions, settings gap, prompt-injection idiom, MVP boundaries, future-evolution mapping) were re-checked and remain **valid**.

---

## 2. Opportunity Inventory

Grouped by likely timing. Each entry: Observation / Validation / Why it matters / Architectural opportunity / Potential benefits / Potential risks / Likely timing / Confidence.

### 2.1 Pre-Implementation Opportunities

#### OPP-1 — Completion close/archive safety (staging, durable completion marker, stable indexing)

- **Observation.** The archive boundary — the platform's primary attachment point — performs irreversible moves before synthesis, has no durable completion marker (reruns re-execute against a moved plan), and computes indices by directory count (collides after gaps).
- **Validation.** Confirmed at HEAD (issues 004/005/006 all present; `CompletedEpicArchiveService.cs:43-85`, `MilestoneGate.cs:87-97`).
- **Why it matters.** Every preservation stage added here (analysis, extraction, ledger finalization, review gate) widens an already-open data-loss window and inherits non-resumability.
- **Architectural opportunity.** Make the close/archive flow copy-first/staged, idempotent, and gap-tolerant, so completion becomes a boundary that additional stages can attach to without multiplying failure surface — and so preservation analysis always runs against live, intact inputs.
- **Potential benefits.** Removes the largest data-loss risk in the system independent of the platform; converts the most overloaded transition (the secondary audit's T24 close route) into a safe extension point; a durable completion marker also fixes the loop's false "idempotent" claim.
- **Potential risks.** Touches subtle prose-documented invariants (re-execution guards, milestone gate semantics); must not silently change certification routing.
- **Likely timing.** **Pre-Implementation.**
- **Confidence.** High.

#### OPP-2 — Completion certification orchestration convergence (capability-ownership restoration)

- **Observation.** The certify→validate→route→archive→update-context sequencing exists in ~three copies (Main CLI service, roadmap state machine, roadmap recovery path); only the archive implementation is shared.
- **Validation.** Confirmed and sharpened at HEAD (see §1.1). The third audit independently frames Completion Certification as a bounded capability (C8) that should own closure as one boundary — the duplication is a capability-ownership breach in its vocabulary.
- **Why it matters.** Preservation stages at completion would otherwise be inserted three times, with three divergence risks; the recovery copy is the one most likely to be forgotten.
- **Architectural opportunity.** Converge the sequencing into the Completion subsystem as the single owner, with the state machine and loop as invokers — restoring the capability boundary that already exists for policy/router/parser components.
- **Potential benefits.** Single attach point for the platform; reduces the roadmap mega-coordinator by one responsibility (aligned with the recovery audits' direction without undertaking the full decomposition); recovery path stops drifting.
- **Potential risks.** The roadmap path has genuinely different context (projections, journaling, lifecycle persistence) — convergence must parameterize those, not flatten them; risk of shifting complexity into an awkward shared abstraction if the two callers' needs are forced into one signature prematurely.
- **Likely timing.** **Pre-Implementation.**
- **Confidence.** Medium-high (validity high; the exact convergence boundary needs design care).

#### OPP-3 — Scoped-operation substrate certification and path-extraction repair (issues 009/010)

- **Observation.** The operation-scoped agent-turn substrate (`OperationPermissionProfile` + `ArtifactMutationTransaction` + app-server approvals) — which `audit.md` designates as the execution shape for preservation-analysis and insight-extraction turns — lost its sandbox-copy fallback before live protocol certification (009), and its permission adapter extracts document *content* as paths, nondeterministically declining valid scoped writes (010).
- **Validation.** New since `audit.md`; both issues verified to implicate `CodexPermissionAdapter.cs` / `OperationPermissionHandler.cs` and the current unconditional app-server routing in `PlanPipeline`/`DecisionSession`.
- **Why it matters.** The platform would become the substrate's heaviest new consumer. Building preservation turns on an uncertified, flaky permission path bakes nondeterministic failures into preservation analysis — and preservation failures at pruning boundaries are exactly where flakiness is least tolerable.
- **Architectural opportunity.** Certify the app-server approval protocol with live tests and fix path extraction (structural approval-payload parsing rather than string heuristics), making the scoped-operation pattern trustworthy for all consumers.
- **Potential benefits.** Directly benefits existing pipelines (milestone extraction and document optimization are the currently affected operations); removes a reliability tax from every future scoped operation.
- **Potential risks.** Protocol certification depends on Codex app-server behavior stability; scope creep into the broader permissions-manager ambition (keep it to certification + extraction fix).
- **Likely timing.** **Pre-Implementation.**
- **Confidence.** High on validity; medium-high on timing (a narrow fix could alternatively land early inside implementation, but the platform's dependence argues for before).

#### OPP-4 — Structured persistence and provenance primitive consolidation (minimal scope)

- **Observation.** Three near-identical provenance/freshness trios, two near-identical structured JSON stores, and byte-duplicated JSON options and Markdown parsing exist across assemblies. The preservation ledger and settings document would otherwise become the fourth and fifth copies.
- **Validation.** Confirmed at HEAD, with the correction that `DerivedArtifactProvenance` is a genuine superset (content-hash drift, supersession lifecycle, richer stale reasons) — consolidation is real but partial; policy differences must survive as configuration, not be flattened.
- **Why it matters.** The ledger's invalidation/versioning story *is* this pattern; adding it against a fourth private copy locks in divergence at exactly the moment a shared primitive is cheapest to establish.
- **Architectural opportunity.** Establish one shared home for the structured-store helper and the causal-input/freshness primitive (dependency-neutral layer), preserving the superset semantics; new consumers (ledger, settings) build on it.
- **Potential benefits.** Removes a 3× diff-engine; gives ledger invalidation a governed, tested foundation; reduces future certification-pattern duplication.
- **Potential risks.** Complexity shift: over-generalizing the reason enums or lifecycle into a lowest-common-denominator abstraction would degrade the roadmap's richer semantics — the consolidation must be a superset extraction, not an intersection.
- **Likely timing.** **Pre-Implementation** (minimal scope: single home + ledger/settings as first new consumers). Full migration of existing consumers can trail as follow-up.
- **Confidence.** Medium-high.

#### OPP-5 — Dead-prompt retirement and stale-document quarantine

- **Observation.** Confirmed-dead prompts (`GetNextDecisions`, both `StartDecisionSession*`) remain in the catalog; roughly half the docs corpus describes the retired backend era; this repository's root carries the very autonomous-artifact proliferation the platform targets.
- **Validation.** Dead status re-confirmed at HEAD (with the `GenerateSystemPromptForFirstExecutionAgent` correction — contested, exclude from retirement until TD-9 is resolved). Staleness map re-confirmed; OSR-6 already records it.
- **Why it matters.** Guidance injection must not target dead prompts; the platform's governance artifacts must cite live architecture; and the root-level artifact sprawl is the platform's first natural test corpus.
- **Architectural opportunity.** Retire confirmed-dead prompts; annotate/quarantine retired-era docs; treat the root audit/plan sprawl as the seed case for preservation analysis rather than pre-cleaning it manually.
- **Potential benefits.** Shrinks the injection surface; prevents mis-grounded design; gives the platform an honest first workload.
- **Potential risks.** Minimal; only the contested prompt requires care.
- **Likely timing.** **Pre-Implementation** (deliberately small).
- **Confidence.** High.

### 2.2 Implementation Opportunities

#### OPP-6 — Repository-owned settings architecture as shared infrastructure

- **Observation.** No channel exists from configuration to planning or execution behavior: settings are binary-directory permissions-only; toggles are env vars; the one parsed flag never reaches the state machine; Roadmap CLI loads no policy at all (issue 003); `RoadmapExecutionBridge` is dead code.
- **Validation.** Confirmed at HEAD in full.
- **Why it matters.** Implementation-First Mode *is* a cross-CLI, cross-machine policy; it cannot exist without this channel, and the channel is generic infrastructure the moment it exists.
- **Architectural opportunity.** A repository-owned, schema-versioned settings document (via the consolidated store) read at composition time by all three CLIs — designed as the general policy-configuration surface, with the mode as its first tenant; issue 003's wiring gap closes opportunistically because the same loader/composition seam is touched.
- **Potential benefits.** Highest-leverage single piece: enables the mode, fixes 003, gives inline-default options records a binding home, replaces env-var sprawl with an auditable artifact, and travels with the repo via the submodule.
- **Potential risks.** Precedence ambiguity (committed artifact vs env override) must be explicit (U-8); threading a settings object through hand-wired composition roots touches many constructors — keep it one document object, not per-flag parameters.
- **Likely timing.** **Implementation** (it is part of the capability, built as shared infrastructure).
- **Confidence.** High.

#### OPP-7 — Classification as an explicit Decision at the change-detection boundary

- **Observation.** The change detector is the system's only observation of agent mutations (paths-only, `.agents/`-filtered, two consumers); classification has no existing home; the third audit establishes that all classification-like acts are Decisions with owner, validator, persistence, and authority boundary — and that display/report text is not decision authority.
- **Validation.** Detector behavior confirmed at HEAD; the promotion-unification claim was refuted (§1.1), so this is a **new Decision class sharing a routing pattern**, not an extension of the promotion classifier.
- **Why it matters.** Classification drives the mode's teeth, the ledger, and preservation triggers; if it is produced as a side-effect (log line, report field) rather than a validated, persisted decision, it acquires informal authority the constitution forbids.
- **Architectural opportunity.** Enrich the detector's output (change kinds) as the single git-observation authority; evaluate classification as a deterministic-first policy whose verdicts are recorded with rule/policy-version lineage; reuse the classify→route→preserve-evidence pattern from promotion without merging taxonomies.
- **Potential benefits.** Deterministic, auditable, replayable classification; single observation source (no second git shell-out); clean separation from promotion's structural-validity axis.
- **Potential risks.** Scope ambiguity (U-2: `.agents/` files are invisible to the detector); tempting drift toward agent-semantic classification in MVP (U-1); coupling classification synchronously into the commit gate without mode-gating could destabilize the stall gate (U-3).
- **Likely timing.** **Implementation.**
- **Confidence.** High on placement; medium on scope until U-1/U-2/U-3 resolve.

#### OPP-8 — Preservation ledger as machine-memory-shaped record with explicit evidence lifecycle

- **Observation.** The established persistence doctrine (schema-versioned JSON under `.agents/`, atomic writes, single-writer) and the causal-input freshness pattern fully cover the ledger's needs; the third audit's Machine Memory capability defines the ledger's semantic role — it *records* decisions/evidence made by other capabilities and owns none of them; its evidence-coupling smell warns that evidence silently becomes hidden control flow.
- **Validation.** Persistence claims confirmed at HEAD; `DecisionLedger` precedent confirmed with the correction that it writes no Markdown projection (a review-facing projection would be new, workflow-justified behavior).
- **Why it matters.** The ledger's core promise — avoiding duplicate analysis across executions — makes it a *lookup input* to future work, which is exactly the moment a record acquires control-flow authority. Whether that is governed or accidental is an architectural choice (U-4).
- **Architectural opportunity.** A ledger whose entries carry explicit lifecycle and causal inputs (file content hash, policy version, prompt SourceHash, guidance version) via the consolidated provenance primitive; its authority posture declared up front (record vs governed lookup) with an executable guard for whichever is chosen.
- **Potential benefits.** Deterministic dedup and invalidation for free; policy evolution becomes auditable re-analysis rather than migration; the review workflow gets a durable substrate.
- **Potential risks.** Evidence-as-hidden-control-flow (R-4); append-heavy growth in a whole-file-rewrite store (use the true-append pattern if volume demands); `.agents/` submodule ride means ledger writes are publish events — merge/conflict behavior under the single-writer assumption should be stated.
- **Likely timing.** **Implementation.**
- **Confidence.** High.

#### OPP-9 — Prompt guidance injection with guidance-version provenance

- **Observation.** The injection idiom is settled by precedent (optional placeholder, empty-string when off, single shared constant, catalog choke points; variants rejected by the WritePlan consolidation). Newly found: rendered placeholder content is provenance-invisible — nothing hashes what was injected.
- **Validation.** Generator mechanics and the provenance gap confirmed at HEAD (§1.1).
- **Why it matters.** An Implementation-First guidance change is a policy change; if no record captures which guidance version a turn ran under, violations and behavior shifts cannot be attributed — undermining the platform's own auditability standard.
- **Architectural opportunity.** Pair the injection seam with a recording obligation: the active guidance/policy version stamped into session telemetry and ledger records (the projection subsystem's input-hash practice generalized to injected policy content).
- **Potential benefits.** First general prompt-policy channel in the system, reusable for future policies; closes a latent provenance gap that predates the platform.
- **Potential risks.** Widening the prompt-name string-key graph (the recovery audits' flagged smell) — additions should ride existing catalog arms, not new string registries; guidance duplication across the Planning/Projection mirrors if the single-constant rule is not enforced.
- **Likely timing.** **Implementation.**
- **Confidence.** High.

#### OPP-10 — HITL preservation review as a durable, *supported* recovery flow

- **Observation.** The only working HITL shape is paused-state + evidence + operator `unblock`; the recovery audits confirm this as the Evidence Recovery capability pattern — and expose its sharpest gap: blocker producers can persist recovery intents that no handler supports, creating permanent report-only dead-ends; separately, some failure paths throw without leaving a durable recoverable state.
- **Validation.** Zero-interactive-HITL confirmed at HEAD; the unsupported-intent gap and ephemeral-failure-path smell confirmed in the recovery audits.
- **Why it matters.** A preservation review gate that blocks a destructive action *must* be resumable, or it converts the platform's safety mechanism into a workflow dead-end.
- **Architectural opportunity.** Model review-pending as a durable recoverable state whose recovery intent is registered with a supported handler from day one; the human resolution artifact doubles as the Implementation-First exception provenance ("explicitly requested by HITL" becomes a durable, scoped record — matching the constitution's human-exception authority requirement).
- **Potential benefits.** Reuses the one proven HITL pattern; gives the mode's exception clause a concrete authority artifact; avoids building interactive input that contradicts the autonomous-loop design.
- **Potential risks.** Inheriting the unsupported-intent gap if handler registration is deferred; over-generalizing into a universal review framework prematurely (R-2).
- **Likely timing.** **Implementation.**
- **Confidence.** High on the pattern; medium on the pause-placement question (U-6).

#### OPP-11 — Insight extraction as a Distillation Pattern instance

- **Observation.** `SynthesizeCompletedEpic` already performs free-form, disposition-annotated synthesis at archive time; the constitution's Distillation Pattern explicitly includes "preservation insight extraction"; the operational-context schema bounds what distilled knowledge may contain.
- **Validation.** Confirmed at HEAD, with two refinements: the sandbox workspace factory yields an *empty* workspace (seeding is the caller's contract), and insight outputs land in the ownership-ambiguous strategic-context zone the recovery audits flag (U-7).
- **Why it matters.** The MVP requirement is already architecturally satisfied by an existing pattern; the design work is boundary-keeping, not invention.
- **Architectural opportunity.** A sibling synthesis with envelope versioning and provenance, an explicit seeding contract, and a declared non-overlap with operational/strategic-context authority (the duplicate-authority scan `audit.md` required, now sharpened by the hot-zone finding).
- **Potential benefits.** Fast MVP on proven mechanics; future structured extraction supersedes bodies without authority migration.
- **Potential risks.** Drift into a shadow knowledge base (the authority doctrine's named failure mode); double-writing understanding that belongs to `roadmap-completion-context.md` or `operational_context.md`.
- **Likely timing.** **Implementation.**
- **Confidence.** High.

#### OPP-12 — Pruning-policy ownership consolidation

- **Observation.** Three mechanisms destroy or compress content with no shared policy: live-artifact rotation/retirement, transfer-time document optimization, and epic archival.
- **Validation.** Confirmed at HEAD.
- **Why it matters.** "Preservation before pruning" is incoherent if each pruning site keeps its own implicit policy; the platform's cut line is policy-vs-mechanics.
- **Architectural opportunity.** Pruning mechanics stay put; the question "may this be removed, and what must be preserved first" gets one owner that all three sites consult — Law 17 (deletion is a lifecycle event) operationalized.
- **Potential benefits.** Single audited policy; the three sites become uniform in preservation behavior without being merged.
- **Potential risks.** Over-coupling the fast rotation path to expensive analysis — the policy must be able to answer "trivially prunable" cheaply (rotation of already-rotated history should not invoke agent turns).
- **Likely timing.** **Implementation.**
- **Confidence.** Medium-high.

#### OPP-13 — Seam extraction at each touched insertion point

- **Observation.** The loop, plan pipeline, and state machine are fixed sequences with hand-wired composition and no step abstractions; prose XML-docs encode sequencing invariants.
- **Validation.** Confirmed at HEAD.
- **Why it matters.** Every platform insertion edits a fixed sequence; doing so without leaving a seam repeats the problem for the next capability.
- **Architectural opportunity.** At each point the platform touches (post-execution segment of the loop, archive boundary, plan verification gates), extract that segment behind an interface with characterization tests — the recovery audits' "small state → explicit transition" direction applied opportunistically, without a framework.
- **Potential benefits.** Regression protection where risk is highest; incremental progress toward the canonical decomposition at zero framework cost.
- **Potential risks.** Scope discipline — "strengthen the seam you touch" must not become a rolling refactor (R-1).
- **Likely timing.** **Implementation.**
- **Confidence.** High.

### 2.3 Immediate Follow-up Opportunities

#### OPP-14 — Prompt registration and catalog convergence

- **Observation.** Adding one runtime prompt with a projection touches ~5-7 files across two diverged near-duplicate catalogs plus path/name registries; prompt-name strings act as architectural keys.
- **Validation.** Confirmed at HEAD (validator enumerated the exact touch set; the catalogs have already diverged, not just duplicated).
- **Why it matters.** The platform will have just paid this registration tax for its own prompts; the pain and the correct shape will be concrete.
- **Architectural opportunity.** Converge the registration surface so a prompt declares its catalog/contract/projection membership once.
- **Potential benefits.** Removes a standing multi-file edit burden for every future prompt; shrinks the string-key graph.
- **Potential risks.** Premature if done before the platform's prompts exercise the current shape; a registry abstraction designed in the abstract tends to miss the divergence that already exists.
- **Likely timing.** **Immediate Follow-up.**
- **Confidence.** Medium-high.

#### OPP-15 — Permission-layer hardening (redirection, wrappers)

- **Observation.** `echo x > file.md` auto-approves; wrapper commands (`env`, `xargs`) bypass the allowlist; one-shots are ungated.
- **Validation.** Confirmed at HEAD (issues 007/008 unfixed).
- **Why it matters.** The platform's enforcement posture deliberately does not depend on the command gate — but defense-in-depth against trivial file-creation bypasses strengthens the mode's credibility, and the fix benefits the permission engine's existing mission.
- **Architectural opportunity.** Reject or classify redirection targets; recurse into wrapped commands.
- **Potential benefits.** Closes the loudest bypasses; reduces the gap between the permission engine's promise and behavior.
- **Potential risks.** Parser complexity creep; false-positive denials on legitimate safe commands.
- **Likely timing.** **Immediate Follow-up** (not a platform dependency by design).
- **Confidence.** Medium-high.

#### OPP-16 — Preservation observability stream and sink primitive extraction

- **Observation.** Observability is parallel append logs with no bus; the telemetry sink (true append, rotation, fail-open) is the right shape, currently single-consumer and living in the CLI project.
- **Validation.** Confirmed at HEAD (including the journal's whole-file-rewrite anti-pattern to avoid).
- **Why it matters.** Preservation actions need an audit stream from day one (Implementation timing, inside OPP-8/OPP-7 records); *extracting the sink as a shared primitive* is justified the moment it has two consumers.
- **Architectural opportunity.** Second consumer triggers extraction of the rotating-JSONL sink into shared infrastructure; preservation metrics later project over this stream.
- **Potential benefits.** One hardened append primitive; the future metrics/health projections get a source without new collection.
- **Potential risks.** Minimal; keep fail-open semantics.
- **Likely timing.** **Immediate Follow-up** (the extraction; the stream itself is Implementation).
- **Confidence.** High.

### 2.4 Deferred Opportunities

#### OPP-17 — Planning/Projection prompt mirror deduplication

- **Observation.** Twelve `ProjectionFor*` templates structurally mirror their Planning counterparts (corpus: 41 files, 13,404 lines).
- **Validation.** Confirmed at HEAD (line count corrected downward; duplication stands).
- **Why it matters / opportunity.** Real duplication, but the platform only requires *not worsening it* (single guidance constant); template unification is a large behavioral surface with no platform dependency.
- **Potential benefits.** Halves the planning-prompt maintenance surface — eventually.
- **Potential risks.** Prompt-behavior regressions are expensive to detect; no current forcing function.
- **Likely timing.** **Deferred.**
- **Confidence.** High (in deferral).

#### OPP-18 — Full canonical decomposition of the roadmap machine (9 capabilities, Run Control, transition contract)

- **Observation.** The recovery audits establish the target vocabulary; the mega-coordinator (1,984 lines, 27 collaborators, 24 persistence sites) is the known debt.
- **Validation.** Confirmed; the audits are explicitly observational and prescribe no framework.
- **Why it matters / opportunity.** The platform must *conform* to the capability vocabulary (own its decisions/evidence/recovery; add no branches inside the coordinator) without *undertaking* the decomposition. Only the C8 segment (OPP-2) is on the platform's path.
- **Potential benefits.** Eventually: authority defragmentation, testable capability boundaries.
- **Potential risks.** Undertaking it now would stall the capability behind the largest refactor in the backlog — the exact momentum loss this triage exists to prevent.
- **Likely timing.** **Deferred** (conformance now, decomposition later).
- **Confidence.** High.

#### OPP-19 — Structured knowledge, semantic dedup, knowledge promotion

- **Observation / opportunity.** Future evolution is fully mapped: envelope-versioned records upgrade in place; promotion runs through the materialization gate ("derived if reconstructable"); semantic machinery (embeddings, entailment) would be a new derived cache under that gate. The operational-context schema's deterministic-diff boundary (no NL entailment, no confidence scoring) is the line MVP must not cross.
- **Validation.** Doctrine confirmed; zero implementing code exists (by design).
- **Potential benefits.** Deferral keeps MVP free-form and honest; the gate prevents an ungoverned knowledge store.
- **Potential risks.** Only if the gate is not enforced (R-4).
- **Likely timing.** **Deferred**, gate-protected.
- **Confidence.** High.

#### OPP-20 — Preservation metrics, repository health, documentation-debt analytics

- **Observation / opportunity.** Projections over the ledger and preservation stream; no new collection needed once OPP-8/OPP-16 exist.
- **Validation.** Consistent with the projection subsystem's shape.
- **Potential benefits.** Cheap when the substrate exists; meaningless before it has data.
- **Likely timing.** **Deferred.**
- **Confidence.** High.

#### OPP-21 — Generalized mutation-acceptance governance

- **Observation / opportunity.** Constitution Law 5 (permission ≠ acceptance) implies an eventual universal acceptance protocol for repository mutations — the platform's post-mutation classification is its first concrete instance. Generalizing beyond that instance now would be speculative architecture.
- **Validation.** No current mechanism or consumer beyond the platform's own needs.
- **Potential risks of early build.** A governance framework with one client is a framework in search of users.
- **Likely timing.** **Deferred** (revisit when a second mutation-acceptance consumer appears).
- **Confidence.** Medium-high.

---

## 3. Coupling Analysis

**Healthy (keep):**
- Classification ← change-detector output: data flows at the natural observation boundary; one git observation source.
- Preservation analysis ← pruning sites: invocation-based, policy-consulting coupling — the sites keep mechanics, the platform keeps policy.
- Guidance constant → prompts via optional placeholder: one-way, versioned (once OPP-9's recording obligation exists), off-by-default.
- Ledger ← provenance primitive: reuse of a proven invalidation engine.

**Excessive (avoid by design):**
- Platform ↔ `RoadmapStateMachine` internals: any per-state branch or new `SaveStateAsync` site inside the coordinator repeats the layer-mixing smell; the context builder, output contracts, and (post-OPP-2) the completion capability are the sanctioned surfaces.
- Classification → commit gate, synchronous and unconditional: mode-ungated hard-blocking couples the stall gate's semantics to classifier correctness; the teeth decision (U-3) must be explicit and mode-scoped.
- Preservation persistence through the state-save path: preservation records belong in their own store, not appended to the roadmap state document's already-overloaded write.

**Accidental (eliminate at the source):**
- A fourth structured-store / provenance copy (prevented by OPP-4).
- Guidance text duplicated per template or per mirror (prevented by the single-constant rule).
- Preservation prompts widening the prompt-name string-key graph via new string registries (ride existing catalog arms; OPP-14 later shrinks the graph).
- A second insight-synthesis mechanism parallel to `SynthesizeCompletedEpic` rather than a sibling in the same runner pattern.

**Temporary (acceptable with a named retirement condition):**
- Triple completion orchestration until OPP-2 lands (retirement condition: single owner).
- Env-var override alongside the settings artifact (retirement condition: precedence rule U-8 settled and documented).
- Ledger without a Markdown projection until the review workflow demonstrates the need.

---

## 4. Responsibility Analysis

**Merge:**
- Pruning *policy* across the three pruning sites into one owner (OPP-12) — mechanics stay distributed.
- Completion certification *sequencing* into the Completion capability (OPP-2).
- Persistence/provenance helpers into one shared home (OPP-4).

**Split:**
- Certification evaluation vs archival mechanics vs preservation obligations: the overloaded close route currently fuses archive + synthesize + context-update + supersession + lifecycle + persistence; preservation stages should attach to a de-overloaded boundary, not be appended to the fusion.
- Classification *pattern* vs classification *taxonomies*: promotion disposition (structural validity of a candidate) and file-role (implementation vs non-implementation) are distinct Decision classes sharing a routing shape — the refuted unification must not be resurrected as a "universal classifier."

**Relocate:**
- Completion sequencing out of the state machine and loop into the owning subsystem (OPP-2).
- The rotating-JSONL sink out of the CLI project when it gains a second consumer (OPP-16).

**Do not create:**
- A universal classifier authority spanning promotion and file-role.
- A knowledge graph, event bus, or pipeline framework (no current second consumer; materialization gate governs).
- Interactive console HITL (contradicts the autonomous-loop design; the durable-state pattern covers the need).

---

## 5. Architectural Leverage

Ranked by breadth of simultaneous benefit:

1. **Settings channel (OPP-6).** One piece of infrastructure enables the mode, fixes issue 003, gives inline-default options a binding home, retires env-var sprawl, and serves every future policy — all three CLIs touched once.
2. **Completion convergence + archive safety (OPP-1/OPP-2).** Fixes three open data-loss issues, de-overloads the system's most overloaded transition, creates the platform's cleanest attach point, and removes one responsibility from the mega-coordinator.
3. **Provenance/store consolidation (OPP-4).** One primitive serves the ledger, settings, projections, roadmap provenance, and any future certification evidence.
4. **Scoped-operation certification (OPP-3).** Repairs the substrate under the platform *and* under two currently-flaky existing operations.
5. **Guidance injection + version recording (OPP-9).** The first general prompt-policy channel, plus closure of a provenance gap that predates the platform.

---

## 6. MVP vs Long-Term (intentional non-architecture)

Where MVP should deliberately avoid premature architecture, and why deferral is beneficial:

- **Free-form insights, envelope-versioned only.** Structure is a *promotion* through the materialization gate; versioning the envelope now means structure later costs no migration. Building schemas now would guess at knowledge shapes with zero corpus.
- **No semantic knowledge graph / no NL entailment / no confidence scoring.** The operational-context schema draws this boundary deliberately (deterministic diff, warnings over mutation); crossing it requires evidence the deterministic tier is insufficient.
- **Deterministic-first classification.** Path/location/kind rules with recorded lineage before any agent-judged semantics; agent classification is an escalation tier, not the foundation (U-1).
- **No policy DSL.** The permission engine's closed-world simple-rule shape is the house pattern; a rules language has no second consumer.
- **No generalized mutation governance (OPP-21), no universal review framework, no pipeline framework.** Each has exactly one prospective client today.
- **No preservation metrics until the ledger has data (OPP-20).**

The common rationale: every one of these acquires its real requirements only after the MVP produces a corpus (classifications, insights, reviews). Architecture invented before the corpus exists would encode guesses; the envelope/gate/provenance discipline ensures none of the deferrals become migrations.

---

## 7. Remaining Architectural Uncertainties

Questions that materially affect architecture and should be resolved before roadmap creation:

- **U-1 — Classification substrate for MVP.** Deterministic rules (pure function, no agent) vs agent-semantic judgment vs deterministic-with-agent-escalation. This decides whether classification is synchronous and cheap or asynchronous and session-bearing — fundamentally different integration shapes at the loop boundary.
- **U-2 — Policy scope.** Does the classification policy govern only the working tree (the detector's current view — `.agents/` is filtered out), or also `.agents/` and root-level planning artifacts (this repo's actual proliferation)? Determines whether the detector needs widening and whether the platform polices the system's own artifact families.
- **U-3 — Violation teeth.** Record-and-flag vs quarantine vs commit-gate block, per mode state. Interacts with the stall gate's no-change semantics (a blocked commit looks like a stall) and must be explicit before the gate integration is designed.
- **U-4 — Ledger authority posture.** Pure record (never consulted for control flow) vs governed lookup (skip re-analysis of unchanged files — which *is* control flow). The evidence-as-hidden-control-flow risk demands this be declared and guarded, not emergent.
- **U-5 — HITL request provenance semantics.** Which durable artifact constitutes "explicitly requested by HITL" — a decisions.md entry, epic/spec text, a review resolution — and what scope/expiry it carries (constitution: human exception authority requires explicit intent, scope, and lineage).
- **U-6 — Review pause placement.** Does a preservation review block the current iteration (destructive action deferred mid-loop) or accumulate to the completion boundary? Determines where the durable review state lives and which handler owns its supported recovery intent.
- **U-7 — Insight vs strategic/operational context ownership.** The duplicate-authority scan's hardest case, sitting in the recovery audits' named ownership hot zone: what may an insight artifact contain that `roadmap-completion-context.md` and `operational_context.md` may not absorb, and who decides promotion between them?
- **U-8 — Settings precedence.** Committed `.agents/` artifact vs machine-local env override — which wins, and is the override visible in provenance records?

---

## 8. Architectural Risks and Guiding Principles

- **R-1 — Rolling-refactor drift.** The recovery audits make wholesale decomposition tempting. *Principle: conformance over decomposition — the platform aligns to the capability vocabulary and strengthens only the seams it touches (OPP-13); OPP-18 stays deferred.*
- **R-2 — Over-generalization from refuted unification.** The promotion/file-role merger was refuted; a "universal classifier" or day-one universal review framework would resurrect it. *Principle: shared patterns, separate taxonomies and authorities; generalize only on the second concrete consumer.*
- **R-3 — Duplicated concepts at the persistence layer.** The fourth store copy, the fourth provenance copy, a second synthesis mechanism. *Principle: a new consumer may not create a new copy — it either reuses the shared primitive or funds its extraction (OPP-4).*
- **R-4 — Competing authorities / hidden control flow.** Insight artifacts vs operational/strategic context (U-7); ledger lookups steering execution (U-4); evidence acquiring authority without binding. *Principles: the duplicate-authority scan is part of the platform's definition of done; the ledger records or is governed — never silently steers; preservation proposes, owners act; every record carries its rule/policy/guidance version.*
- **R-5 — Provenance-invisible policy.** Injected guidance is unhashed today. *Principle: any content injected into a prompt under a policy carries a recorded version in the turn's durable records (OPP-9).*
- **R-6 — Non-durable safety states.** Throws and unsupported recovery intents create dead-ends exactly where preservation blocks destructive actions. *Principle: every preservation block is a durable, recoverable state with a registered supported handler before the blocking behavior ships (OPP-10).*
- **R-7 — Self-referential proliferation.** A preservation platform that emits unbounded syntheses, review records, and ledgers becomes the problem it polices. *Principle: the platform's own artifacts are subject to its own classification and the materialization gate; its evidence lives in bounded, indexed families, not free-growing root documents.*

---

## 9. Summary Triage Table

| # | Opportunity | Likely timing | Confidence |
|---|---|---|---|
| OPP-1 | Completion close/archive safety (004/005/006) | Pre-Implementation | High |
| OPP-2 | Completion certification orchestration convergence | Pre-Implementation | Medium-high |
| OPP-3 | Scoped-operation certification + path-extraction repair (009/010) | Pre-Implementation | High / Medium-high |
| OPP-4 | Structured store + provenance consolidation (minimal scope) | Pre-Implementation | Medium-high |
| OPP-5 | Dead-prompt retirement + stale-doc quarantine | Pre-Implementation | High |
| OPP-6 | Repository-owned settings architecture | Implementation | High |
| OPP-7 | Classification as explicit Decision at change boundary | Implementation | High / Medium (scope) |
| OPP-8 | Preservation ledger with explicit evidence lifecycle | Implementation | High |
| OPP-9 | Guidance injection + guidance-version provenance | Implementation | High |
| OPP-10 | HITL review as durable supported recovery flow | Implementation | High / Medium (placement) |
| OPP-11 | Insight extraction as Distillation instance | Implementation | High |
| OPP-12 | Pruning-policy ownership consolidation | Implementation | Medium-high |
| OPP-13 | Seam extraction at touched insertion points | Implementation | High |
| OPP-14 | Prompt registration/catalog convergence | Immediate Follow-up | Medium-high |
| OPP-15 | Permission-layer hardening (007/008) | Immediate Follow-up | Medium-high |
| OPP-16 | Observability sink extraction (on second consumer) | Immediate Follow-up | High |
| OPP-17 | Planning/Projection prompt mirror dedup | Deferred | High |
| OPP-18 | Full canonical machine decomposition | Deferred | High |
| OPP-19 | Structured knowledge / semantic dedup / promotion | Deferred | High |
| OPP-20 | Preservation metrics / health analytics | Deferred | High |
| OPP-21 | Generalized mutation-acceptance governance | Deferred | Medium-high |
