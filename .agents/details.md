# Implementation-First Review Details

## Source Basis

This file supplements `.agents/plan.md` using `.agents/specs/epic.md` and `.agents/specs/s0.md` through `.agents/specs/s7.md`.

Use `.agents/plan.md` as the current implementation target. The specs are the recovered intent source. Where the specs and plan differ, this file records the difference so implementers do not silently follow the older shape.

## Milestone Mapping

The specs use an older milestone order. The current plan intentionally refines that order:

| Spec file | Spec milestone | Current plan milestone |
| --- | --- | --- |
| `s0.md` | Architectural foundation | Milestone 0 |
| `s1.md` | Changed file classification | Milestone 2 |
| `s2.md` | Semantic candidate confirmation | Milestone 4 |
| `s3.md` | Non-implementation review ledger | Milestone 3 |
| `s4.md` | Free-form insight synthesis | Milestone 6 |
| `s5.md` | HITL epic completion review | Milestone 7 |
| `s6.md` | Planning integration and implementation-first guidance | Milestone 1 |
| `s7.md` | Architectural convergence | Milestone 8 |

The current order is useful because prompt policy and request capture need to exist before agents are asked to plan more work, ledger identity should exist before semantic confirmation persists dispositions, and post-execution integration must be proven before synthesis and completion review depend on the ledger.

## Policy Tension To Resolve

There is one meaningful tension between the specs and the current plan.

The specs say default implementation-first mode still allows non-implementation documentation when the HITL specifically requested that documentation or deliverable. The current plan says prompt-time non-implementation file generation requires both an enabled `artifactPolicy.allowHitlRequestedNonImplementationFiles` setting and an explicit HITL request.

Unless the plan is revised, implement the plan's stricter rule for prompt-time generation: enabled setting plus explicit captured HITL request. Still preserve the spec's HITL-authority intent in two ways:

- Always capture explicit HITL request evidence when it exists.
- Always allow completion review to record a human keep decision for a non-implementation file, regardless of whether the file was originally generated under the stricter prompt policy.

If product intent is to follow the original spec instead, update `.agents/plan.md` before Milestone 1 so the setting semantics are not ambiguous.

## Shared Vocabulary Contract

Use one vocabulary across model names, JSON fields, prompt text, tests, evidence files, and review output.

Deterministic classification routes:

- `ExcludedImplementationArtifact`
- `ExcludedMachineRequiredArtifact`
- `ExcludedSanctionedOperationalArtifact`
- `SemanticReviewCandidate`
- `AmbiguousForSemanticReview`

Semantic dispositions:

- `ConfirmedNonImplementation`
- `FalsePositive`
- `Uncertain`

Ledger resolution states:

- `Unresolved`
- `HitlKept`
- `HitlDeleted`
- `HitlFalsePositive`
- `HitlDeferred`

HITL provenance kinds:

- `None`
- `HitlRequested`
- `HitlKept`

Do not use `UncertainCandidate` for deterministic routing. `AmbiguousForSemanticReview` means "send this file to semantic confirmation." `Uncertain` is only a semantic confirmation outcome.

## Artifact Paths

Add canonical review paths to `OrchestrationArtifactPaths`:

- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

`CompletionArtifactPaths` should delegate to the orchestration constants for shared `.agents` paths instead of duplicating new literals. `.agents/review` and `.agents/evidence/non-implementation` are sanctioned operational artifacts and must be excluded from semantic review.

## Settings And Prompt Policy

Extend the current `CliSettingsLoadResult` rather than replacing it. The existing `LoadPermissionPolicy()` compatibility helper must keep returning only `PermissionPolicyOptions`.

Add:

```json
"artifactPolicy": {
  "allowHitlRequestedNonImplementationFiles": false
}
```

Settings behavior:

- Missing `artifactPolicy` defaults to implementation-first mode.
- Missing `allowHitlRequestedNonImplementationFiles` defaults to `false`.
- `permissions` validation must remain unchanged.
- `artifactPolicy` controls prompt guidance only. It never disables post-execution review.
- Enabled mode never authorizes autonomous documentation or theory-protection artifacts. It only permits explicitly captured HITL-requested non-implementation deliverables under the plan's stricter policy.

Prompt policy should come only from `ImplementationFirstPromptPolicyComposer`. Prompt templates may accept a `{promptPolicy}` parameter, or a small helper may append the composed text, but the policy body itself must not be copied into multiple templates.

Prompt targets include:

