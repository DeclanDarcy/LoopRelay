# Contract Endpoint Catalog

This catalog expands the Milestone 0.2 contract inventory from family-level entries into endpoint-level route surfaces. It is an inventory and fixture-selection aid, not a generated schema and not a substitute for serialized golden fixtures.

Current scan baseline: 177 backend endpoint mappings under `src/CommandCenter.Backend/Endpoints`, Tauri command wrappers in `src/CommandCenter.Shell/src/main.rs`, TypeScript API wrappers in `src/CommandCenter.UI/src/api`, manual TypeScript types in `src/CommandCenter.UI/src/types`, and development mock payloads in `src/CommandCenter.UI/src/devTauriMock.ts`.

## Inventory Fields

Each endpoint-level inventory entry must eventually include:

| Field | Meaning |
| --- | --- |
| Endpoint or command | Backend route and any Tauri command name that relays it. |
| Backend authority | Service or projection owner that computes semantic meaning. |
| Projection type | Backend projection, command result, primitive, stream, or error envelope exposed externally. |
| Serialization authority | Backend JSON serialization unless the command is shell-owned. |
| Contract identity | Stable Oracle name used by fixtures and generated or verified consumers. |
| Consumers | Shell command, TS API wrapper, TS type, hooks/controllers/components, mocks, and tests. |
| Compatibility consumers | Manual Rust mirrors, manual TS types, dev mocks, characterization tests, docs, or external callers that require compatibility until migrated. |
| Fixture candidate | Whether this endpoint should produce a representative golden fixture. |
| Fixture priority | High, Medium, Low, Later, or No. |

## Consumer Taxonomy

| Consumer class | Current examples | Oracle obligation |
| --- | --- | --- |
| Backend endpoint tests | `tests/CommandCenter.Backend.Tests` | Pin status, shape, null/empty behavior, and error envelope behavior for fixture candidates. |
| Rust shell commands | `src/CommandCenter.Shell/src/main.rs` | Preserve serialized backend payloads and errors; any typed Rust mirror is a compatibility consumer until generated, verified, or retired. |
| TypeScript API wrappers | `src/CommandCenter.UI/src/api/*.ts` | Use contract identity and command argument shape consistently; eventually consume generated or verified types. |
| Manual TypeScript types | `src/CommandCenter.UI/src/types/*.ts` | Compatibility consumers only; not contract authority. |
| Dev Tauri mock | `src/CommandCenter.UI/src/devTauriMock.ts` | Must be generated from, or checked against, Oracle-observed contracts before it can be trusted as development data. |
| React hooks and components | `src/CommandCenter.UI/src/hooks`, `src/CommandCenter.UI/src/App.tsx`, feature surfaces | Consume authoritative facts; must not infer backend-owned semantics from weak strings. |
| Characterization and E2E tests | `src/CommandCenter.UI/src/test`, Playwright tests | Useful downstream compatibility evidence, but not shape authority. |
| Durable docs | `docs/` | May describe accepted contracts and lifecycle, but must not define shape independently of backend serialization and fixtures. |

## Narrow Serialization Rules

These rules are now required before selecting any first golden fixture. Unknowns remain inventory work, not fixture assumptions.

| Concern | Current Oracle rule |
| --- | --- |
| Property naming | The contract shape is the emitted backend JSON property names. Fixture comparison must observe serialized JSON, not C# property names. |
| Identifiers | GUID route values and GUID-valued fields are serialized as strings. Domain string identifiers remain strings and must not be normalized by shell or UI transport. |
| Enums and lifecycle states | Observable enum-like states are serialized as backend-owned strings. UI string unions are compatibility mirrors until generated or verified. |
| Null vs omitted | Explicit `null` and omitted properties are distinct contract states. Transport must preserve explicit nulls; fixtures must record whichever state backend serialization emits. |
| Empty collections | Empty arrays are meaningful and must not be converted to null or omitted by transport, mocks, or resources. |
| Empty objects and strings | Empty objects and empty strings are preserved as emitted. They are not absence unless the backend contract defines them that way. |
| Date/time values | Date/time serialization must be captured from backend JSON before fixture approval. No fixture may assume local UI formatting is contract shape. |
| Ordering | Array order is contract-significant only when the backend projection explicitly owns ordering. Fixture notes must identify whether ordering is semantic or incidental. |
| Unknown fields | Transport and generated/verified consumers must not drop unknown backend fields silently during relay. UI rendering may ignore them after receipt. |
| Error envelopes | Backend error payloads, including `boundaryViolation` when present, are backend-owned contracts. Shell and TS transport may wrap errors for ergonomics but must preserve structured payloads. |
| Streams | Streaming endpoints require stream-specific trace or fixture rules. Single JSON fixture rules do not certify event-stream ordering or reconnection behavior. |
| Compatibility fields | A compatibility field may be fixture-certified only when its authoritative source, consumer list, replacement path, and retirement condition are recorded. |

