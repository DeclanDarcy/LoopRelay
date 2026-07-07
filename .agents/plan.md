# Implementation-First Non-Implementation File Review Plan

## Purpose

Implement a post-execution review loop that detects repository files created or changed outside implementation work, confirms likely non-implementation artifacts with read-only semantic review, records durable review state, optionally synthesizes useful information, and presents the result for explicit human review at epic completion.

The default operating mode is implementation-first: autonomous repository growth should normally be code, tests, project files, runtime configuration, generated build artifacts that are intentionally tracked, or LoopRelay operational artifacts under `.agents`. Non-implementation files remain allowed when the human explicitly requested that deliverable or explicitly chooses to keep it during review.

This is not a commit gate, publication gate, repository acceptance system, repository knowledge platform, or documentation debt analyzer. It is a post-execution identification and review loop plus centralized prompt guidance.

## Projection Certification Gate

This plan is not eligible to drive autonomous execution until it has been reviewed against a concrete project projection version or hash.

Implementation work must add a durable gate that records:

- projection identity or source hash
- plan content hash
- reviewer or process that performed the projection review
- review verdict
- timestamp
- any projection-specific constraints added during review

Recommended artifact path: `.agents/review/plan-projection-certification.json`.

If this certification artifact is absent, stale for the current plan hash, or tied to a missing projection identity, autonomous execution must stop with a clear blocker. The plan may still be edited, reviewed, or manually discussed, but it must not be treated as self-authorizing executable authority.

## Target Capability

For every execution slice:

```text
pre-slice repository baseline
-> execution
-> post-slice repository snapshot
-> execution-produced changed-file delta
-> deterministic parallel classification
-> read-only semantic confirmation for routed candidates
-> durable non-implementation review ledger
-> review evidence published with .agents state
```

At epic completion:

```text
fresh repository review refresh
+ unresolved confirmed non-implementation files
+ false positives
+ uncertain semantic dispositions
+ optional compact synthesis
-> human keep/delete/synthesis/uncertain decisions
-> stale-decision validation
-> durable decision record
-> completion certification proceeds only after required review decisions exist
```

Planning, decision, roadmap, and execution prompts should receive centralized implementation-first guidance unless the user has explicitly opted in to autonomous non-implementation file generation.

## Vocabulary

Use these terms consistently in code, tests, persisted JSON, prompt text, and review artifacts:

- `Implementation artifact`: source code, tests, UI assets, migrations, project files, build scripts, package manifests, generated source that is intentionally tracked, and other files required to implement, build, test, or run the product.
- `Machine-required artifact`: lockfiles, solution/project files, configuration files, generated manifests, prompt resources compiled into the product, schema files, CI files, and other non-code files required by tools or runtime behavior.
- `Sanctioned operational artifact`: LoopRelay runtime files under `.agents`, including plans, milestones, handoffs, decisions, deltas, evidence, state, projections, archives, ledgers, certification artifacts, and review artifacts.
- `Semantic review candidate`: a changed file that deterministic classification cannot safely exclude and that must be semantically reviewed.
- `Ambiguous for semantic review`: deterministic routing for an unknown or ambiguous file that still requires semantic review. This is not a final uncertainty disposition.
- `Confirmed non-implementation file`: a candidate semantically confirmed as a non-implementation artifact.
- `False positive`: a routed candidate semantically determined to be implementation, machine-required, sanctioned operational, or otherwise not a non-implementation artifact.
- `Semantic uncertainty`: a semantic confirmation result where the review agent cannot responsibly decide whether the candidate is non-implementation.
- `HITL-requested non-implementation file`: a non-implementation file tied to explicit human authority, either from a captured request/deliverable marker or a later human keep decision.
- `Free-form insight synthesis`: compact review support extracted from confirmed non-implementation files before keep/delete decisions; it is not a structured knowledge store.

Do not use `UncertainCandidate` for deterministic routing. Reserve `Uncertain` only for semantic confirmation output.

## Architecture

Add shared review primitives under `src/LoopRelay.Orchestration.Primitives` because the capability is used by the main CLI loop, completion certification, and roadmap/planning prompt flow. Avoid a new project unless implementation proves the primitives cannot stay cohesive there.

Recommended folders:

- `Models/NonImplementationReview/`
- `Services/NonImplementationReview/`
- `Abstractions/NonImplementationReview/`

Primary shared components:

