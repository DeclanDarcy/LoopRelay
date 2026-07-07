# Roadmap - Implementation-First Non-Implementation File Review

## Purpose

This roadmap evolves LoopRelay so that the epic implementation loop can detect, review, and avoid unnecessary non-implementation repository artifacts while preserving HITL authority.

The intended capability is a post-execution review loop:

```text
git diff
-> changed file list
-> deterministic parallel classification
-> semantic candidate confirmation
-> review ledger
-> optional free-form insight synthesis
-> HITL review at epic completion
```

The roadmap also adds prompt guidance so planning and execution, by default, avoid autonomously creating non-implementation files unless the HITL specifically requested them.

This is not a repository governance redesign. It does not make preservation a commit gate, publication gate, or general repository acceptance system.

---

## Guiding Principles

* Repository growth should primarily occur through implementation artifacts.
* Implementation code and machine-required files should reliably route away from non-implementation review.
* False positives are acceptable for candidate routing, but they must be recorded separately once identified.
* Expensive semantic reasoning should run only after cheap deterministic classification.
* The review ledger prevents repeated identification of files the system has already understood.
* HITL review occurs at epic completion and decides whether to keep or delete non-implementation files and whether to keep any extracted synthesis.
* MVP insight synthesis is compact and free-form. Structured synthesis is deferred until the system learns the right shape.
* Non-implementation file generation is an explicit opt-in setting. By default, LoopRelay should run in implementation-first mode and hard-steer away from autonomously generating non-implementation files.
* In the default implementation-first mode, non-implementation documentation is allowed only when the HITL specifically requested that documentation or deliverable.
* Prompt guidance should discourage documentation milestones, certification/freeze/governance/authority document generation, Architecture Tests, Golden Tests, and other theory-protection artifacts that are not backed by executable evaluation.
* The architecture should evolve existing LoopRelay concepts instead of creating a parallel repository governance platform.

---

# Milestone 0 - Architectural Foundation

## Objective

Resolve only the architectural questions needed to implement the non-implementation file review loop cleanly.

Avoid implementing classification, semantic confirmation, ledger behavior, synthesis, review workflow, or enforcement.

## Scope

Establish:

* the vocabulary for implementation artifacts, machine-required artifacts, sanctioned LoopRelay operational artifacts, non-implementation candidates, confirmed non-implementation files, false positives, and HITL-requested non-implementation files
* the repository policy setting that controls explicit opt-in to non-implementation file generation, including the default implementation-first behavior and the HITL-requested documentation exception
* the prompt policy composition point used by later planning and execution prompts
* the responsibility boundary for post-execution non-implementation file review

## Exit Criteria

LoopRelay has enough architectural clarity to implement the review loop without first resolving ownership, terminology, or policy propagation questions.

---

# Milestone 1 - Changed File Classification

## Objective

Introduce deterministic, false-positive-tolerant classification of changed files from `git diff`.

## Scope

Implement:

* changed file discovery from the post-execution diff
* parallel deterministic classification for each changed file
* reliable exclusion of implementation code and machine-required files
* routing of likely or uncertain non-implementation files as candidates
* classification evidence sufficient for debugging and downstream confirmation

The classifier routes files. It does not semantically confirm them, record review decisions, extract insight, or ask the HITL for decisions.

## Exit Criteria

After execution, LoopRelay can produce a candidate list of changed files that may be non-implementation artifacts while reliably excluding code and machine-required files from that list.

---

# Milestone 2 - Semantic Candidate Confirmation

## Objective

Use an execution agent with structured output to confirm whether each routed candidate is actually non-implementation.

## Scope

Implement semantic confirmation that returns:

* confirmed non-implementation
* false positive
* uncertain

The semantic stage may include concise rationale and evidence, but its primary MVP responsibility is binary candidate confirmation with explicit false-positive handling.

It does not decide whether files remain in the repository.

## Exit Criteria

Every candidate routed from deterministic classification has a structured semantic disposition that downstream ledger and review stages can consume.

---

# Milestone 3 - Non-Implementation Review Ledger

## Objective

Persist review state so already identified files do not need to be re-identified.

## Scope

Implement a repository-local ledger that records:

* confirmed non-implementation files
* false positives separately
* uncertain candidates requiring later attention
* enough provenance to explain and reuse the result