## Backend JSON Serialization Observation

The backend HTTP JSON configuration is currently `JsonSerializerDefaults.Web` plus `JsonStringEnumConverter` in `src/CommandCenter.Backend/Program.cs`.

Observed implications for Oracle fixtures:

| Concern | Observed backend behavior | Repository dashboard implication |
| --- | --- | --- |
| Property naming | Web defaults emit camelCase property names. | Fixtures must record `repository`, `executionState`, `continuitySummary`, and other emitted camelCase names. |
| Enum representation | `JsonStringEnumConverter` emits enum values as strings. | `availability`, `readiness`, `executionState`, `state`, and `repositoryState` are string-valued contract fields. |
| Null emission | Web defaults do not ignore null values unless an ignore condition is configured. | Nullable dashboard fields such as `activeExecutionSession`, `executionSummary`, `operationalContextLastUpdatedAt`, and decision-session fields emit explicit `null`. |
| Empty collections | Empty arrays are emitted for initialized collection properties. | `executionHistory`, `healthDimensions`, `recentTransferLineage`, and `diagnostics` must remain arrays, including when empty. |
| Date/time values | `DateTimeOffset` values are serialized by System.Text.Json as JSON strings preserving offset/UTC information. | Timestamp fields must be captured from emitted JSON rather than inferred from TypeScript date formatting. |
| Time spans | `TimeSpan` values are serialized by System.Text.Json as JSON strings. | `duration` and `estimatedCacheTtl` are string-valued duration contracts when present. |
| Field ordering | Serialization order follows the current serializer/type metadata, but no semantic ordering guarantee is documented for object properties. | Fixture comparisons should treat object property order as non-semantic unless a later Oracle rule explicitly changes this. |
| Array ordering | Array order is whatever the backend projection returns. | Repository dashboard repository order follows `IRepositoryService.GetAllAsync`; `executionHistory` order is owned by execution session projection behavior. |

## Endpoint Family Coverage

| Family | Route count | Contract identity scope | Primary consumers | Compatibility consumers | Fixture priority |
| --- | ---: | --- | --- | --- | --- |
| Repository | 5 | repository dashboard, registration result, workspace aggregate, refresh result | shell repository commands, TS repository API/types, repository/workspace UI | Rust repository mirrors, manual TS types, dev mock | High |
| Artifacts | 5 | artifact inventory, artifact content, artifact save result, handoff/decision rotation result | shell artifact commands, TS artifact API/types, editor/workspace UI | manual TS types, dev mock, current artifact-file conventions | Medium |
| Planning | 1 | planning milestones projection | TS planning type and UI consumers | manual TS planning type | Low |
| Operational context | 7 | proposal generation, proposal list/detail, content edit, accept/reject/promote results | shell operational-context commands, TS API/types, continuity UI | manual TS types, dev mock | High |
| Continuity | 3 | diagnostics, generated report, report history | shell continuity commands, TS continuity API/types, continuity UI | manual TS types, dev mock | Medium |
| Execution | 3 | execution context preview, start result, active execution | shell execution commands, TS execution API/types, execution UI | Rust execution mirrors, manual TS types, dev mock | High |
| Execution sessions | 8 | session summary, prompt manifest, transparency, status, events, stream, accept/reject results | shell session commands, event consumers, TS execution API/types | Rust execution mirrors, manual TS types, stream format assumptions | High |
| Git | 5 | repository status, eligibility, preparation, commit result, push result | shell Git commands, TS Git/execution API/types, Git UI | Rust Git mirrors, manual TS types, dev mock | High |
| Decisions | 61 | decision context, candidates, lifecycle eligibility, proposals, review, governance, quality, influence, certification | shell decision commands, TS decision API/types, decision/governance UI | manual TS unions/types, dev mock, characterization tests | High |
| Decision sessions | 34 | session registry, diagnostics, analysis, lifecycle, continuity artifacts, transfers, recovery, workflow, certification | shell decision-session commands, TS API/types, governance UI | manual TS types, dev mock | High |
| Reasoning | 21 | events, threads, relationships, graph, traces, query, reconstruction, materialization review, certification | shell reasoning commands, TS reasoning API/types, reasoning UI | manual TS types, dev mock, reasoning persistence docs | High |
| Workflow | 28 | workflow instance, diagnostics, timeline, transitions, gates, recovery, execution, handoff, decisions, operational context, Git, continuation, preparation, health, reports, certification | shell workflow commands, TS workflow API/types, workflow UI | manual TS types, dev mock | High |
| Ping | 1 | ping primitive | shell health check, backend wait logic | none significant | No |