- `RepositorySliceBaselineStore`: captures pre-slice and post-slice git snapshots and computes execution-produced deltas.
- `RepositoryChangeSetDetector`: obtains git status/diff data and file facts. It must support untracked files without treating the whole dirty tree as current execution output.
- `NonImplementationArtifactClassifier`: deterministic classifier that runs per changed file and produces route, evidence, and exclusion reason.
- `NonImplementationReviewLedgerStore`: JSON ledger under `.agents/review/non-implementation-ledger.json` with schema versioning. A minimal schema/store exists before semantic confirmation; later milestones harden query/rendering behavior.
- `INonImplementationReviewRunner`: dedicated read-only runner contract for semantic confirmation and synthesis.
- `NonImplementationSemanticConfirmer`: confirms candidates through `INonImplementationReviewRunner` with strict structured output.
- `NonImplementationPostExecutionReviewService`: wires detector, classifier, semantic confirmer, authority capture, ledger write, and evidence rendering for each execution slice.
- `NonImplementationInsightSynthesizer`: optional read-only/free-form synthesis runner for confirmed non-implementation files.
- `NonImplementationCompletionReviewService`: performs a fresh review refresh, assembles the completion review set, writes the pending review request, parses human decisions, validates stale-delete safety, applies approved deletes, and records durable outcomes.
- `ExplicitNonImplementationAuthorityCaptureService`: records explicit authority when a non-implementation deliverable is requested or authorized before completion.
- `ImplementationFirstPromptPolicyComposer`: central source of prompt guidance derived from CLI settings.

Canonical artifact paths should live in `OrchestrationArtifactPaths` where possible:

- `.agents/review/plan-projection-certification.json`
- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

Completion-specific code may keep compatibility constants in `CompletionArtifactPaths`, but those constants must delegate to the orchestration paths instead of inventing duplicate literals.

## Settings

Extend the existing settings document loaded by `CliSettingsLoader` without breaking `LoadPermissionPolicy()`.

Add:

```json
"artifactPolicy": {
  "allowAutonomousNonImplementationFiles": false
}
```

Implementation requirements:

- Default to `false` when loading the default template.
- Keep `permissions` validation unchanged.
- Return a broader settings result, for example `CliSettingsLoadResult(Permissions, ArtifactPolicy, Path, IsDefaultTemplate)`.
- Preserve `LoadPermissionPolicy()` as a compatibility helper.
- Add `NonImplementationArtifactPolicyOptions` with a default implementation-first mode.
- The setting controls prompt guidance only. It does not disable the post-execution review loop.

## Milestone 0 - Architectural Foundation

Objective: establish terminology, settings, prompt-policy composition, projection certification, minimal ledger ownership, and review runner boundaries before implementing the full loop.

Work:

- Add shared vocabulary types and documentation. Prefer concise code-level records/enums plus a short `docs/non-implementation-review.md` because this capability requires terminology and boundaries to be durable.
- Add the projection certification model and stale-check logic keyed by plan hash and projection identity/hash.
- Add `NonImplementationArtifactPolicyOptions` and settings loader support.
- Add `ImplementationFirstPromptPolicyComposer` that returns stable guidance text for implementation-first and opt-in modes.
- Add a minimal `NonImplementationReviewLedgerStore` skeleton with schema version, stable path, load/save validation, and empty document support. This exists early so semantic confirmation can safely depend on it.
- Define `INonImplementationReviewRunner`.
  - Inputs are bounded prompt payloads and cancellation tokens.
  - Output is returned as structured text only.
  - The runner must use a read-only sandbox/profile with no workspace writes, no commits, no pushes, and no mutation-capable scoped artifact operation.
  - Confirmation and synthesis services must depend on this interface, not on the normal execution agent path.
- Define review ownership:
  - slice baseline and changed-file detection live in orchestration primitives
  - semantic confirmation and synthesis run only through the read-only review runner
  - main CLI invokes the post-execution identification loop after execution writes and before the `.agents` post-execution publish
  - epic-completion review runs before completion certification closes the epic
  - roadmap/planning prompts consume the centralized prompt policy text
- Add focused tests in `LoopRelay.Orchestration.Primitives.Tests` and `LoopRelay.Permissions.Tests` for settings defaults, projection certification staleness, ledger skeleton validation, read-only runner contract checks, and policy text selection.

Acceptance:

