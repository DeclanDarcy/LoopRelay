# Implementation-First Non-Implementation File Review Plan

## Purpose

Implement a post-execution review loop that detects repository files created or changed outside implementation code, confirms likely non-implementation artifacts with semantic review, records durable review state, optionally synthesizes useful information, and presents the result for explicit human review at epic completion.

The default operating mode is implementation-first: autonomous repository growth should normally be code, tests, project files, runtime configuration, generated build artifacts that are intentionally tracked, or LoopRelay operational artifacts under `.agents`. Non-implementation files remain allowed when the human explicitly requested that deliverable or explicitly chooses to keep it during review.

This is not a commit gate, publication gate, repository acceptance system, repository knowledge platform, or documentation debt analyzer. It is a post-execution review loop plus centralized prompt guidance.

## Target Capability

After each execution slice:

```text
post-execution changed-file snapshot
-> deterministic parallel classification
-> semantic confirmation for routed candidates
-> durable non-implementation review ledger
```

At epic completion:

```text
confirmed non-implementation files
+ false positives
+ uncertain candidates
+ optional compact synthesis
-> human keep/delete/synthesis decisions
-> durable decision record
-> completion certification proceeds only after required review decisions exist
```

Planning and execution prompts should also receive centralized implementation-first guidance unless the user has explicitly opted in to autonomous non-implementation file generation.

## Vocabulary

Use these terms consistently in code, tests, persisted JSON, prompt text, and review artifacts:

- `Implementation artifact`: source code, tests, UI assets, migrations, project files, build scripts, package manifests, generated source that is intentionally tracked, and other files required to implement, build, test, or run the product.
- `Machine-required artifact`: lockfiles, solution/project files, configuration files, generated manifests, prompt resources compiled into the product, schema files, CI files, and other non-code files required by tools or runtime behavior.
- `Sanctioned operational artifact`: LoopRelay runtime files under `.agents`, including plans, milestones, handoffs, decisions, deltas, evidence, state, projections, archives, ledgers, and review artifacts.
- `Non-implementation candidate`: a changed file that deterministic classification cannot safely exclude and that should be semantically reviewed.
- `Confirmed non-implementation file`: a candidate semantically confirmed as a non-implementation artifact.
- `False positive`: a routed candidate semantically determined to be implementation, machine-required, sanctioned operational, or otherwise not a non-implementation artifact.
- `Uncertain candidate`: a candidate whose purpose cannot be responsibly confirmed.
- `HITL-requested non-implementation file`: a non-implementation file specifically requested by the human or retained by an explicit human decision.
- `Free-form insight synthesis`: compact review support extracted from confirmed non-implementation files before keep/delete decisions; it is not a structured knowledge store.

## Architecture

Add shared review primitives under `src/LoopRelay.Orchestration.Primitives` because the capability is used by the main CLI loop, completion certification, and roadmap/planning prompt flow. Avoid a new project unless implementation shows the primitives cannot stay cohesive there.

Recommended folders:

- `Models/NonImplementationReview/`
- `Services/NonImplementationReview/`
- `Abstractions/NonImplementationReview/`

Primary shared components:

- `RepositoryChangeSetDetector`: obtains a post-execution changed-file snapshot from git. Use porcelain status as the authoritative path list so untracked files are included; record `git diff --name-status` metadata where available for tracked files.
- `NonImplementationArtifactClassifier`: deterministic classifier that runs per changed file and produces route, evidence, and exclusion reason.
- `NonImplementationReviewLedgerStore`: JSON ledger under `.agents/review/non-implementation-ledger.json` with schema versioning.
- `NonImplementationSemanticConfirmer`: read-only agent runner that confirms candidates with strict structured output.
- `NonImplementationInsightSynthesizer`: optional read-only/free-form synthesis runner for confirmed non-implementation files.
- `NonImplementationCompletionReviewService`: assembles the completion review set, writes the pending review request, parses human decisions, applies delete decisions, and records durable outcomes.
- `ImplementationFirstPromptPolicyComposer`: central source of prompt guidance derived from CLI settings.

Canonical artifact paths should live in `OrchestrationArtifactPaths` where possible:

- `.agents/review/non-implementation-ledger.json`
- `.agents/review/non-implementation-review.md`
- `.agents/review/non-implementation-decisions.md`
- `.agents/review/non-implementation-synthesis.md`
- `.agents/evidence/non-implementation/`