## Priority Endpoint Inventory

These entries are sufficient to pick early fixture candidates after field-level shape and serialization observations are added.

| Contract identity | Backend endpoint | Tauri command | Projection or result | Consumers | Compatibility consumers | Fixture priority |
| --- | --- | --- | --- | --- | --- | --- |
| Repository dashboard | `GET /api/repositories` | `list_repositories` | `RepositoryDashboardProjection[]` | TS repository API/types, repository selector UI | Rust `RepositoryDashboardProjection`, manual TS type, dev mock | High |
| Repository workspace | `GET /api/repositories/{repositoryId}/workspace` | `get_repository_workspace` | `RepositoryWorkspaceProjection` | TS repository API/types, workspace UI | Rust `RepositoryWorkspaceProjection`, manual TS type, dev mock | High |
| Repository workspace refresh | `POST /api/repositories/{repositoryId}/refresh` | `refresh_repository_workspace` | `RepositoryWorkspaceProjection` | refresh hooks and workspace UI | Rust workspace mirror, manual TS type, dev mock | High |
| Artifact inventory | `GET /api/repositories/{repositoryId}/artifacts` | none currently exposed in TS API | artifact inventory projection | backend tests and future artifact inventory UI | endpoint-only shape assumptions | Medium |
| Artifact content | `GET /api/repositories/{repositoryId}/artifacts/content` | `load_artifact_content` | `string` | TS artifact API, editor UI | plain string content assumptions | No |
| Current handoff rotation | `POST /api/repositories/{repositoryId}/artifacts/rotate-current-handoff` | `rotate_current_handoff` | `RepositoryWorkspaceProjection` | TS artifact API, workspace refresh chain | Rust workspace mirror, manual TS type | Medium |
| Current decisions rotation | `POST /api/repositories/{repositoryId}/artifacts/rotate-current-decisions` | `rotate_current_decisions` | `RepositoryWorkspaceProjection` | TS artifact API, workspace refresh chain | Rust workspace mirror, manual TS type | Medium |
| Execution context preview | `GET /api/repositories/{repositoryId}/execution/context` | `preview_execution_context` | `ExecutionContextPreview` | TS execution API/types, execution UI | Rust request/result mirror, manual TS type, dev mock | High |
| Execution start | `POST /api/repositories/{repositoryId}/execution/start` | `start_execution` | `ExecutionSessionSummary` | TS execution API/types, execution UI | Rust `ExecutionSessionSummary`, manual TS type, dev mock | High |
| Active execution | `GET /api/repositories/{repositoryId}/execution/active` | `get_active_execution` | `ExecutionSessionSummary` or absence result | TS execution API/types, execution UI | Rust `ExecutionSessionSummary`, manual TS type | High |
| Execution session summary | `GET /api/execution-sessions/{sessionId}` | `get_execution_session` | session summary/details | TS execution API/types | manual TS type | High |
| Execution prompt manifest | `GET /api/execution-sessions/{sessionId}/prompt` | `get_execution_prompt_manifest` | `ExecutionPromptManifest` | TS execution API/types, prompt UI | manual TS type | High |
| Execution status | `GET /api/execution-sessions/{sessionId}/status` | none currently in TS API | session status projection | future event/status consumers | stream/status assumptions | High |
| Execution events | `GET /api/execution-sessions/{sessionId}/events` | none currently in TS API | event list | future event consumers | manual stream/event assumptions | Medium |
| Execution event stream | `GET /api/execution-sessions/{sessionId}/events/stream` | browser/event stream path via backend URL | event stream | TS `executionEvents.ts`, browser event source | stream shape/order assumptions | Later |
| Git status | `GET /api/repositories/{repositoryId}/git/status` | `get_git_status` | `RepositoryGitStatus` | TS Git API/types, Git UI | Rust `RepositoryGitStatus`, manual TS type, dev mock | High |
| Git eligibility | `POST /api/execution-sessions/{sessionId}/git/eligibility` | `get_execution_git_eligibility` | `ExecutionGitActionEligibility` | TS execution API/types, Git UI | Rust request/result mirrors, manual TS type | High |
| Commit preparation | `POST /api/execution-sessions/{sessionId}/git/prepare-commit` | `prepare_commit` | `CommitPreparation` | TS execution API/types, commit UI | Rust `CommitPreparation`, manual TS type | High |
| Commit execution | `POST /api/execution-sessions/{sessionId}/git/commit` | `commit_execution` | `ExecutionSessionSummary` | TS execution API/types, commit UI | Rust execution mirrors, manual TS type | High |
| Push execution | `POST /api/execution-sessions/{sessionId}/git/push` | `push_execution` | `PushAttemptResult` | TS execution API/types, push UI | Rust `PushAttemptResult`, manual TS type | High |
| Operational context proposal generation | `POST /api/repositories/{repositoryId}/operational-context/generate` | `generate_operational_context_proposal` | `OperationalContextProposal` | TS operational-context API/types, continuity UI | manual TS type, dev mock, characterization tests | High |
| Operational context proposal list | `GET /api/repositories/{repositoryId}/operational-context/proposals` | `list_operational_context_proposals` | proposal list | TS operational-context API/types, continuity UI | manual TS type, dev mock | High |
| Continuity diagnostics | `GET /api/repositories/{repositoryId}/continuity/diagnostics` | `get_continuity_diagnostics` | `ContinuityDiagnostics` | TS continuity API/types, continuity UI | manual TS type, dev mock | Medium |
| Continuity report generation | `POST /api/repositories/{repositoryId}/continuity/reports` | `generate_continuity_report` | `ContinuityReport` | TS continuity API/types, continuity UI | manual TS type, dev mock | Medium |
| Decision lifecycle eligibility | `GET /api/repositories/{repositoryId}/decisions/lifecycle/eligibility` | `get_decision_lifecycle_eligibility` | `DecisionLifecycleEligibilityProjection` | TS decision API/types, decision UI | manual TS type/unions, dev mock | High |
| Decision proposal browser | `GET /api/repositories/{repositoryId}/decisions/proposals/browser` | `list_decision_proposal_browser` | `DecisionProposalBrowserItem[]` | TS decision API/types, proposal browser UI | manual TS type, dev mock | High |
| Decision proposal review | `GET /api/repositories/{repositoryId}/decisions/proposals/{proposalId}/review` | `get_decision_proposal_review` | `DecisionReviewWorkspace` | TS decision API/types, review UI | manual TS type, dev mock | High |
| Decision governance | `GET /api/repositories/{repositoryId}/decisions/governance` | `get_decision_governance` | `DecisionGovernanceReport` | TS decision API/types, governance UI | manual TS type, dev mock | Medium |
| Decision quality report | `GET /api/repositories/{repositoryId}/decisions/quality/reports/current` | `get_decision_quality_report` | `DecisionQualityReport` | TS decision API/types, quality UI | manual TS type, dev mock | Medium |
| Decision certification | `GET /api/repositories/{repositoryId}/decisions/certification` | `get_decision_certification` | `DecisionCertificationReport` | TS decision API/types, certification UI | manual TS type, dev mock | Medium |
| Decision-session active | `GET /api/repositories/{repositoryId}/decision-sessions/active` | `get_active_decision_session` | `DecisionSessionProjection | null` | TS decision-session API/types, governance UI | manual TS type, dev mock | High |
| Decision-session lifecycle projection | `GET /api/repositories/{repositoryId}/decision-sessions/lifecycle/projection` | `get_decision_session_lifecycle_projection` | `DecisionSessionLifecycleProjection` | TS decision-session API/types, governance UI | manual TS type, dev mock | High |
| Decision-session transfer eligibility | `GET /api/repositories/{repositoryId}/decision-sessions/lifecycle/eligibility` | `get_decision_session_transfer_eligibility` | `DecisionSessionTransferEligibility` | TS decision-session API/types, governance UI | manual TS type, dev mock | High |
| Decision-session workflow | `GET /api/repositories/{repositoryId}/decision-sessions/workflow` | `get_decision_session_workflow` | `WorkflowDecisionSessionProjection` | TS decision-session API/types, workflow governance UI | manual TS type, dev mock | High |
| Reasoning graph | `GET /api/repositories/{repositoryId}/reasoning/graph` | `get_reasoning_graph` | `ReasoningGraph` | TS reasoning API/types, reasoning UI | manual TS type, dev mock | High |
| Reasoning backward trace | `GET /api/repositories/{repositoryId}/reasoning/trace/backward` | `trace_reasoning_backward` | `ReasoningTrace` | TS reasoning API/types, trace UI | manual TS type, dev mock | High |
| Reasoning reconstruction report | `POST /api/repositories/{repositoryId}/reasoning/reconstructions/reports` | `run_reasoning_reconstruction` | `ReasoningReconstructionReport` | TS reasoning API/types, reasoning UI | manual TS type, dev mock | Medium |
| Workflow projection | `GET /api/repositories/{repositoryId}/workflow` | `get_workflow_projection` | `WorkflowInstance` | TS workflow API/types, workflow UI | manual TS type, dev mock | High |
| Workflow diagnostics | `GET /api/repositories/{repositoryId}/workflow/diagnostics` | `get_workflow_diagnostics` | diagnostics projection | TS workflow API/types, workflow UI | manual TS type, dev mock | High |
| Workflow recovery | `GET /api/repositories/{repositoryId}/workflow/recovery` | `get_workflow_recovery` | `WorkflowRecoveryDiagnostics` | TS workflow API/types, recovery UI | manual TS type, dev mock | High |
| Workflow health | `GET /api/repositories/{repositoryId}/workflow/health` | `get_workflow_health` | `WorkflowHealthAssessment` | TS workflow API/types, workflow UI | manual TS type, dev mock | Medium |
| Workflow certification | `GET /api/repositories/{repositoryId}/workflow/certification` | `get_workflow_certification` | `WorkflowCertificationResult` | TS workflow API/types, workflow UI | manual TS type, dev mock | Medium |
| Error envelope | all non-success backend responses | shell error handling | backend error payload and optional `boundaryViolation` | TS `TransportError`, UI boundary notices | Rust error serialization, TS parser, tests | High |