- Terms are represented by explicit types or documented constants.
- Plan execution is gated on projection certification.
- `allowAutonomousNonImplementationFiles` defaults to implementation-first mode.
- One prompt-policy composer produces all implementation-first guidance.
- A minimal ledger store exists before semantic confirmation.
- Semantic confirmation and synthesis cannot use a mutation-capable runner.

## Milestone 1 - Slice Baseline And Changed File Classification

Objective: deterministically route only files changed by the current execution slice.

Work:

- Implement `RepositorySliceBaselineStore`.
  - Capture a pre-slice snapshot immediately before the execution agent runs.
  - Capture a post-slice snapshot immediately after the execution agent completes.
  - Compare path status, existence, and content hash to compute execution-produced changes.
  - Exclude pre-existing dirty files that did not change during the slice.
  - Include pre-existing dirty files that the execution slice further changed, with `PreExisted = true`.
  - Include new untracked files created during the slice.
  - Assign an `ExecutionSliceId` and persist baseline metadata where necessary for crash-safe review.
- Implement `RepositoryChangeSetDetector` using `IProcessRunner` and `Repository`.
  - Parse `git status --porcelain` for changed path discovery.
  - Record `git diff --name-status` metadata where available for tracked files.
  - Include untracked, modified, added, deleted, renamed, and staged paths.
  - Capture path, status, baseline status, post status, deletion flag, existence, extension, size if available, baseline hash if readable, post hash if readable, and tracked diff metadata when available.
  - Filter out `.agents` only after recording sanctioned operational evidence.
- Implement `NonImplementationArtifactClassifier`.
  - Exclude implementation artifacts by source/test directory, recognized code extensions, UI asset paths, migrations, scripts, prompt resources, and generated source conventions.
  - Exclude machine-required artifacts such as `.slnx`, `.csproj`, `.props`, `.targets`, lockfiles, package manifests, CI files, settings templates, JSON schema/config files used by runtime or tests, and source-generator inputs.
  - Exclude sanctioned operational artifacts under `.agents`, including the review ledger and evidence created by this capability.
  - Route likely prose/design/audit/roadmap/report files as `SemanticReviewCandidate`.
  - Route unknown ambiguous files as `AmbiguousForSemanticReview`.
  - Emit classification evidence with rule ID, path facts, and rationale.
- Run classification with `Task.WhenAll` over the slice delta records.
- Add tests:
  - code files under `src` and `tests` are excluded
  - `.csproj`, `.slnx`, package/config files, prompt resources, and lockfiles are excluded
  - `.agents` operational files are excluded as sanctioned
  - root/docs/issues Markdown files route as candidates when changed by the slice
  - unknown ambiguous files route as ambiguous for semantic review
  - pre-existing dirty files unchanged by execution are excluded from the slice delta
  - pre-existing dirty files modified by execution are included with baseline facts
  - untracked files created during execution are discovered
  - renames use the destination path

Acceptance:

- Changed-file discovery includes untracked files without treating the whole dirty tree as current execution output.
- Classification is deterministic for the same baseline and post-slice state.
- Code and machine-required files reliably avoid semantic review.
- Candidate output includes enough evidence for semantic review, ledger identity, and debugging.

## Milestone 2 - Ledger Identity And Persistence

Objective: make semantic disposition identity durable before semantic confirmation depends on it.

Work:

- Expand ledger records with:
  - schema version
  - entry ID
  - execution slice ID or discovery context
  - path
  - baseline status
  - post status
  - content hash reviewed
  - baseline content hash when available
  - `PreExisted` flag
  - deterministic classification route and evidence
  - semantic disposition, nullable until confirmation
  - semantic rationale and evidence
  - classifier version
  - confirmation prompt source hash
  - first seen and last seen timestamps
  - authority kind: `None`, `HitlRequested`, `PlanDeclaredDeliverable`, `DecisionAuthorized`, `HitlKept`
  - authority evidence path or excerpt when available
  - resolution state: `Unresolved`, `HitlKept`, `HitlDeleted`, `HitlFalsePositive`, `HitlDeferred`
  - human decision metadata when present
- Keep confirmed, false-positive, and semantically uncertain entries distinguishable in the same JSON document. Expose query methods that return them separately.
- Add duplicate suppression rules:
  - skip semantic confirmation only when path, reviewed content hash, classifier version, and confirmation prompt source hash match a valid existing semantic disposition
  - re-confirm when content changes, path identity changes, classifier version changes, or prompt source hash changes
  - never skip solely because a path appeared in the ledger before
