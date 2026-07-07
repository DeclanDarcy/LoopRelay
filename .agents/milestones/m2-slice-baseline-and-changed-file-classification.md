# Milestone 2 - Slice Baseline And Changed File Classification

## Objective

deterministically route only files changed by the current execution slice.

## Work
- [x] Implement `RepositorySliceBaselineStore`.
  - [x] Capture a pre-slice snapshot immediately before the execution agent runs.
  - [x] Capture a post-slice snapshot immediately after the execution agent completes.
  - [x] Compare path status, existence, and content hash to compute execution-produced changes.
  - [x] Exclude pre-existing dirty files that did not change during the slice.
  - [x] Include pre-existing dirty files that the execution slice further changed, with `PreExisted = true`.
  - [x] Include new untracked files created during the slice.
  - [x] Assign an `ExecutionSliceId` and persist baseline metadata where necessary for crash-safe review.
- [x] Implement `RepositoryChangeSetDetector` using `IProcessRunner` and `Repository`.
  - [x] Parse `git status --porcelain` for changed path discovery.
  - [x] Record `git diff --name-status` metadata where available for tracked files.
  - [x] Include untracked, modified, added, deleted, renamed, and staged paths.
  - [x] Capture path, status, baseline status, post status, deletion flag, existence, extension, size if available, baseline hash if readable, post hash if readable, and tracked diff metadata when available.
  - [x] Filter out `.agents` only after recording sanctioned operational evidence.
- [x] Implement `NonImplementationArtifactClassifier`.
  - [x] Exclude implementation artifacts by source/test directory, recognized code extensions, UI asset paths, migrations, scripts, prompt resources, and generated source conventions.
  - [x] Exclude machine-required artifacts such as `.slnx`, `.csproj`, `.props`, `.targets`, lockfiles, package manifests, CI files, settings templates, JSON schema/config files used by runtime or tests, and source-generator inputs.
  - [x] Exclude sanctioned operational artifacts under `.agents`, including the review ledger and evidence created by this capability.
  - [x] Route likely prose/design/audit/roadmap/report files as `SemanticReviewCandidate`.
  - [x] Route unknown ambiguous files as `AmbiguousForSemanticReview`.
  - [x] Emit classification evidence with rule ID, path facts, and rationale.
- [x] Run classification with `Task.WhenAll` over the slice delta records.
- [x] Add tests:
  - [x] code files under `src` and `tests` are excluded
  - [x] `.csproj`, `.slnx`, package/config files, prompt resources, and lockfiles are excluded
  - [x] `.agents` operational files are excluded as sanctioned
  - [x] root/docs/issues Markdown files route as candidates when changed by the slice
  - [x] unknown ambiguous files route as ambiguous for semantic review
  - [x] pre-existing dirty files unchanged by execution are excluded from the slice delta
  - [x] pre-existing dirty files modified by execution are included with baseline facts
  - [x] untracked files created during execution are discovered
  - [x] renames use the destination path

## Detail Notes

The HITL-described flow says `git diff -> changed file list`, but the current plan's baseline store is necessary to preserve that intent without treating the whole dirty tree as current execution output.

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

## Acceptance
- [x] Changed-file discovery includes untracked files without treating the whole dirty tree as current execution output.
- [x] Classification is deterministic for the same baseline and post-slice state.
- [x] Code and machine-required files reliably avoid semantic review.
- [x] Candidate output includes enough evidence for semantic review, ledger identity, and debugging.