- `WritePlan.prompt`
- `RevisePlan.prompt`
- `StartExecution.prompt`
- `ContinueExecution.prompt`
- `GenerateSystemPromptForFirstExecutionAgent.prompt`
- `GenerateSystemPromptForNextExecutionAgent.prompt`
- decision prompts such as `GetNextDecisions.prompt`, `StartDecisionSession.prompt`, and `StartDecisionSessionFromTransfer.prompt` when they can prescribe deliverables
- roadmap planning prompts under `src/LoopRelay.Core/Prompts/Planning` through `RoadmapPromptCatalog.RenderRuntime` or an equivalent centralized context section
- completion evaluation context when review summaries exist

The policy text must discourage freeze, certification, governance, authority documentation milestones, unsupported Architecture Tests, unsupported Golden Tests, and other artifacts whose purpose is protecting theory rather than validating executable behavior.

## HITL Request Capture

`ExplicitHitlNonImplementationRequestCaptureService` should capture only explicit HITL request evidence. Do not infer request evidence from plan prose, agent-authored decisions, file names, or the mere existence of a documentation deliverable.

Generated plan and decision artifacts may carry a stable structured section such as:

```markdown
## HITL-Requested Non-Implementation Deliverables

| Path Or Pattern | Source | Source Hash | Rationale |
| --- | --- | --- | --- |
```

The exact section name can differ if the implementation already has a better convention, but it must be stable and parseable. Captured request entries need:

- deliverable path or path pattern
- source artifact path
- source content hash
- HITL provenance kind
- rationale or short excerpt
- first captured timestamp using the repository's existing timestamp convention, or UTC ISO-8601 if no convention exists

Request evidence explains legitimacy. It is not a keep/delete decision and must not bypass completion review.

## Slice Baseline And Delta Detection

The epic says `git diff -> changed file list`, but the current plan's baseline store is necessary to satisfy the spec intent without treating the whole dirty tree as current execution output.

Capture the pre-slice baseline after LoopRelay's pre-execution `.agents` context publish and immediately before `execution.RunAsync`. Capture the post-slice snapshot immediately after `execution.RunAsync` succeeds. Prefer capturing before `RetireLiveDecisionsAsync` and before post-execution `.agents` publication so the delta represents execution output, not later LoopRelay cleanup. If integration constraints force cleanup first, record those `.agents` changes as sanctioned operational evidence and never route them to semantic review.

The detector should use `IProcessRunner` and repository-relative paths. It needs both status and content facts:

- parse `git status --porcelain` for tracked and untracked changed paths
- record tracked `git diff --name-status` metadata when available
- include modified, added, deleted, renamed, staged, and untracked paths
- for renames, use the destination path as the candidate path and preserve source path in evidence
- compute SHA-256 over file bytes for existing files so untracked files have stable reviewed hashes
- for deleted files, preserve baseline hash when available and record reviewed status as deleted
- record existence, deletion flag, extension, size, baseline status, post status, baseline hash, post hash, and `PreExisted`

Delta rules:

- A clean file changed by execution is included.
- A new untracked file created by execution is included.
- A pre-existing dirty file unchanged by execution is excluded from the slice delta.
- A pre-existing dirty file further changed by execution is included with `PreExisted = true`.
- `.agents` files are recorded as sanctioned operational evidence, then excluded from semantic review.

## Deterministic Classification Details

The deterministic classifier is a router. It must not perform semantic confirmation and must not decide keep/delete behavior.

Exclude implementation artifacts using repository context, not extension alone. Examples:

- source files under `src`
- test files under `tests`
- UI assets required by product behavior
- migrations
- scripts and build scripts
- prompt resources compiled into or required by the product
- generated source conventions that are intentionally tracked

Exclude machine-required artifacts. Examples:

- `.slnx`, `.csproj`, `.props`, `.targets`
- lockfiles and package manifests
- CI files
- runtime or test configuration
- JSON schemas and config files used by runtime or tests
- source-generator inputs and generated manifests

Exclude sanctioned operational artifacts under `.agents`, including plans, milestones, handoffs, decisions, deltas, evidence, state, projections, archives, review artifacts, and ledgers.

Route likely prose, design, audit, roadmap, planning, issue, report, or root/docs Markdown files as `SemanticReviewCandidate` when they were changed by the execution slice. Route unknown ambiguous files as `AmbiguousForSemanticReview`.

Every classification result should include:

- route
- rule ID
- path facts used by the rule
- concise rationale
- classifier version

Run per-file classification with `Task.WhenAll`. Determinism means the same baseline, post-slice state, classifier version, and policy setting produce the same routes and evidence.

## Ledger Identity And Schema