- Add authority-capture hooks that can attach explicit human authority from a structured plan/decision marker or later completion decision. Do not infer human authority from prose unless the marker is explicit.
- Add tests:
  - writes schema version
  - records pending, confirmed, false-positive, and semantically uncertain entries separately
  - same path/hash/version suppresses duplicate confirmation
  - changed hash requires confirmation
  - path-only match does not suppress confirmation
  - authority kind and evidence are durable
  - invalid schema blocks with a clear error

Acceptance:

- Ledger state is repository-local and durable before semantic confirmation integration.
- False positives are never merged into confirmed review state.
- Known unchanged files avoid repeated semantic confirmation only through hash/version identity.
- Later synthesis and HITL review can consume ledger state directly.

## Milestone 3 - Semantic Candidate Confirmation

Objective: confirm routed candidates with a mutation-impossible read-only agent workflow.

Work:

- Add `ConfirmNonImplementationCandidate.prompt` under `src/LoopRelay.Core/Prompts`.
- Prompt requirements:
  - input includes candidate path, deterministic evidence, slice ID, baseline status, post status, reviewed content hash, and bounded content excerpt or instructions to inspect the file read-only
  - output is strict JSON or an exact Markdown field table parsed into:
    - ledger entry ID
    - candidate path
    - reviewed content hash
    - disposition: `ConfirmedNonImplementation`, `FalsePositive`, or `Uncertain`
    - concise rationale
    - evidence excerpts or path facts
    - uncertainty note when applicable
  - prompt explicitly forbids keep/delete decisions
- Implement parser and validation for the structured output.
- Implement `NonImplementationSemanticConfirmer`.
  - Consume only `SemanticReviewCandidate` and `AmbiguousForSemanticReview` routes from deterministic classification.
  - Ask `NonImplementationReviewLedgerStore` whether a valid semantic disposition already exists for the exact path/hash/classifier/prompt identity.
  - Treat false positives as normal outcomes.
  - Preserve semantic uncertainty instead of forcing a binary answer.
  - Update ledger entries with semantic disposition and rationale.
- Host composition:
  - main CLI and roadmap/completion tests pass an `INonImplementationReviewRunner`
  - shared primitives do not depend on `LoopRelay.Cli.AgentSpecs`
  - tests assert review services receive read-only runner calls and never open operational or scoped mutation specs
- Add tests:
  - parser accepts each valid disposition
  - parser rejects missing/unknown disposition
  - parser rejects mismatched entry ID, path, or content hash
  - service skips only valid exact ledger identities
  - service confirms candidates and records rationale
  - service does not process deterministic exclusions
  - service cannot be constructed with a mutation-capable runner adapter

Acceptance:

- Every routed candidate receives a durable semantic disposition or is skipped by a valid exact ledger identity.
- False positives and semantic uncertainty are first-class outcomes.
- Semantic confirmation does not decide retention or deletion.
- Semantic confirmation cannot mutate repository files.

## Milestone 4 - Post-Execution CLI Integration

Objective: make the identification loop actually run after every execution slice.

Work:

- Implement `NonImplementationPostExecutionReviewService`.
  - Accept the pre-slice baseline and post-slice snapshot.
  - Detect execution-produced changes.
  - Classify changed files.
  - Create or update ledger entries.
  - Run semantic confirmation for candidates not covered by valid ledger identity.
  - Render review evidence under `.agents/evidence/non-implementation/`.
  - Return evidence paths and summary counts.
- Main CLI integration:
  - Capture the pre-slice baseline immediately before `execution.RunAsync`.
  - Run the post-execution review service immediately after `execution.RunAsync` succeeds and before the `.agents` post-execution publish.
  - Keep the service before `CommitGate.CommitPushAndEvaluateAsync` so parent repository changes are reviewed before commit/push.
  - Publish `.agents` after the review service so ledger and evidence are not stranded.
  - If review infrastructure fails, return `LoopOutcome.Failed` with evidence rather than silently skipping the loop.
- Roadmap execution integration:
  - If legacy roadmap execution is re-enabled, apply the same pre/post baseline and post-execution review service around `RoadmapExecutionBridge`.
  - If roadmap execution remains paused, document that the main CLI is the active execution integration and keep roadmap completion review refresh as a backstop.