Completion-specific code may keep compatibility constants in `CompletionArtifactPaths`, but it should delegate to the orchestration paths instead of inventing duplicate literals.

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

Objective: establish terminology, settings, prompt-policy composition, and ownership without implementing the full review loop.

Work:

- Add shared vocabulary types and documentation. Prefer concise code-level records/enums plus a short `docs/non-implementation-review.md` because this capability specifically requires terminology and boundaries to be durable.
- Add `NonImplementationArtifactPolicyOptions` and settings loader support.
- Add `ImplementationFirstPromptPolicyComposer` that returns stable guidance text for implementation-first and opt-in modes.
- Define review ownership:
  - changed-file detection and classification live in orchestration primitives
  - semantic confirmation and synthesis are orchestration services composed by hosts with a read-only agent spec
  - main CLI invokes the post-execution identification loop after execution writes and before the `.agents` post-execution publish
  - epic-completion review runs before completion certification closes the epic
  - roadmap/planning prompts consume the centralized prompt policy text
- Add focused tests in `LoopRelay.Orchestration.Primitives.Tests` and `LoopRelay.Permissions.Tests` for settings defaults and policy text selection.

Acceptance:

- Terms are represented by explicit types or documented constants.
- `allowAutonomousNonImplementationFiles` defaults to implementation-first mode.
- One prompt-policy composer produces all implementation-first guidance.
- Later milestones can use stable paths, model names, and ownership boundaries.

## Milestone 1 - Changed File Classification

Objective: deterministically route changed files that may be non-implementation artifacts.

Work:

- Implement `RepositoryChangeSetDetector` using `IProcessRunner` and `Repository`.
  - Parse `git status --porcelain` as the authoritative changed path list.
  - Include untracked, modified, added, deleted, renamed, and staged paths.
  - Filter out `.agents` only after recording sanctioned operational evidence.
  - Capture path, status, deletion flag, existence, extension, size if available, content hash if readable, and tracked diff metadata when available.
- Implement `NonImplementationArtifactClassifier`.
  - Exclude implementation artifacts by source/test directory, recognized code extensions, UI asset paths, migrations, scripts, prompt resources, and generated source conventions.
  - Exclude machine-required artifacts such as `.slnx`, `.csproj`, `.props`, `.targets`, lockfiles, package manifests, CI files, settings templates, JSON schema/config files used by runtime or tests, and source-generator inputs.
  - Exclude sanctioned operational artifacts under `.agents`, including the review ledger and evidence created by this capability.
  - Route likely prose/design/audit/roadmap/report files as `NonImplementationCandidate`.
  - Route unknown ambiguous files as `UncertainCandidate` so semantic confirmation can decide.
  - Emit classification evidence with rule ID, path facts, and rationale.
- Run classification with `Task.WhenAll` over the changed-file records.
- Add tests:
  - code files under `src` and `tests` are excluded
  - `.csproj`, `.slnx`, package/config files, prompt resources, and lockfiles are excluded
  - `.agents` operational files are excluded as sanctioned
  - root/docs/issues Markdown files route as candidates
  - unknown ambiguous files route as uncertain
  - untracked files are discovered
  - renames use the destination path

Acceptance:

- Changed-file discovery includes untracked files.
- Classification is deterministic for the same repository state.
- Code and machine-required files reliably avoid semantic review.
- Candidate output includes enough evidence for semantic review and debugging.

## Milestone 2 - Semantic Candidate Confirmation

Objective: confirm routed candidates with structured agent output.

Work:

- Add `ConfirmNonImplementationCandidate.prompt` under `src/LoopRelay.Core/Prompts`.
- Prompt requirements:
  - input includes candidate path, deterministic evidence, status, content hash, and bounded content excerpt or instructions to inspect the file read-only
  - output is strict JSON or an exact Markdown field table parsed into:
    - candidate path
    - disposition: `ConfirmedNonImplementation`, `FalsePositive`, or `Uncertain`
    - concise rationale
    - evidence excerpts or path facts
    - uncertainty note when applicable
  - prompt explicitly forbids keep/delete decisions
- Implement parser and validation for the structured output.
- Implement `NonImplementationSemanticConfirmer`.
  - Consume only candidates from deterministic classification.
  - Skip candidates with valid ledger entries for the same path, content hash, classifier version, and prompt source hash.
  - Treat false positives as normal outcomes.
  - Preserve uncertainty instead of forcing a binary answer.
