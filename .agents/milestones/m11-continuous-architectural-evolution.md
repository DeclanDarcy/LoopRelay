# Phase 11 - Governance and Documentation

Goal: align architecture documentation, governance evidence, and rollback paths with the implemented design.

## Design note (m11)

m11 is a documentation + governance milestone (the file name "continuous-architectural-evolution" is stale; the
title and body are authoritative, as with m8). Its certification bar — "Documentation matches implemented
behavior" — was the whole task, so the work was driven by ground-truth discovery against source rather than the
plan's original aspiration. A seven-lens read-only discovery pass established the verified implementation facts
and audited the existing `docs/` corpus for staleness; the central `docs/architecture.md` predated the entire
m2–m10 loop and described only the legacy execution-session model. No prompt, Rust, UI, or wire change; the
only code added is one additive test (`OrchestrationGovernanceTests`).

Key divergences the docs now record faithfully (verified in source, not assumed): held-open transport is
`codex app-server` JSON-RPC, not the removed `codex proto`; the m7 Decision router is registry-free (a pure
synchronous threshold over `RouterInputs`), not the DecisionSessions registry policy/eligibility services;
milestone pinning was removed (agents move between milestones dynamically) and the `execution/context` preview
endpoint was genericized to drop `?milestonePath=`; the m10 `OrchestrationFeatureFlags` are the rollback
off-switches; and the orchestration loop bypasses `AwaitingAcceptance` entirely — handoffs rotate synchronously
in the orchestrator and the human gate moved downstream to the Decision Submit gate (`BeginSubmitDecisionsAsync`),
while the legacy `CommandCenter.Execution` subsystem (`HandoffService` → `AwaitingAcceptance`,
`ExecutionSessionService.Accept/RejectAsync`) remains unmodified as rollback path 5.

Deliverables:
- `docs/architecture.md` — added the "Orchestration Loop Architecture" section (the seven mechanisms: shared
  role-agnostic runtime, Operational vs Decision roles, generated prompt authority, repository-scoped
  orchestrator ownership, plan-authoring lifecycle, handoff/decision rotation, router reuse/transfer); relabeled
  the legacy execution-session subsystem and corrected the genericized (now milestone-free) context-preview
  section.
- `docs/orchestration-loop-governance.md` (new) — the durable evidence register: eight per-change evidence
  packages (LOOP-1..LOOP-8) following the `docs/architectural-evidence.md` schema, the
  `DIVERGENCE-AWAITING-ACCEPTANCE` governance record, the five rollback paths, an explicit Known Fallback
  Behavior section, and a governance-test coverage map.
- `docs/prompt-architecture.md` (new) — the 11 canonical prompts with generated signatures (`.Text` vs
  `.Render(args)` derived from each `.prompt`'s placeholders), session roles, loop usage, the Lib.Prompts
  source-generator + `SourceHash`, `PromptProvenance` (7 fields), and the no-literal-prompt enforcement.
- `docs/architectural-mechanisms.md` — appended "Orchestration Loop Mechanisms": No-Literal-Prompt Enforcement,
  Prompt Provenance Capture, Handoff and Decision Rotation Sequencing, and Layering Isolation for the Loop.
- `docs/contracts.md` + `docs/contract-endpoint-catalog.md` — the ten loop endpoints + DELETE teardown
  (method/route/family/error codes/consumers), the three SSE stream event vocabularies (4 + 9 + 9), the
  `Last-Event-ID` replay + payload-vs-lifecycle contract, and the structured-error envelope mapping
  (KeyNotFound→404, Argument→400, InvalidOperation/ObjectDisposed→409).
- `docs/compatibility-structure-governance.md` — the loop's compatibility impact (legacy execution sessions
  retained unmodified; the m8-frozen TS run-event types + `repository-dashboard.generated.ts` byte-identical;
  additive hooks/routes/tests) plus inventory rows for the legacy rollback routes and the frozen TS mirror.

