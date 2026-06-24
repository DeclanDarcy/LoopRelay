## Milestone 2: Governance Workspace Integration

### Objective

Make the governance lifecycle visible and actionable while preserving `CommandCenter.DecisionSessions` as the lifecycle authority. User-facing product language should be `Governance`; backend, API, and model names should keep `DecisionSession` where that is the established implementation boundary.

### Backend and Shell

- [ ] Reuse existing decision-session read routes in `DecisionSessionEndpoints.cs`.
- [ ] Add a narrow transfer execution endpoint:
   - [ ] `POST /api/repositories/{repositoryId}/decision-sessions/transfers`
   - [ ] Calls `IDecisionSessionTransferService.ExecuteAsync(repositoryId)`.
   - [ ] Returns `DecisionSessionTransferResult`.
   - [ ] Does not execute transfer unless policy and eligibility services allow it.
- [ ] Add a narrow persisted recovery endpoint:
   - [ ] `POST /api/repositories/{repositoryId}/decision-sessions/recovery`
   - [ ] Calls `IDecisionSessionRecoveryService.RecoverAsync(repositoryId)`.
   - [ ] Returns `DecisionSessionRecoveryResult`.
- [ ] Keep `GET /decision-sessions/recovery` as assessment-only and `POST /decision-sessions/recovery` as the persisted recovery trigger.
- [ ] Add Tauri commands for:
   - [ ] session list and active session
   - [ ] diagnostics
   - [ ] metrics, statistics, economics, coherence
   - [ ] lifecycle policy and policy diagnostics
   - [ ] transfer eligibility and eligibility diagnostics
   - [ ] lifecycle projection, history, influence, health
   - [ ] continuity artifacts and artifact lookup
   - [ ] transfers, transfer history, transfer diagnostics, transfer execution
   - [ ] recovery, recovery history, recovery diagnostics, persisted recovery
   - [ ] workflow summary, workflow health, workflow influence
   - [ ] certification get/report/run

### UI

- [ ] Add `src/CommandCenter.UI/src/types/decisionSessions.ts` matching `CommandCenter.DecisionSessions.Models` plus workflow governance projection models.
- [ ] Update `src/CommandCenter.UI/src/types/repositories.ts` to include `decisionSessionSummary` on dashboard and workspace projections.
- [ ] Add `src/CommandCenter.UI/src/api/decisionSessions.ts` and export it.
- [ ] Add hooks for lifecycle projection, policy, eligibility, analysis, transfers, recovery, continuity artifacts, health, and certification.
- [ ] Add repository-level governance summary rendering using `RepositoryDecisionSessionSummary`:
   - [ ] active session id
   - [ ] lifecycle state
   - [ ] lifecycle decision
   - [ ] transfer eligibility status
   - [ ] coherence score
   - [ ] transfer pressure
   - [ ] cache pressure or miss risk
   - [ ] health dimensions
- [ ] Add a dedicated governance workspace under `src/CommandCenter.UI/src/features/governance/`:
   - [ ] `GovernanceWorkspace`
   - [ ] `DecisionSessionLifecyclePanel`
   - [ ] `DecisionSessionAnalysisPanel`
   - [ ] `DecisionSessionEligibilityPanel`
   - [ ] `DecisionSessionTransferPanel`
   - [ ] `DecisionSessionContinuityArtifactPanel`
   - [ ] `DecisionSessionRecoveryPanel`
   - [ ] `DecisionSessionHealthPanel`
   - [ ] `DecisionSessionCertificationPanel`
- [ ] Lifecycle explanation must display authoritative reuse score, transfer score, reason, contributing factors, transfer pressure, cache risk, continuity benefit, coherence, fragmentation, and growth when present.
- [ ] Transfer readiness must distinguish "transfer recommended" from "transfer currently executable".
- [ ] Recovery display must distinguish recovered, diagnosed, requires intervention, duplicate active sessions, interrupted transfers, discarded snapshots, and rebuilt snapshots.
- [ ] Workflow integration must consume only `IWorkflowDecisionSessionService` and `IDecisionSessionObservabilityService` outputs already exposed through workflow and decision-session endpoints.
- [ ] Where governance affects product status, render the workflow gate or required human action next to the governance detail instead of inventing a separate governance workflow.
- [ ] Navigation, page titles, and visible UI labels use Governance terminology while code-facing contracts keep DecisionSession naming where they mirror backend authority.

### Tests

- [ ] Backend endpoint tests for transfer execution and persisted recovery.
- [ ] Repository projection tests proving `decisionSessionSummary` serializes and TypeScript types include it.
- [ ] UI tests for repository governance summary, lifecycle explanation, transfer eligibility, recovery, health, and certification.

### Exit Criteria

- [ ] Decision-session functionality is available through one frontend client.
- [ ] Repository summaries surface governance without detailed duplication.
- [ ] A dedicated Governance Workspace presents lifecycle, analysis, transfer, continuity artifact, recovery, health, certification, and history.
- [ ] Transfer trigger and persisted recovery trigger are reachable through approved UI actions.
- [ ] Workflow reflects governance state without owning it.
- [ ] No duplicate governance state or authority path is introduced.