- Host composition:
  - main CLI and completion tests can pass a read-only review agent spec factory
  - do not depend on `LoopRelay.Cli.AgentSpecs` from shared primitives
- Add tests:
  - parser accepts each valid disposition
  - parser rejects missing/unknown disposition
  - service skips known ledger entries
  - service confirms candidates and records rationale
  - service does not process deterministic exclusions

Acceptance:

- Every routed candidate receives a durable semantic disposition or is skipped by a valid ledger entry.
- False positives and uncertainty are first-class outcomes.
- Semantic confirmation does not decide retention or deletion.

## Milestone 3 - Non-Implementation Review Ledger

Objective: persist semantic review state and suppress duplicate identification work.

Work:

- Add ledger records with:
  - schema version
  - entry ID
  - path
  - content hash
  - git status at discovery
  - deterministic classification route and evidence
  - semantic disposition
  - semantic rationale and evidence
  - classifier version
  - confirmation prompt source hash
  - first seen and last seen timestamps
  - resolution state: `Unresolved`, `HitlKept`, `HitlDeleted`, `HitlFalsePositive`, `HitlDeferred`
  - human decision metadata when present
- Keep confirmed, false-positive, and uncertain entries distinguishable in the same JSON document. Expose query methods that return them separately.
- Add `NonImplementationReviewLedgerStore` with schema validation and stable ordering.
- Add Markdown evidence rendering for human-readable snapshots under `.agents/evidence/non-implementation/`.
- Duplicate suppression rule:
  - skip semantic confirmation only when path, content hash, classifier version, and confirmation prompt source hash match a valid existing semantic disposition
  - re-confirm when the file content changes or when the classifier/prompt version changes
- Add tests:
  - writes schema version
  - records confirmed, false-positive, and uncertain entries separately
  - same path/hash suppresses duplicate confirmation
  - changed hash requires confirmation
  - invalid schema blocks with a clear error

Acceptance:

- Ledger state is repository-local and durable.
- False positives are never merged into confirmed review state.
- Known unchanged files avoid repeated semantic confirmation.
- Synthesis and HITL review can consume ledger state directly.

## Milestone 4 - Free-Form Insight Synthesis

Objective: produce compact review support from confirmed non-implementation files before human decisions.

Work:

- Add `SynthesizeNonImplementationInsights.prompt` under `src/LoopRelay.Core/Prompts`.
- Implement `NonImplementationInsightSynthesizer`.
  - Input: unresolved confirmed non-implementation ledger entries, semantic rationale, bounded file content, and source paths.
  - Exclude false positives.
  - Include uncertain entries only in a separate "uncertain, not synthesized as fact" section if useful.
  - Output compact free-form Markdown with source path references.
  - Do not require or produce a structured knowledge schema.
  - Do not authorize keeping, deleting, or promoting files.
- Write synthesis to `.agents/review/non-implementation-synthesis.md` and record its source entry IDs/hashes in the ledger or a small sidecar section.
- Add tests:
  - synthesizer is not invoked when no confirmed entries exist
  - false positives are excluded
  - synthesis output path is stable
  - review set links synthesis to source entries

Acceptance:

- Confirmed non-implementation files can yield a compact synthesis before HITL review.
- Synthesis remains free-form and source-linked.
- Synthesis is review support only.

## Milestone 5 - HITL Epic Completion Review

Objective: require explicit human decisions for confirmed non-implementation files at epic completion.

Work:

- Implement `NonImplementationCompletionReviewService`.
  - Load ledger state.
  - Assemble unresolved confirmed entries, false positives, uncertain entries, and optional synthesis.
  - If no unresolved confirmed or uncertain entries exist, return `Ready`.
  - If decisions are missing, write `.agents/review/non-implementation-review.md` plus a decision template at `.agents/review/non-implementation-decisions.md` and return `Blocked`.
  - Parse human decisions on rerun.
  - Apply `Delete` decisions to repository files only after validating the path stays inside the repository and is not under `.agents`.
  - Record `Keep`, `Delete`, `KeepSynthesis`, `DiscardSynthesis`, and uncertain-candidate resolutions durably.
  - Preserve a `HitlRequested` or `HitlKept` authority reason where the decision states that the human requested the file or chose to retain it.
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
  - In `RunCompletionCertificationAsync`, run the same review service before completion evaluation when there are unresolved entries.
  - If blocked, persist `EvidenceBlocked` with the review request path and a next step to fill the decisions template and rerun.
