# Compatibility Structure Governance

This inventory is an M0.4 governance detector for compatibility structures. It makes transitional compatibility visible before later contract generation, passive transport, authority restoration, and structural debt milestones decide whether each structure is removed, generated, replaced, or retained under a narrower exception.

## Detection Scope

The executable guard validates the inventory shape below and requires each entry to carry the same governance metadata:

- owner,
- consumers,
- replacement path,
- retirement condition,
- reachable evidence.

The guard also checks that this document remains aligned with the active bounded compatibility routes in `tests/LoopRelay.Backend.Tests/BackendEndpointDispositionTests.cs` and the transitional compatibility command families and Rust mirror inventory in `docs/shell-transport-classification.md`.

## Compatibility Kinds

| Kind | Definition | Governance use |
| --- | --- | --- |
| Compatibility field | Transitional serialized property retained for existing consumers while structured authority or generated contracts catch up. | Must identify derivation source or later proof target, consumers, replacement path, retirement condition, and evidence. |
| Compatibility route | Legacy or compatibility endpoint retained for current shell, UI, health, planning, or integration consumers. | Must identify owner, consumers, replacement route or retirement condition, and evidence. |
| Compatibility command | Legacy transport command or command family retained while generated command contracts or passive relay replace manual transport surfaces. | Must identify owner, consumers, replacement path, retirement condition, and evidence. |
| Compatibility mirror | Transitional Rust or TypeScript request/response model, request adapter, or error adapter that parallels backend-owned contract shape. | Must identify owner, consumers, replacement path, retirement condition, and evidence. |

## Compatibility Field Inventory

| Compatibility structure | Kind | Owner | Consumers | Replacement path | Retirement condition | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `WorkflowInstance` flattened workflow status and eligibility fields | Compatibility field | Workflow and contract architecture tests | Backend workflow endpoint consumers, manual TypeScript workflow contract, shell workflow projection command, workflow UI | Replace compatibility review of flattened fields with explicit backend-owned semantic fields in generated contract artifacts. | Retire or reclassify after generated workflow contracts cover structured lifecycle and eligibility semantics and consumer verification proves no workflow consumer depends on stale flattened compatibility. | `.agents/milestones/m0.2-workflow-fixture-field-classification-slice-0027.md` |

## Compatibility Route Inventory

| Compatibility structure | Kind | Owner | Consumers | Replacement path | Retirement condition | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `GET /api/ping` | Compatibility route | Backend endpoint disposition tests | Shell backend health command, local development health checks | Keep only as bounded diagnostics compatibility or replace with a typed runtime health/status contract. | Retire compatibility classification if diagnostics health becomes an explicitly shell-owned or backend-runtime contract with dedicated evidence. | `tests/LoopRelay.Backend.Tests/BackendEndpointDispositionTests.cs` |
| `GET /api/repositories/{repositoryId:guid}/planning` | Compatibility route | Backend endpoint disposition tests | Planning readiness consumers and repository workspace planning surfaces | Replace with workflow preparation/readiness projection consumption when planning readiness is folded into the canonical workflow contract. | Retire after workflow readiness consumers no longer require the planning endpoint and endpoint disposition evidence is updated. | `tests/LoopRelay.Backend.Tests/BackendEndpointDispositionTests.cs` |
| Legacy execution-session command and Git routes under `POST/GET /api/execution-sessions/{sessionId:guid}/…` (status, prompt, transparency, events, events/stream, accept, reject, git/eligibility, git/prepare-commit, git/commit, git/push) | Compatibility route | `LoopRelay.Execution` (`ExecutionEndpoints`, `ExecutionSessionsEndpoints`, `GitEndpoints`) | Legacy execution/Git shell command family and execution UI; orchestration-loop rollback target (rollback path 5) | None — these are the documented rollback surface and remain registered unmodified in `Program.cs` alongside the additive loop routes. | Retire only if and when the orchestration loop's downstream Decision Submit gate fully supersedes the legacy `AwaitingAcceptance` acceptance/commit/push workflow and no rollback target is required (no current retirement condition). | `src/LoopRelay.Backend/Program.cs` (`MapExecutionEndpoints`, `MapExecutionSessionsEndpoints`, `MapGitEndpoints`); `docs/orchestration-loop-governance.md` |

## Compatibility Command Inventory