- Add tests:
  - main CLI captures pre-slice baseline before execution
  - main CLI runs detector/classifier/confirmer/ledger after execution and before `.agents` post-execution publish
  - component tests alone are insufficient: add an end-to-end loop test that a generated root Markdown file reaches the ledger after one execution slice
  - pre-existing dirty Markdown that execution does not touch is not ledgered as current slice output
  - post-execution review failure fails the loop and does not report epic completion

Acceptance:

- The review loop runs after every successful execution slice.
- `.agents` publication includes review ledger and evidence.
- False closure is impossible where components pass but the operational loop never invokes them.

## Milestone 5 - Free-Form Insight Synthesis

Objective: produce compact review support from confirmed non-implementation files before human decisions.

Work:

- Add `SynthesizeNonImplementationInsights.prompt` under `src/LoopRelay.Core/Prompts`.
- Implement `NonImplementationInsightSynthesizer`.
  - Input: unresolved confirmed non-implementation ledger entries, semantic rationale, bounded file content, source paths, and entry IDs.
  - Exclude false positives.
  - Include semantically uncertain entries only in a separate "uncertain, not synthesized as fact" section if useful.
  - Output compact free-form Markdown with source path references and ledger entry IDs.
  - Do not require or produce a structured knowledge schema.
  - Do not authorize keeping, deleting, or promoting files.
  - Use only `INonImplementationReviewRunner`.
- Write synthesis to `.agents/review/non-implementation-synthesis.md` and record its source entry IDs/hashes in the ledger or a small sidecar section.
- Add tests:
  - synthesizer is not invoked when no confirmed entries exist
  - false positives are excluded
  - synthesis output path is stable
  - review set links synthesis to source entries
  - synthesis runner uses read-only review runner only

Acceptance:

- Confirmed non-implementation files can yield a compact synthesis before HITL review.
- Synthesis remains free-form and source-linked.
- Synthesis is review support only.
- Synthesis cannot mutate repository files.

## Milestone 6 - HITL Epic Completion Review

Objective: require explicit human decisions for confirmed non-implementation files at epic completion.

Work:

- Implement `NonImplementationCompletionReviewService`.
  - Begin with a fresh repository review refresh, not ledger state alone.
  - The refresh must detect current changed files not covered by the latest post-execution review and update the ledger before readiness is evaluated.
  - If no unresolved confirmed or semantically uncertain entries exist after refresh, return `Ready`.
  - If decisions are missing, write `.agents/review/non-implementation-review.md` plus a decision template at `.agents/review/non-implementation-decisions.md` and return `Blocked`.
  - Parse human decisions on rerun.
  - Validate every decision target by ledger entry ID, path, reviewed content hash, and reviewed status.
  - Apply `Delete` decisions only when the current file path, current content hash, current status, and ledger entry ID match the reviewed target.
  - If any delete target is stale, replaced, moved, missing unexpectedly, or hash-mismatched, block, rescan, and require a fresh decision.
  - Validate delete paths stay inside the repository and are not under `.agents`.
  - Record `Keep`, `Delete`, `KeepSynthesis`, `DiscardSynthesis`, and semantically uncertain entry resolutions durably.
  - Preserve a `HitlRequested` or `HitlKept` authority reason where the decision states that the human requested the file or chose to retain it.
- Decision template grammar:
  - one table row per unresolved ledger entry
  - required columns: `Entry ID`, `Path`, `Reviewed SHA-256`, `Reviewed Status`, `Decision`, `Authority Reason`
  - allowed file decisions: `Keep`, `Delete`, `ResolveFalsePositive`, `Defer`
  - allowed synthesis decisions in a separate single-row table: `KeepSynthesis`, `DiscardSynthesis`, `DeferSynthesis`
  - parser rejects duplicate entry IDs, unknown decisions, missing required rows, hash/status/path mismatch, and non-empty decisions for entries no longer unresolved
- Main CLI integration:
  - At the top of `LoopRunner.RunAsync`, after `gate.IsEpicCompleteAsync()` returns true and before `completionCertification.CertifyPlanCompletionAsync`, run completion review.
  - If review is blocked, publish `.agents` state, do not clear the decision-session resume state, and return `LoopOutcome.CompletionBlocked`.
  - If review applies parent-repo deletions, commit and push those deletions before certification using a commit path that does not increment the stall counter. Add a narrow `CommitGate.CommitPushIfChangedAsync` helper if needed.
  - Pass review evidence paths to completion certification context so final certification can see the review state.