The ledger is repository-local review state, not a knowledge database, commit gate, or repository acceptance record.

Minimum ledger entry fields:

- schema version
- entry ID
- execution slice ID or discovery context
- path
- previous path for renames when available
- baseline status
- post status
- reviewed content SHA-256, or deleted marker plus baseline hash for deleted files
- baseline content SHA-256 when available
- `PreExisted`
- deterministic classification route and evidence
- semantic disposition, nullable until confirmed
- semantic rationale and evidence
- classifier version
- confirmation prompt source hash
- first seen timestamp
- last seen timestamp
- HITL provenance kind
- HITL provenance evidence path or excerpt
- resolution state
- human decision metadata when present

Keep confirmed non-implementation entries, false positives, and semantic uncertainties distinguishable in the same JSON document. Expose query methods that return them separately.

Duplicate suppression must be exact:

- Skip semantic confirmation only when path, reviewed content hash or deleted-reviewed identity, classifier version, and confirmation prompt source hash match a valid existing semantic disposition.
- Re-confirm when content changes, path identity changes, classifier version changes, or prompt source hash changes.
- Never skip solely because a path appeared in the ledger before.

Invalid schema should fail with a clear error. Do not silently discard or rewrite unknown ledger state.

## Read-Only Review Runner

Semantic confirmation and synthesis must depend on `INonImplementationReviewRunner`, not on the normal mutation-capable execution path.

Runner constraints:

- accepts bounded prompt payloads and cancellation tokens
- returns structured text only
- has no workspace writes
- has no commits or pushes
- has no mutation-capable scoped artifact operation
- may read only the repository context needed for review

Shared primitives should not depend on CLI-specific agent specs. CLI, roadmap, and completion hosts can provide adapters, but tests must prove review services use the read-only runner and cannot be constructed with a mutation-capable runner adapter.

## Semantic Confirmation Contract

Add a confirmation prompt such as `ConfirmNonImplementationCandidate.prompt`.

Inputs should include:

- ledger entry ID
- candidate path
- route: `SemanticReviewCandidate` or `AmbiguousForSemanticReview`
- deterministic evidence
- execution slice ID or discovery context
- baseline status
- post status
- reviewed content hash or deleted-reviewed identity
- bounded content excerpt, or read-only inspection instructions

Output should be strict JSON or an exact parseable field table with:

- ledger entry ID
- candidate path
- reviewed content hash or deleted-reviewed identity
- disposition: `ConfirmedNonImplementation`, `FalsePositive`, or `Uncertain`
- concise rationale
- evidence excerpts or path facts
- uncertainty note when applicable

The parser must reject missing dispositions, unknown dispositions, mismatched entry ID, mismatched path, mismatched reviewed hash/status, and malformed output. Parser failure is review infrastructure failure, not semantic uncertainty.

The prompt must forbid keep/delete decisions. False positives are expected outcomes, not errors. Semantic uncertainty must remain durable instead of being forced into confirmed or false-positive categories.

## Post-Execution Integration

`NonImplementationPostExecutionReviewService` should run after every successful execution slice and before the post-execution `.agents` publish. It should:

- accept the pre-slice baseline and post-slice snapshot
- detect execution-produced changes
- classify changed files
- create or update ledger entries
- skip confirmation only for valid exact ledger identities
- run semantic confirmation for routed candidates
- render review evidence under `.agents/evidence/non-implementation/`
- return evidence paths and summary counts

If review infrastructure fails, the loop should return `LoopOutcome.Failed` with evidence rather than silently skipping the review. Component tests are insufficient; at least one CLI-level test must prove a root Markdown file created during an execution slice reaches the ledger.

The service should run before `CommitGate.CommitPushAndEvaluateAsync` so parent repository changes are reviewed before commit/push. The `.agents` publication after review should include ledger and evidence so review state is not stranded.

## Free-Form Synthesis

Synthesis is optional review support generated before HITL keep/delete decisions.

Inputs:

- unresolved confirmed non-implementation ledger entries
- semantic rationale
- bounded file content
- source paths
- entry IDs and reviewed hashes

Rules:

- Exclude false positives.
- Do not synthesize semantically uncertain entries as fact. If useful, include them in a separate "uncertain, not synthesized as fact" section.
- Keep output compact and free-form Markdown.
- Include source path references and ledger entry IDs.
- Write to `.agents/review/non-implementation-synthesis.md`.
- Record source entry IDs and reviewed hashes in the ledger or a sidecar section.
- Treat synthesis as stale unless its source entry IDs, reviewed hashes, and synthesis prompt source hash still match.