## Repository Dashboard Field Ownership Pilot

Contract identity: `Repository dashboard`.

Producer endpoint: `GET /api/repositories`.

Backend projection type: `RepositoryDashboardProjection[]`.

Serialization authority: backend HTTP JSON configuration.

Primary backend owner: `RepositoryProjectionService.GetDashboardAsync`, which composes repository identity, availability, planning readiness, execution state/session summary, artifact-derived counts, continuity summary, reasoning summary, and decision-session summary.

Known consumers:

- Rust Tauri command `list_repositories`.
- TypeScript API wrapper `listRepositories`.
- Manual TypeScript type `RepositoryDashboardProjection`.
- Dev Tauri mock `dashboardEntry`.
- React shell and navigation consumers, including sidebar and selected repository summary surfaces.
- Backend projection tests and frontend characterization tests.

Known compatibility finding:

- The Rust `RepositoryDashboardProjection` mirror currently includes `reasoningSummary` but omits `decisionSessionSummary`, while backend and TypeScript dashboard contracts include `decisionSessionSummary`. This is now protected by recursive `ContractConsumerVerificationTests` as downstream consumer drift evidence for the Oracle and a later passive-transport/manual-mirror retirement slice; it is not corrected by this inventory slice.
- The manual TypeScript `RepositoryDashboardProjection` currently matches the Oracle fixture shape, including imported execution summary aliases and nested decision-session summary arrays. This proves TypeScript is a verified compatibility consumer for the repository dashboard pilot, not a contract authority.