- Completion integration:
  - Extend `CompletionCertificationRequest` with non-implementation review evidence paths or a review summary path.
  - Include the review summary in `CompletionPromptContextBuilder.BuildEvaluationContextAsync`.
  - Archive `.agents/review` contents with completed epic artifacts.
- Roadmap CLI integration:
  - In `RunCompletionCertificationAsync`, run the same review service before completion evaluation.
  - If blocked, persist `EvidenceBlocked` with the review request path and a next step to fill the decisions template and rerun.
- Add tests:
  - completion review performs a fresh scan before returning ready
  - ledger has no unresolved entries but current prose/report files exist, so review does not falsely return ready
  - epic completion blocks when unresolved confirmed entries exist and no decisions file exists
  - blocked review does not clear decision-session resume state
  - keep decision records HITL authority and allows certification to continue
  - delete decision removes only repository files outside `.agents` when entry ID/path/hash/status match
  - stale delete decision blocks when hash changed after review
  - delete decision rejects path traversal and `.agents` paths
  - synthesis keep/discard is recorded separately from file keep/delete
  - semantically uncertain entry can be resolved as false positive, keep, delete, or deferred

Acceptance:

- Epic completion review happens before final certification closes the epic.
- Readiness is based on a fresh review refresh plus ledger state, not stale ledger state alone.
- The human can keep/delete files, keep/discard synthesis, and resolve semantically uncertain entries.
- Delete decisions cannot remove content that was not reviewed.
- Decisions are durable and auditable.
- The workflow does not become autonomous repository acceptance.

## Milestone 7 - Planning Integration, HITL Authority Capture, And Implementation-First Guidance

Objective: inject centralized implementation-first guidance into planning, execution, decision, roadmap, and completion prompts while capturing explicit non-implementation deliverable authority early.

Work:

- Extend `ImplementationFirstPromptPolicyComposer` with concise text for:
  - default implementation-first mode
  - explicit opt-in mode
  - HITL-requested documentation exception
  - prohibition against autonomous freeze/certification/governance/authority documentation milestones
  - warning against Architecture Tests, Golden Tests, and theory-protection artifacts unless backed by executable evaluation or explicitly requested
- Add `ExplicitNonImplementationAuthorityCaptureService`.
  - Capture only explicit structured authority, not inferred intent.
  - Require generated plans or decisions to declare any human-requested non-implementation deliverable in a stable section.
  - Persist authority entries with deliverable path or path pattern, source artifact path, source hash, authority kind, and rationale.
  - Attach authority to ledger entries when a changed candidate path matches an explicit authority entry.
  - Do not treat authority as a deletion/retention decision; it explains why a non-implementation file may be legitimate.
- Wire settings and authority capture into:
  - `LoopCliComposition`
  - `PlanCliComposition`
  - `RoadmapCliComposition`
  - completion prompt runner composition where needed
- Prompt rendering changes:
  - `WritePlan.prompt`: include policy guidance and require plans to avoid autonomous non-implementation deliverables unless explicitly requested; if requested, list them in a structured explicit-authority section.
  - `RevisePlan.prompt`: carry the same policy and preserve/update the explicit-authority section.
  - `StartExecution.prompt` and `ContinueExecution.prompt`: include policy guidance before the execution command.
  - `GenerateSystemPromptForFirstExecutionAgent.prompt` and `GenerateSystemPromptForNextExecutionAgent.prompt`: instruct the decision agent not to authorize autonomous non-implementation artifacts unless allowed by policy or explicit HITL request, and to preserve structured authority markers.
  - roadmap planning prompts in `src/LoopRelay.Core/Prompts/Planning`: include policy guidance through `RoadmapPromptCatalog.RenderRuntime` or prompt context sections, not ad hoc duplicated text.
  - milestone generation should avoid documentation-centric milestones unless requested.
  - completion prompts should receive review summaries when present.
- Prefer adding a `{promptPolicy}` parameter to generated prompt templates. If a prompt has many call sites, wrap policy appending in a small helper so policy text still comes from one composer.
- Add tests:
  - default settings inject implementation-first guidance into plan, execution, decision, and roadmap prompt paths
  - opt-in setting changes guidance
  - prompt text preserves the HITL-requested documentation exception
  - plan prompt discourages documentation-centric milestones and unsupported Architecture/Golden Tests
  - explicit non-implementation deliverables are captured only from structured authority markers
  - ledger entries can attach captured authority evidence
  - no prompt has a separately hard-coded copy of the policy body outside the composer