- Add tests:
  - epic completion blocks when unresolved confirmed entries exist and no decisions file exists
  - blocked review does not clear decision-session resume state
  - keep decision records HITL authority and allows certification to continue
  - delete decision removes only repository files outside `.agents`
  - delete decision rejects path traversal and `.agents` paths
  - synthesis keep/discard is recorded separately from file keep/delete
  - uncertain candidate can be resolved as false positive, keep, delete, or deferred

Acceptance:

- Epic completion review happens before final certification closes the epic.
- The human can keep/delete files, keep/discard synthesis, and resolve uncertain candidates.
- Decisions are durable and auditable.
- The workflow does not become autonomous repository acceptance.

## Milestone 6 - Planning Integration and Implementation-First Guidance

Objective: inject centralized implementation-first guidance into planning, execution, decision, roadmap, and completion prompts.

Work:

- Extend `ImplementationFirstPromptPolicyComposer` with concise text for:
  - default implementation-first mode
  - explicit opt-in mode
  - HITL-requested documentation exception
  - prohibition against autonomous freeze/certification/governance/authority documentation milestones
  - warning against Architecture Tests, Golden Tests, and theory-protection artifacts unless backed by executable evaluation or explicitly requested
- Wire settings into:
  - `LoopCliComposition`
  - `PlanCliComposition`
  - `RoadmapCliComposition`
  - completion prompt runner composition where needed
- Prompt rendering changes:
  - `WritePlan.prompt`: include policy guidance and require plans to avoid autonomous non-implementation deliverables unless explicitly requested.
  - `RevisePlan.prompt`: carry the same policy during plan revision.
  - `StartExecution.prompt` and `ContinueExecution.prompt`: include policy guidance before the execution command.
  - `GenerateSystemPromptForFirstExecutionAgent.prompt` and `GenerateSystemPromptForNextExecutionAgent.prompt`: instruct the decision agent not to authorize autonomous non-implementation artifacts unless allowed by policy or explicit HITL request.
  - roadmap planning prompts in `src/LoopRelay.Core/Prompts/Planning`: include policy guidance through `RoadmapPromptCatalog.RenderRuntime` or prompt context sections, not ad hoc duplicated text.
  - milestone generation should avoid documentation-centric milestones unless requested.
  - completion prompts should receive review summaries when present.
- Prefer adding a `{promptPolicy}` parameter to generated prompt templates. If a prompt has many call sites, wrap policy appending in a small helper so policy text still comes from one composer.
- Add tests:
  - default settings inject implementation-first guidance into plan, execution, decision, and roadmap prompt paths
  - opt-in setting changes guidance
  - prompt text preserves the HITL-requested documentation exception
  - plan prompt discourages documentation-centric milestones and unsupported Architecture/Golden Tests
  - no prompt has a separately hard-coded copy of the policy body outside the composer

Acceptance:

- Relevant prompts receive implementation-first guidance from one composition point.
- Default planning and execution steer away from autonomous non-implementation files.
- Explicitly requested documentation remains allowed.

## Milestone 7 - Architectural Convergence

Objective: simplify only where implementation evidence shows friction or duplication.

Work:

- Review terminology across model names, prompt text, ledger fields, evidence artifacts, and tests.
- Remove duplicated policy wording and duplicated path constants.
- Confirm classification, semantic confirmation, ledger, synthesis, and completion review have clear ownership.
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

- post-execution changed files are discovered, classified, and semantically confirmed when needed
- implementation and machine-required files reliably avoid non-implementation review
- confirmed non-implementation, false-positive, and uncertain states are durably distinct
- repeated unchanged candidates are skipped by ledger identity
- compact free-form synthesis is available for confirmed non-implementation files when useful
- epic completion blocks for required human decisions and resumes after decisions are recorded
- human keep/delete/synthesis/uncertain decisions are durable and auditable
- default prompts steer toward implementation-first repository growth
- explicit opt-in and explicit HITL-requested documentation are preserved
- no commit gate, publication gate, repository acceptance platform, or structured knowledge system has been introduced