The ledger supports duplicate suppression for already identified files.

It is not a repository knowledge database, repository acceptance system, or commit gate.

## Exit Criteria

LoopRelay can skip semantic confirmation for files already represented by valid ledger entries and can distinguish confirmed non-implementation files from false positives.

---

# Milestone 4 - Free-Form Insight Synthesis

## Objective

Extract compact, free-form insight from confirmed non-implementation files before HITL review.

## Scope

Implement optional MVP synthesis that:

* summarizes meaningful implementation-relevant information for HITL review
* remains free-form rather than structured
* is generated before keep/delete decisions
* does not assume the final structure of future knowledge extraction

The synthesis is review support. It does not authorize keeping, deleting, or promoting files.

## Exit Criteria

When confirmed non-implementation files contain meaningful information, LoopRelay can present a compact free-form synthesis for HITL consideration before deletion or retention decisions.

---

# Milestone 5 - HITL Epic Completion Review

## Objective

Present confirmed non-implementation files, false positives, uncertain candidates, and optional synthesis to the HITL at epic completion.

## Scope

Implement a review workflow where the HITL can decide whether to:

* keep non-implementation files
* delete non-implementation files
* keep the synthesis
* discard the synthesis
* resolve uncertain candidates

The workflow preserves explicit HITL authority. Non-implementation files remain acceptable when they were specifically requested by the HITL.

## Exit Criteria

At epic completion, LoopRelay can present the review set and record HITL decisions without converting the review loop into autonomous repository acceptance.

---

# Milestone 6 - Planning Integration and Implementation-First Guidance

## Objective

Conditionally inject implementation-first guidance so planning and execution prompts avoid generating non-implementation artifacts unless the HITL specifically requested them or the user has explicitly opted in to non-implementation file generation.

## Scope

Integrate the user setting into:

* roadmap generation
* milestone generation
* plan generation
* execution prompts
* decision prompts

When the explicit opt-in is absent, guidance should discourage:

* documentation-centric milestones such as freeze, certification, governance, and authority document milestones
* epic or milestone details that prescribe autonomous non-implementation file generation
* Architecture Tests, Golden Tests, and theory-protection artifacts not backed by executable evaluation

The setting defaults to implementation-first behavior. Unless the user explicitly opts in to non-implementation file generation, guidance should hard-steer away from autonomous non-implementation artifacts. If the HITL specifically requests non-implementation documentation or a non-implementation deliverable, planning and execution should honor that request regardless of the default.

## Exit Criteria

LoopRelay planning and execution naturally converge toward implementation-first work while preserving explicit HITL authority over requested non-implementation artifacts.

---

# Milestone 7 - Architectural Convergence

## Objective

After the capability exists, simplify architecture where implementation evidence shows that simplification is justified.

## Scope

Review:

* terminology
* responsibility boundaries
* prompt policy flow
* classification and confirmation orchestration
* ledger ownership
* review flow

Convergence must reduce ambiguity or duplication introduced by the implemented capability. It must not add new functionality or broaden the project into repository governance.

## Exit Criteria

The implemented capability feels native to LoopRelay, with clear ownership and less planning or architectural ambiguity than the system had before the epic.

---

# Deferred Opportunities

The following are intentionally outside the current roadmap:

* structured insight synthesis
* semantic deduplication of extracted knowledge
* repository knowledge projection
* preservation metrics
* repository health analysis
* documentation debt analysis
* semantic garbage collection
* repository mutation acceptance, commit gating, or publication gating unless the HITL explicitly confirms that broader architecture later

---

# Success Criteria

The completed epic should satisfy the following properties:

## Implementation-First Repository Growth

Autonomous repository growth is implementation-driven by default.

## Reliable Exclusion

Implementation code and machine-required files reliably avoid non-implementation review.

## False-Positive Tolerance

False positives are acceptable during deterministic candidate routing and are recorded separately after semantic confirmation.

## HITL Authority

Non-implementation files persist because the HITL specifically requested or explicitly chose to keep them.

## Review Timing

HITL review occurs at epic completion and is informed by ledger state and optional free-form synthesis.

## Minimal Scope

The capability remains a post-execution review loop plus conditional prompt guidance, not a general repository acceptance architecture.