Acceptance:

- Relevant prompts receive implementation-first guidance from one composition point.
- Default planning and execution steer away from autonomous non-implementation files.
- Explicitly requested documentation remains allowed and is captured as durable authority when declared.
- HITL authority is captured when it is granted, not reconstructed only at completion.

## Milestone 8 - Architectural Convergence

Objective: simplify only where implementation evidence shows friction or duplication.

Work:

- Review terminology across model names, prompt text, ledger fields, evidence artifacts, and tests.
- Remove duplicated policy wording and duplicated path constants.
- Confirm classification, semantic confirmation, ledger, post-execution integration, synthesis, and completion review have clear ownership.
- Keep review state separate from roadmap decision ledgers and completion certification decisions.
- Collapse helper classes only when boundaries are artificial after implementation.
- Remove or revise any code that implies commit gating, publication gating, repository acceptance, structured knowledge extraction, or documentation debt analysis.
- Add or adjust tests only for behavior that moved during convergence.

Acceptance:

- The implemented capability has stable terminology and clear ownership.
- Prompt policy flow remains centralized.
- The architecture is simpler than immediately after the feature landed.
- No new capability is added during convergence.

## Deferred Work

Do not implement these in this plan:

- structured insight synthesis
- semantic deduplication of extracted knowledge
- repository knowledge projection
- preservation metrics
- repository health analysis
- documentation debt analysis
- semantic garbage collection
- commit gating, publication gating, or repository mutation acceptance
- broad relay/runtime redesign

## Anti-False-Closure Tests

Add tests or explicit review checks for these failure modes:

- Components pass, but no CLI test proves the loop runs after each execution slice.
- Ledger has no unresolved entries, but current changed prose/report files exist at completion.
- A delete decision exists, but the target file hash no longer matches the reviewed ledger entry.
- A deterministic ambiguous route is treated as a final semantic uncertainty and never confirmed.
- Prompt policy appears in prompts, but duplicated hard-coded text diverges from the composer.
- Synthesis is generated and source-linked, but later treated as retention authority.
- A human-requested non-implementation deliverable lacks captured authority and blocks completion without explanation.
- A read-only semantic review accidentally uses a mutation-capable execution spec.

## Validation Strategy

Run focused tests after each milestone, then the full solution before completion:

```powershell
dotnet test tests/LoopRelay.Orchestration.Primitives.Tests/LoopRelay.Orchestration.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Permissions.Tests/LoopRelay.Permissions.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Cli.Tests/LoopRelay.Cli.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Completion.Tests/LoopRelay.Completion.Tests.csproj --no-restore --nologo
dotnet test tests/LoopRelay.Roadmap.Cli.Tests/LoopRelay.Roadmap.Cli.Tests.csproj --no-restore --nologo
dotnet test LoopRelay.slnx --no-restore --nologo
```

If restore state is missing, run:

```powershell
dotnet restore LoopRelay.slnx
dotnet test LoopRelay.slnx --no-restore --nologo
```

## Completion Criteria

The plan is complete when:

- autonomous execution is blocked until projection certification exists for the current plan hash
- every execution slice captures a pre-slice baseline and reviews only execution-produced deltas
- post-execution changed files are discovered, classified, semantically confirmed when needed, and ledgered before `.agents` publication
- implementation and machine-required files reliably avoid semantic review
- confirmed non-implementation, false-positive, and semantic uncertainty states are durably distinct
- repeated unchanged candidates are skipped only by exact path/hash/classifier/prompt identity
- semantic confirmation and synthesis are read-only and mutation-impossible by construction
- compact free-form synthesis is available for confirmed non-implementation files when useful
- epic completion starts with a fresh review refresh
- epic completion blocks for required human decisions and resumes after valid decisions are recorded
- delete decisions require entry ID, path, status, and content hash match before file removal
- human keep/delete/synthesis/uncertain decisions are durable and auditable
- explicit HITL-requested non-implementation deliverables are captured as authority when granted
- default prompts steer toward implementation-first repository growth from one centralized composer
- explicit opt-in and explicit HITL-requested documentation are preserved
- no commit gate, publication gate, repository acceptance platform, or structured knowledge system has been introduced