Process: the docs were authored against the verified facts (the keystone `architecture.md` rewrite and the
governance register by hand for consistency with the established register; the four inventory/mechanism docs by
a disjoint-file authoring workflow). A five-doc adversarial faithfulness review (find → default-refute verify;
11 agents, ~580K tokens) extracted every concrete assertion and checked it against source — **4 confirmed
mismatches / 1 refuted**, with the governance register, prompt-architecture, mechanisms, and contract docs
clean on the first pass. All four confirmed mismatches were corrected (three were stale milestone-pinned prose
in the genericized context-preview section; one was a compatibility-inventory overclaim). The new
`OrchestrationGovernanceTests` pins the rollback surface (the four flag defaults, the router threshold default,
and the continued existence of the legacy execution-session subsystem) — the one boundary no earlier milestone
guarded — making the governance coverage map truthful rather than aspirational.

Verification: full backend suite **1129 passed / 1 skipped / 0 failed** (m10 was 1126 → +3 from
`OrchestrationGovernanceTests`); the m10 suite reproducibility (the `ProcessEnvironment` serialization) holds.
The single skip remains the live-only `[Fact(Skip)]` app-server check. Backend-only and additive: the three
m8-frozen UI type files, the m8 contract goldens, the prompts, the Rust shell, and the UI are byte-untouched →
UI stays m9's 420/420 by construction; cargo not exercised.

## Implementation

- [x] Update architecture documentation for:
  - [x] `CommandCenter.Agents` as shared role-agnostic process runtime;
  - [x] Operational vs Decision session roles;
  - [x] generated prompt authority;
  - [x] repository-scoped orchestrator ownership;
  - [x] plan authoring lifecycle;
  - [x] handoff/decision artifact rotation;
  - [x] router reuse/transfer behavior.
- [x] Update contract documentation for all new endpoints, stream events, and structured errors.
- [x] Update prompt architecture documentation for the 11 canonical prompts and generated signatures.
- [x] Record governance evidence for the intentional divergence from current `HandoffService` behavior and `AwaitingAcceptance`.
- [x] Record rollback paths:
  - [x] disable Plan Authoring screen (UI mount gate `isAuthoringSessionActive`, not a backend flag);
  - [x] disable persistent planning (`PersistentPlanningProcessEnabled=false`);
  - [x] disable Decision reuse and force transfer-only (`PersistentDecisionProcessReuseEnabled=false` / `TransferOnlyDecisionFallbackEnabled=true`);
  - [x] disable automatic commit/push (`AutomaticCommitPushAfterExecuteEnabled=false`);
  - [x] return to existing execution/session endpoints (legacy subsystem retained unmodified).
- [x] Document compatibility impact for existing execution sessions, generated TypeScript artifacts, UI hooks, and tests.
- [x] Add or update architectural mechanism docs for prompt provenance and no-literal-prompt enforcement.

## Certification

- [x] Documentation matches implemented behavior — a five-doc adversarial faithfulness review (find →
  default-refute verify) checked every concrete assertion against source; 4 confirmed mismatches were corrected,
  1 was refuted, and the register/prompt/mechanism/contract docs were clean on the first pass.
- [x] Every architecture-affecting change has invariant, owner, evidence, compatibility impact, and rollback
  path — the `docs/orchestration-loop-governance.md` register records eight evidence packages plus the
  AwaitingAcceptance divergence, each following the `docs/architectural-evidence.md` schema.
- [x] Governance tests protect the new boundaries — layering, prompt-authority, endpoint-disposition,
  decision-isolation, process-leak, recovery, per-flag, and contract Oracle guards already exist (coverage map);
  the new `OrchestrationGovernanceTests` adds the previously-unguarded rollback surface. Full suite green
  (1129/1-skip/0-fail).
- [x] Known fallback behavior is explicit and does not masquerade as the full design — the register's Known
  Fallback Behavior section labels the deterministic `CommandCenter.Decisions` services, the `(len+3)/4` token
  estimator, the registry-free router degradation, and the one-shot fallback as fallbacks, not the live design.