| Compatibility structure | Kind | Owner | Consumers | Replacement path | Retirement condition | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Repository catalog shell command family | Compatibility command | Shell architecture tests | Tauri repository list/register/remove commands and repository selection UI | Replace with passive relay or generated command contract for repository catalog operations. | Retire after repository catalog commands no longer use manual Rust request or response mirrors and consumer verification proves repository dashboard data is preserved. | `docs/shell-transport-classification.md` |
| Repository workspace and artifacts shell command family | Compatibility command | Shell architecture tests | Tauri workspace, refresh, artifact save, and rotation commands plus workspace UI | Replace with passive relay or generated command contract for repository workspace and artifact operations. | Retire after workspace/artifact commands no longer use manual Rust request or response mirrors and Oracle consumer verification proves no field loss. | `docs/shell-transport-classification.md` |
| Execution and Git shell command family | Compatibility command | Shell architecture tests | Tauri execution, Git status, commit preparation, commit, push, and eligibility commands plus execution UI | Replace with passive relay or generated command contract for execution and Git operations. | Retire after execution/Git commands preserve backend status, retryability, null/empty, and error semantics without shell-owned domain mirrors. | `docs/shell-transport-classification.md` |

## Compatibility Mirror Inventory

| Compatibility structure | Kind | Owner | Consumers | Replacement path | Retirement condition | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `Repository`, `RepositoryDashboardProjection`, `RepositoryContinuitySummary`, `RepositoryReasoningSummary` | Compatibility mirror | Shell architecture tests | Tauri repository catalog commands and repository dashboard UI | Replace with generated contract or opaque `serde_json::Value` relay. | Retire after generated/passive replacement is verified and dashboard consumers do not lose fields such as `decisionSessionSummary`. | `docs/shell-transport-classification.md` |
| `RepositoryWorkspaceProjection`, `ArtifactInventory`, `Artifact`, `OperationalContextProposalSummary`, `OperationalContextProjection`, `OperationalContextItem` | Compatibility mirror | Shell architecture tests | Tauri repository workspace and operational-context consumers | Replace with generated contract or opaque relay. | Retire after workspace and operational-context commands preserve backend-owned projection shape without manual Rust mirrors. | `docs/shell-transport-classification.md` |
| `ExecutionSessionSummary`, `RepositoryDirtyState`, `RepositoryGitStatus`, `CommitScopeItem`, `CommitStatusSnapshot`, `CommitPreparation`, `PushAttemptResult` | Compatibility mirror | Shell architecture tests | Tauri execution/Git commands and execution UI | Replace with generated contract or opaque relay. | Retire after Git/execution compatibility coverage proves status, retryability, and push semantics remain backend-owned. | `docs/shell-transport-classification.md` |
| `RegisterRepositoryRequest`, `SaveArtifactContentRequest`, `OperationalContextProposalContentRequest`, `OperationalContextProposalReviewRequest`, `ExecutionStartRequest`, `ExecutionAcceptanceRequest`, `CommitRequest`, `PushRequest`, `ExecutionGitActionEligibilityRequest` | Compatibility mirror | Shell architecture tests | Tauri command request bodies for repository, artifact, operational-context, execution, commit, push, and eligibility operations | Replace with generated command metadata/body types or verified opaque body relay. | Retire after request-body preservation is verified for the affected command families. | `docs/shell-transport-classification.md` |
| `ErrorResponse` | Compatibility mirror | Shell architecture tests | Tauri backend error handling and boundary-violation compatibility behavior | Replace with typed transport error preservation. | Retire or reclassify after passive transport preserves backend error envelopes without legacy string-error rewriting. | `docs/shell-transport-classification.md` |
| m8-frozen orchestration-loop run-event types (`src/LoopRelay.UI/src/types/planning.ts`, `executionRun.ts`, `decisionRun.ts`) and the generated `src/LoopRelay.UI/src/contracts/generated/repository-dashboard.generated.ts` | Compatibility mirror | Orchestration contract Oracle (m8 freeze) | Orchestration-loop UI hooks/streams (`usePlanStatus`, `usePlanStream`, `useExecutionStream`, `useDecisionStream`) and the repository dashboard projection | These TypeScript shapes mirror backend-owned loop contracts and must stay byte-identical; the backend producers (`PlanStatus`/`PlanLifecycleState`, the plan/execution/decision stream events) are the authority and may only extend the shape additively. | Retire the freeze only when a generated-contract pipeline regenerates these shapes from the backend producers and the consumer/freshness suites prove no field loss; until then the byte-freeze is the compatibility guarantee. | `tests/LoopRelay.Backend.Tests/OrchestrationConsumerContractTests.cs`, `ContractGeneratedArtifactPipelineTests.cs` |

## Orchestration Loop Compatibility Impact

The Plan Authoring → Execution → Decision orchestration loop (milestones m0–m10) is an additive subsystem layered onto the existing backend; it does not modify, migrate, or retire any structure inventoried above. The full evidence register, rollback paths, and intentional divergences are in `docs/orchestration-loop-governance.md`; the implementation overview is in `docs/architecture.md` (Orchestration Loop Architecture). This section records the loop's compatibility obligations against the four consumer surfaces this governance document tracks.