Current consumer verification scope:

- Rust `RepositoryDashboardProjection` root shape.
- Nested Rust `Repository`, `ExecutionSessionSummary`, `RepositoryContinuitySummary`, and `RepositoryReasoningSummary` shape reachable from the dashboard fixture.
- TypeScript `RepositoryDashboardProjection` root shape.
- Nested TypeScript `Repository`, `ExecutionSessionSummary`, `RepositoryContinuitySummary`, `RepositoryReasoningSummary`, `RepositoryDecisionSessionSummary`, `RepositoryDecisionSessionHealthDimension`, and `RepositoryDecisionSessionTransferSummary` shape reachable from the dashboard fixture.
- Missing, extra, and value-kind drift classification.
- Dev mock verification remains pending.

Top-level field catalog:

| JSON field | Backend field/type | Semantic owner | Serialization owner | Consumers | Compatibility field | Required | Derived | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `repository` | `Repository` | Core repository service/configuration | Backend JSON | shell, TS, UI, mock, tests | No | Yes | No | Repository identity object. |
| `availability` | `RepositoryAvailability` | Middle repository projection availability check | Backend JSON string enum | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from repository path, directory access, and `.git` presence. |
| `readiness` | `ExecutionReadiness` | Planning service | Backend JSON string enum | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from plan and milestone artifact state. |
| `executionState` | `RepositoryExecutionState` | Execution session service | Backend JSON string enum | shell, TS, UI, mock, tests | No | Yes | Yes | Repository-level execution lifecycle state. |
| `activeExecutionSession` | `ExecutionSessionSummary` or `null` | Execution session service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Explicit `null` when no active execution session exists. |
| `executionSummary` | `ExecutionSessionSummary` or `null` | Execution session service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Latest/current summary; explicit `null` allowed. |
| `executionHistory` | `ExecutionSessionSummary[]` | Execution session service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Empty array is meaningful. |
| `milestoneCount` | `int` | Artifact inventory/projection service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Count of discovered milestone artifacts. |
| `hasCurrentHandoff` | `bool` | Artifact inventory/projection service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from current handoff artifact discovery. |
| `hasCurrentDecisions` | `bool` | Artifact inventory/projection service | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from current decisions artifact discovery. |
| `continuitySummary` | `RepositoryContinuitySummary` | Continuity projection composition | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Summary of operational-context state. |
| `reasoningSummary` | `RepositoryReasoningSummary` | Reasoning repository/projection composition | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Empty summary is emitted when reasoning repository is absent. |
| `decisionSessionSummary` | `RepositoryDecisionSessionSummary` | Decision-session observability/projection composition | Backend JSON | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Empty summary is emitted when observability service is absent. |

