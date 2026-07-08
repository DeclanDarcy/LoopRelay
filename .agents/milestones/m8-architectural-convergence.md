# Milestone 8 - Architectural Convergence

## Objective

simplify only where implementation evidence shows friction or duplication, without adding capability or changing the review-loop intent.

## Work
- [x] Review terminology across model names, prompt text, ledger fields, evidence artifacts, and tests.
- [x] Remove duplicated policy wording and duplicated path constants.
- [x] Confirm classification, semantic confirmation, ledger, post-execution integration, synthesis, and completion review have clear ownership.
- [x] Keep review state separate from roadmap decision ledgers and completion certification decisions.
- [x] Collapse helper classes only when boundaries are artificial after implementation.
- [x] Remove or revise any code that implies commit gating, publication gating, repository acceptance, structured knowledge extraction, or documentation debt analysis.
- [x] Preserve every behavior required by the HITL-described post-execution review loop; do not use convergence to add features, change policy semantics, or weaken implementation-first prompt guidance.
- [x] Add or adjust tests only for behavior that moved during convergence.

## Detail Notes

Convergence should reduce ambiguity or duplication introduced while landing the feature. It must not add new capability, broaden the review loop into governance, or reinterpret HITL design intent to fit implementation preferences.

Review these seams after the implementation exists:

- vocabulary consistency across models, JSON fields, prompt text, tests, evidence files, and review output
- prompt policy flow from `ImplementationFirstPromptPolicyComposer`
- canonical artifact path ownership through `OrchestrationArtifactPaths`
- `CompletionArtifactPaths` delegation to orchestration constants for shared `.agents` paths
- classification, semantic confirmation, ledger, post-execution review, synthesis, and completion review ownership
- separation between non-implementation review state, roadmap decision ledgers, and completion certification decisions
- helper classes whose boundaries proved artificial after implementation

Remove or revise any code, prompt text, test name, or evidence wording that implies:

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

Do not remove tests solely because the architecture was simplified. Move or update tests when behavior moves; delete tests only when the covered behavior was intentionally removed from scope.

## Acceptance
- [x] The implemented capability has stable terminology and clear ownership.
- [x] Prompt policy flow remains centralized.
- [x] The architecture is simpler than immediately after the feature landed.
- [x] No new capability is added during convergence.
