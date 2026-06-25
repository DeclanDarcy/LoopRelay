## Milestone 9: Product Cohesion

### Objective

Make the application feel unified, not merely smaller. Remove fragmentation so every semantic concept has one authority, one projection, one primary navigation path, and one primary presentation.

### Implementation

- [x] Audit navigation for workflow, decision sessions, decisions, execution, reasoning, operational context, repository, health, diagnostics, and certification.
- [x] Define one primary home and allowed contextual links for each capability.
- [x] Consolidate duplicate workflow displays, governance summaries, execution monitoring views, reasoning confidence displays, continuity evolution summaries, health widgets, and certification summaries.
  - [x] Execution monitoring/history workspace duplicates converted to contextual summaries. See `./m9-execution-consolidation.md`.
  - [x] Execution workflow display converted to contextual summary linking to primary workflow operations. See `./m9-workflow-consolidation.md`.
  - [x] Selected repository governance summary converted to contextual summary linking to primary Governance workspace. See `./m9-governance-summary-consolidation.md`.
  - [x] Execution decision influence display converted to contextual summary linking to primary Decisions workspace. See `./m9-decision-influence-consolidation.md`.
  - [x] Selected repository reasoning summary converted to contextual counts/status/latest activity with navigation to primary Reasoning workspace. See `./m9-reasoning-summary-consolidation.md`.
  - [x] Selected repository and workspace inspector continuity summaries converted to revision/warning/proposal/latest activity status with navigation to primary Continuity and Operational Context surfaces. See `./m9-continuity-summary-consolidation.md`.
  - [x] Selected repository health and certification summaries converted to counts/status/latest assessment/latest run with navigation to primary Governance and Reasoning workspaces. See `./m9-health-certification-summary-consolidation.md`.
- [x] Review backend endpoints and classify each as `Keep`, `Redirect`, `Internal`, or `Remove`.
  - [x] Registered backend route dispositions are now guarded by endpoint disposition tests. See `./m9-backend-endpoint-disposition-verification.md`.
- [x] Review projections and classify each as authoritative, derived consumer, compatibility, or retire.
- [x] Review frontend state and classify each state value as authoritative view state, derived display state, disposable UI state, or duplicate domain state.
- [ ] Normalize interaction patterns for review, accept, reject, transfer, recover, generate, refine, commit, push, promote, archive, and supersede:
   - [x] Proposal review lifecycle actions normalized through a shared interaction summary covering action, eligibility, evidence, result, and diagnostics. See `./m9-interaction-normalization-proposal-review.md`.
   - [x] Resolved decision supersede/archive actions normalized through the shared interaction summary. See `./m9-interaction-normalization-decision-actions.md`.
   - [x] Candidate promote/dismiss/expire/duplicate and proposal generation actions normalized through the shared interaction summary. See `./m9-interaction-normalization-candidate-actions.md`.
   - [x] Refinement and resolution panels normalized through thin phase-specific wrappers around the shared interaction summary. See `./m9-interaction-normalization-refinement-resolution.md`.
   - [x] Execution commit and push/retry actions normalized through a thin execution-specific wrapper around the shared interaction summary. See `./m9-interaction-normalization-execution-git.md`.
   - [x] Execution recovery transparency normalized through a thin execution-specific wrapper around the shared interaction summary. See `./m9-interaction-normalization-execution-recovery.md`.
   - [x] Decision-session transfer actions normalized through the shared interaction summary. See `./m9-interaction-normalization-governance-transfer.md`.
   - [x] Interaction consistency audit completed and governance recovery action presentation normalized through the shared interaction summary. See `./m9-interaction-consistency-audit.md`.
   - [x] action
   - [x] eligibility
   - [x] evidence
   - [x] result
   - [x] diagnostics
- [x] Build or update a unified operational dashboard that summarizes:
   - [x] workflow
   - [x] governance
   - [x] execution
   - [x] operational context
   - [x] reasoning
   - [x] repository
   - [x] health
   - [x] certification
   - [x] diagnostics
   - [x] Unified selected-repository operational dashboard completed. See `./m9-operational-dashboard.md`.