Nested field catalog:

| JSON field path | Backend field/type | Semantic owner | Serialization owner | Consumers | Compatibility field | Required | Derived | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `repository.id` | `Guid` | Core repository service/configuration | Backend JSON string | shell, TS, UI, mock, tests | No | Yes | No | Stable repository identifier. |
| `repository.name` | `string` | Core repository registration | Backend JSON | shell, TS, UI, mock, tests | No | Yes | No | Repository display name. |
| `repository.path` | `string` | Core repository registration | Backend JSON | shell, TS, UI, mock, tests | No | Yes | No | Local path string, not normalized by transport. |
| `continuitySummary.operationalContextExists` | `bool` | Continuity/artifact projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from current operational-context artifact. |
| `continuitySummary.operationalContextRevisionCount` | `int` | Continuity/artifact projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Counts current plus historical operational-context artifacts. |
| `continuitySummary.operationalContextLastUpdatedAt` | `DateTimeOffset?` | Continuity/artifact projection | Backend JSON string/null | shell, TS, UI, mock, tests | No | Yes | Yes | Explicit `null` when no current operational context exists. |
| `continuitySummary.openQuestionCount` | `int` | Continuity parser/projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Count of parsed open questions. |
| `continuitySummary.activeRiskCount` | `int` | Continuity parser/projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Count of parsed active risks. |
| `continuitySummary.pendingProposalExists` | `bool` | Operational-context proposal store/projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Derived from latest proposal status. |
| `reasoningSummary.*Count` | `int` fields | Reasoning repository/projection | Backend JSON | shell, TS, UI, mock, tests | No | Yes | Yes | Event/thread/relationship and event-family counts. |
| `reasoningSummary.last*At` | `DateTimeOffset?` fields | Reasoning repository/projection | Backend JSON string/null | shell, TS, UI, mock, tests | No | Yes | Yes | Explicit `null` when no corresponding activity exists. |
| `reasoningSummary.certificationResult` | `string?` | Reasoning certification/projection | Backend JSON string/null | shell, TS, UI, mock, tests | No | Yes | Yes | Currently nullable summary field. |
| `decisionSessionSummary.decisionSessionId` | `string?` | Decision-session observability | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Stringified active session id, explicit `null` when absent. |
| `decisionSessionSummary.state` | `string?` | Decision-session lifecycle authority | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Stringified active session state. |
| `decisionSessionSummary.lifecycleDecision` | `string?` | Decision-session lifecycle policy | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Stringified lifecycle policy decision. |
| `decisionSessionSummary.transferEligibilityStatus` | `string?` | Decision-session transfer eligibility | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Stringified transfer eligibility status. |
| `decisionSessionSummary.estimatedTokenCount` | `long?` | Decision-session metrics/size authority | Backend JSON number/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Uses size estimate, falling back to metrics estimate. |
| `decisionSessionSummary.estimatedCacheTtl` | `TimeSpan?` | Decision-session metrics/cache authority | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Duration string when present. |
| `decisionSessionSummary.cacheMissRisk` | `decimal?` | Decision-session metrics/cache authority | Backend JSON number/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Numeric risk score when present. |
| `decisionSessionSummary.coherenceScore` | `decimal?` | Decision-session coherence authority | Backend JSON number/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Numeric coherence score when present. |
| `decisionSessionSummary.transferPressure` | `decimal?` | Decision-session coherence authority | Backend JSON number/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Numeric transfer pressure when present. |
| `decisionSessionSummary.healthDimensions` | `RepositoryDecisionSessionHealthDimension[]` | Decision-session health authority | Backend JSON array | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Empty array when absent. |
| `decisionSessionSummary.healthDimensions[].name` | `string` | Decision-session health authority | Backend JSON | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Health dimension name. |
| `decisionSessionSummary.healthDimensions[].status` | `string` | Decision-session health authority | Backend JSON | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Stringified health status. |
| `decisionSessionSummary.healthDimensions[].findings` | `string[]` | Decision-session health authority | Backend JSON array | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Empty array allowed. |
| `decisionSessionSummary.recentTransferLineage` | `RepositoryDecisionSessionTransferSummary[]` | Decision-session transfer authority | Backend JSON array | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Empty array when absent. |
| `decisionSessionSummary.recentTransferLineage[].*At` | `DateTimeOffset`/`DateTimeOffset?` | Decision-session transfer authority | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | `completedAt` is nullable. |
| `decisionSessionSummary.diagnostics` | `string[]` | Decision-session diagnostics authority | Backend JSON array | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Concatenated distinct errors and warnings. |
| `decisionSessionSummary.generatedAt` | `DateTimeOffset?` | Decision-session observability | Backend JSON string/null | TS, UI, mock, backend tests; Rust mirror drift | No | Yes | Yes | Projection generation timestamp, explicit `null` when no observability service exists. |
| `activeExecutionSession.*` | `ExecutionSessionSummary` fields | Execution session service | Backend JSON | shell, TS, UI, mock, tests | No | Conditional | Yes | Shares execution summary contract; explicit `null` when absent. |
| `executionSummary.*` | `ExecutionSessionSummary` fields | Execution session service | Backend JSON | shell, TS, UI, mock, tests | No | Conditional | Yes | Shares execution summary contract; explicit `null` when absent. |
| `executionHistory[]` | `ExecutionSessionSummary` items | Execution session service | Backend JSON array | shell, TS, UI, mock, tests | No | Yes | Yes | Empty array when no history exists. |

## Remaining Catalog Work

- Add dev mock consumer verification against the repository dashboard Oracle fixture.
- Map every Decision, DecisionSession, Reasoning, and Workflow endpoint to a specific backend service/projection type rather than family-level authority.
- Classify shell-owned commands separately from backend-relay commands.
- Add an Oracle dependency graph showing backend projection type to endpoint to shell command to TS API/type to UI consumer.