Synthesis must not authorize keeping, deleting, promoting, or retaining any source file. It is not a structured knowledge system.

## HITL Completion Review

Completion review must begin with a fresh repository review refresh, not ledger state alone. This prevents false readiness when the ledger has no unresolved entries but current changed prose/report files exist.

`NonImplementationCompletionReviewService` should return `Ready` only when the fresh refresh plus ledger state has no unresolved confirmed non-implementation entries and no unresolved semantic uncertainties. False positives should remain auditable but should not block by themselves.

If decisions are missing, write:

- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`

and return `Blocked`.

Decision template requirements:

```markdown
| Entry ID | Path | Reviewed SHA-256 | Reviewed Status | Decision | HITL Reason |
| --- | --- | --- | --- | --- | --- |
```

Allowed file decisions:

- `Keep`
- `Delete`
- `ResolveFalsePositive`
- `Defer`

Allowed synthesis decisions in a separate single-row table:

- `KeepSynthesis`
- `DiscardSynthesis`
- `DeferSynthesis`

The parser must reject duplicate entry IDs, unknown decisions, missing required rows, path mismatch, hash mismatch, status mismatch, and non-empty decisions for entries that are no longer unresolved.

Delete decisions require stale-decision validation:

- ledger entry ID matches
- path matches
- reviewed status matches
- current content hash matches the reviewed hash
- current file is still at the reviewed path
- delete path resolves inside the repository
- delete path is not under `.agents`

If a delete target is stale, replaced, moved, missing unexpectedly, hash-mismatched, outside the repository, or under `.agents`, block, rescan, and require a fresh decision.

`Defer` is a valid explicit human decision. Once recorded as `HitlDeferred`, it should not be treated as a missing decision for that review cycle, but it must remain visible in completion context and audit output.

Keep decisions should record `HitlKept`. If the human states the file was originally requested, preserve or attach `HitlRequested` evidence where possible.

## Completion And Roadmap Integration

In the main CLI, run completion review after `gate.IsEpicCompleteAsync()` returns true and before `completionCertification.CertifyPlanCompletionAsync`.

If completion review is blocked:

- publish `.agents` state
- keep decision-session resume state intact
- return `LoopOutcome.CompletionBlocked`

If completion review applies parent-repository deletions, commit and push those deletions before certification without incrementing the stall counter. A narrow `CommitGate.CommitPushIfChangedAsync` helper is acceptable if needed.

Pass review evidence paths or a summary path into `CompletionCertificationRequest` and include the review summary in `CompletionPromptContextBuilder.BuildEvaluationContextAsync`. Completed epic archiving should include `.agents/review` contents.

For roadmap completion, run the same review service before completion evaluation. If blocked, persist blocked evidence with the review request path and a next step to fill the decisions template and rerun.

## Test Coverage That Must Not Be Lost

The plan already lists validation commands. The important cross-cutting test intent from the specs is:

- default settings produce implementation-first prompt guidance
- enabled setting does not permit non-implementation deliverables without explicit HITL request evidence under the current plan's stricter policy
- prompt policy body is centralized and not duplicated
- explicit HITL request evidence is captured only from structured, grounded markers
- code, tests, project files, package/config files, prompt resources, lockfiles, and `.agents` artifacts are deterministically excluded
- root/docs/issues prose files changed by the current slice route to semantic review
- ambiguous deterministic routes are semantically confirmed instead of treated as final uncertainty
- pre-existing dirty files unchanged by execution are excluded from the slice delta
- pre-existing dirty files modified by execution are included with baseline facts
- untracked files created during execution are discovered
- renamed files use destination path for review identity and preserve source path evidence
- ledger duplicate suppression requires exact path/hash/classifier/prompt identity
- false positives are separate from confirmed non-implementation entries
- semantic confirmation and synthesis use only the read-only review runner
- post-execution review is invoked by the CLI, not just component services
- review failure fails the loop rather than silently skipping review
- completion review performs a fresh refresh before readiness
- stale delete decisions block when content changed after review
- delete decisions reject path traversal and `.agents` paths
- synthesis keep/discard is recorded separately from file keep/delete
- completion review blocked state does not clear decision-session resume state

## Non-Goals To Preserve

Do not broaden the implementation into:

- structured insight synthesis
- semantic deduplication
- repository knowledge projection
- preservation metrics
- repository health analysis
- documentation debt analysis
- semantic garbage collection
- repository mutation acceptance
- commit gating
- publication gating
- repository certification
- broad relay/runtime redesign