- [x] Delete obsolete UI components, old workflow derivation, duplicate panels, temporary views, deprecated widgets, obsolete summaries, and unused client functions after replacements are tested.
  - [x] Duplicate execution artifact diagnostics renderer removed in favor of the shared explainability diagnostics renderer. See `./m9-obsolete-ui-cleanup-artifact-diagnostics.md`.
  - [x] Duplicate decision quality priority signal renderer removed in favor of the shared explainability diagnostics renderer. See `./m9-obsolete-ui-cleanup-decision-quality-signals.md`.
  - [x] Duplicate decision option comparison evidence renderer removed in favor of shared decision evidence fragments and `EvidenceList`. See `./m9-obsolete-ui-cleanup-decision-option-evidence.md`.
  - [x] Duplicate decision recommendation and burden explanation renderers removed in favor of shared `DecisionBasis`, `EvidenceList`, and `DiagnosticList`. See `./m9-obsolete-ui-cleanup-decision-explanations.md`.
  - [x] Duplicate generation certification executive-readiness evidence and blocking-gap renderers removed in favor of shared `EvidenceList` and `DiagnosticList`. See `./m9-obsolete-ui-cleanup-generation-certification.md`.
  - [x] Duplicate governance lifecycle factor and analysis warning renderers removed in favor of shared `EvidenceList` and `DiagnosticList`. See `./m9-obsolete-ui-cleanup-governance-signals.md`.
  - [x] Duplicate reasoning graph fallback diagnostics and operational-context warning lists removed in favor of shared `DiagnosticList`. See `./m9-obsolete-ui-cleanup-continuity-reasoning-diagnostics.md`.
  - [x] Duplicate decision lifecycle/revision diagnostics, revision source attribution, and execution conflict evidence renderers removed in favor of shared `DiagnosticList` and `EvidenceList`. See `./m9-obsolete-ui-cleanup-decision-execution-diagnostics-evidence.md`.
  - [x] Duplicate decision option evaluation evidence, option diagnostics, analyzed-option diagnostics, and operational-context modification supporting-evidence renderers removed in favor of shared `EvidenceList` and `DiagnosticList`. See `./m9-obsolete-ui-cleanup-decision-continuity-explainability.md`.
  - [x] Execution git commit/push surface terminology aligned so it no longer presents a competing workflow authority label. See `./m9-obsolete-ui-cleanup-execution-git-workflow-terminology.md`.
  - [x] Final health/certification renderer audit completed; remaining local surfaces are intentional domain wrappers or compact dashboard rollups. See `./m9-health-certification-renderer-audit.md`.
- [x] Align terminology across statuses, health, diagnostics, recovery, certification, governance, execution, and explainability.
  - [x] Terminology and primary surface reachability audit completed for the primary workspace tab strip, contextual section labels, and Git Evidence naming. See `./m9-terminology-reachability-audit.md`.

### Likely Cleanup Targets

- [x] `src/CommandCenter.UI/src/lib/executionWorkflow.ts` after workflow projection integration.
- [x] Any rail or status component that still consumes `RepositoryExecutionState` as a workflow source.
- [x] Duplicate decision recommendation, quality, governance, and influence summaries replaced by explainability components.
  - [x] Decision quality priority signals now render through shared diagnostics.
  - [x] Decision option comparison evidence now renders through shared evidence components.
  - [x] Decision recommendation and burden explanation details now render through shared decision-basis components; governance explanation remains a navigation/grouping wrapper over shared diagnostics.
- [x] Duplicate health renderers replaced by shared `HealthView`.
- [x] Duplicate diagnostics renderers replaced by shared `DiagnosticList`.
  - [x] Execution artifact diagnostics list local renderer removed; `DiagnosticList` is now the only visible artifact diagnostics renderer.
  - [x] Generation certification executive-readiness blocking gaps now render through shared diagnostics.
  - [x] Reasoning graph fallback diagnostics and operational-context warning diagnostics now render through shared diagnostics.
  - [x] Decision lifecycle generation diagnostics, revision lineage diagnostics, revision source attribution, and execution conflict evidence now render through shared explainability components.
  - [x] Decision option and analyzed-option diagnostics now render through shared diagnostics.
  - [x] Workflow certification failure rendering now uses shared diagnostics. See `./m9-obsolete-ui-cleanup-workflow-certification-failures.md`.
  - [x] Final health/certification audit found no remaining duplicate generic diagnostic renderers requiring replacement. See `./m9-health-certification-renderer-audit.md`.

### Tests

- [x] Navigation characterization tests.
- [x] UI tests proving primary surfaces remain reachable.
- [x] Static or unit tests for removed duplicate helpers where practical.
- [x] Backend endpoint disposition tests for retained routes.

### Exit Criteria

- [x] Every major capability has one obvious primary navigation path.
- [x] Every semantic concept has one authoritative projection and one primary presentation.
- [x] Duplicate endpoints, projections, views, and components are removed or intentionally retained with documented purpose.
- [x] Interaction patterns are consistent across the product.
- [x] The dashboard gives a coherent overview without replacing detailed workspaces.

Final validation evidence: see `./m9-final-cohesion-validation.md`.