- **Existing execution sessions (legacy `LoopRelay.Execution`).** The legacy execution-session subsystem — `ExecutionEndpoints`, `ExecutionSessionsEndpoints`, `GitEndpoints`, `HandoffService.ProcessProviderCompletionAsync` (the `AwaitingAcceptance` transition), `ExecutionSessionService.AcceptAsync`/`RejectAsync`, `RepositoryExecutionState` (`Ready`/`Executing`/`AwaitingAcceptance`/`Accepted`/`AwaitingCommit`/`AwaitingPush`), and the single-file `execution-sessions.json` store (`FileSystemExecutionSessionStore`) — is retained **unmodified** and remains registered in `Program.cs`. It is the documented rollback target (rollback path 5 in `docs/orchestration-loop-governance.md`). No migration is forced: the loop's human gate moved downstream to the Decision Submit gate (`BeginSubmitDecisionsAsync`), but the legacy acceptance/commit/push state machine is untouched and stays reachable.
- **Generated TypeScript artifacts.** The three m8-frozen run-event type files (`planning.ts`, `executionRun.ts`, `decisionRun.ts` under `src/LoopRelay.UI/src/types/`) and the generated `repository-dashboard.generated.ts` (under `src/LoopRelay.UI/src/contracts/generated/`) stay byte-identical. The backend loop producers are the authority; their wire fields are additive (for example `ExecutionPromptManifest.Provenance` is nullable, and `PlanLifecycleState` — `PlanAuthoring`/`ExecutingPlan` — is a new additive projection surfaced through `PlanStatus`, separate from the wire-coupled `RepositoryExecutionState`). The loop's new endpoints add routes without changing any existing generated shape. These mirrors are inventoried in the Compatibility Mirror Inventory and guarded by the consumer/freshness contract suites.
- **UI hooks.** The loop adds new hooks and streams (`usePlanStatus`, `usePlanStream`, `useExecutionStream`, `useDecisionStream`) bound to the additive `plan/*`, `execution/stream`, `decision/*`, and `conversation` routes. Existing legacy hooks (`useExecutionSession`, `useExecutionGitEligibility`, `useDecisionSessions`, and the surrounding execution/decision-session hooks) are unchanged. Rollback path 1 is a UI mount gate (`isAuthoringSessionActive`), not a backend flag: holding it closed leaves the loop endpoints registered but undriven.
- **Tests.** Loop coverage is additive. The m8 contract goldens (the consumer/freshness/request-boundary suites and the frozen TypeScript shapes) stay byte-identical; new guards (`RepositoryOrchestratorFeatureFlagsTests`, `ProcessLeakDetectionTests`, `OrchestratorShutdownAndRemovalTests`, the recovery-window certification, and the rollback-surface guard `OrchestrationGovernanceTests`) are added alongside the existing endpoint-disposition and shell-transport guards rather than replacing them.

No legacy route is retired: the legacy execution-session/Git routes remain inventoried in the Compatibility Route Inventory above and registered in `Program.cs`. The additive loop routes are net-new — they are **not** compatibility routes and are not inventoried as such; their new endpoint families (`Plan` from `…/plan/`, `DecisionRuntime` from `…/decision/`, and `Conversation`) are classified distinct from the legacy `Decisions` (`…/decisions`), `DecisionSessions` (`…/decision-sessions`), and `Execution` (`…/execution`, `/api/execution-sessions`, `…/git`) families by `BackendEndpointDispositionTests`. The m10 `OrchestrationFeatureFlags` (`PersistentPlanningProcessEnabled`, `PersistentDecisionProcessReuseEnabled`, `TransferOnlyDecisionFallbackEnabled`, `AutomaticCommitPushAfterExecuteEnabled`) default to today's behavior, so the additive surface is a no-op overlay until a deployment explicitly opts out.

## Exclusions

- This inventory does not scan every source field, property, method, or markdown use of the word compatibility.
- Archived `.agents/` material is excluded because it is historical evidence.
- UI text that uses the word compatibility as presentation copy is not classified here unless it represents a governed transitional contract or transport structure.
- This slice does not certify derivation correctness for compatibility fields; later contract, authority, and projection mechanisms own that proof.

## Non-Claims

This inventory does not prove:

- that each compatibility structure is still necessary,
- that each compatibility structure derives correctly from authoritative structure,
- that all compatibility debt has been found,
- that shell mirrors are safe to keep indefinitely,
- or that later compatibility retirement work can be skipped.
