# Shell Transport Classification

This document records the Milestone 0.3 shell regression classification inventory. It does not migrate shell behavior. It gives later passive-transport work a guarded vocabulary for command-family responsibility and Rust mirror state.

## Classification Vocabulary

Command-family responsibility categories:

| Category | Meaning | Accepted examples |
| --- | --- | --- |
| Passive transport | The Tauri command forwards a backend-owned request and returns backend-owned JSON or scalar content without interpreting domain meaning. | `get_workflow_projection`, `list_reasoning_events`, `get_decision_context`, `load_artifact_content` |
| Shell-owned operations | The command owns native shell, process, dialog, or lifecycle behavior that is not backend domain semantics. | `select_repository_directory`, `get_backend_url`, `ping_backend`, backend process startup and shutdown |
| Transitional compatibility | The command still uses Rust request or response structs that mirror backend domain contracts until generated contracts or opaque transport replaces them. | `list_repositories`, `get_repository_workspace`, `get_active_execution`, `get_git_status`, `prepare_commit`, `push_execution` |
| Unknown / requires review | The command family cannot be classified from current evidence and must not be certified as passive. | None identified in this M0.3 inventory; future additions start here until classified. |

Rust mirror properties:

| Property | Accepted values | Meaning |
| --- | --- | --- |
| Current state | Passive, Mirror, Compatibility, Unknown | What the current shell code does today. |
| Target state | Passive, Shell-owned, Retired, Quarantined | The intended M1.3 or later disposition. |

## Command-Family Inventory

| Family | Representative commands | Current category | Target category | Evidence | Known gap |
| --- | --- | --- | --- | --- | --- |
| Backend lifecycle and shell metadata | `ping_backend`, `get_backend_url`, backend startup/shutdown | Shell-owned operations | Shell-owned operations | Shell owns sidecar process lifecycle and exposes runtime URL/health. | Error behavior is not yet part of the passive transport certification claim. |
| Native repository selection | `select_repository_directory` | Shell-owned operations | Shell-owned operations | Native dialog selection is shell responsibility. | None for passive transport; still needs UI contract coverage later if command shape changes. |
| Repository catalog | `list_repositories`, `register_repository`, `remove_repository` | Transitional compatibility | Passive transport or generated command contract | `list_repositories` returns `Vec<RepositoryDashboardProjection>`; registration uses `RegisterRepositoryRequest`. | Dashboard Rust mirror has known downstream drift and must be retired or generated later. |
| Repository workspace and artifacts | `get_repository_workspace`, `refresh_repository_workspace`, `load_artifact_content`, `save_artifact_content`, `rotate_current_handoff`, `rotate_current_decisions` | Transitional compatibility | Passive transport or generated command contract | Workspace and artifact rotation return `RepositoryWorkspaceProjection`; content load is scalar transport; save uses request body mirror. | Workspace Rust mirror omits known fields and must not become contract authority. |
| Operational context | `generate_operational_context_proposal`, `list_operational_context_proposals`, proposal get/edit/accept/reject/promote | Passive transport | Passive transport | Responses use `serde_json::Value`; edit/review bodies are compatibility request adapters. | POST body preservation needs later generic helper coverage. |
| Decision candidates and proposals | decision context, candidate lifecycle, proposal review/refinement/resolution, assimilation, option comparison, evidence inspection | Passive transport | Passive transport | Responses use `serde_json::Value`; transition helpers forward backend-owned JSON bodies. | Non-boundary error semantics and body preservation remain uncertified. |
| Decision governance, certification, quality, and influence | governance reports, certification reports, quality reports/trends, execution-decision influence | Passive transport | Passive transport | Responses use `serde_json::Value`. | Broader route metadata is not certified here. |
| Reasoning | events, manual capture, threads, relationships, graph, trace, query, reconstruction, materialization review, certification | Passive transport | Passive transport | Responses and command bodies use `serde_json::Value`. | Query/body transport is classified but not yet covered by Rust relay tests. |
| Continuity diagnostics and reports | `get_continuity_diagnostics`, `generate_continuity_report`, `list_continuity_reports` | Passive transport | Passive transport | Responses use `serde_json::Value`. | Report-generation POST semantics remain outside current Rust tests. |
| Workflow | projection, diagnostics, timeline, history, transitions, gates, recovery, execution, handoff, decisions, operational context, Git, continuation, preparation, health, reports, certification | Passive transport | Passive transport | Responses use `serde_json::Value`; primary workflow projection is already request-boundary verified. | Sibling workflow endpoints and write commands need later request/body classification. |
| Decision sessions | session list, active session, diagnostics, metrics, statistics, economics, coherence, lifecycle, transfer, recovery, workflow, certification | Passive transport | Passive transport | Responses use `serde_json::Value`. | Large route family remains route-metadata inventory only. |
| Execution and Git | `start_execution`, `get_active_execution`, `get_git_status`, `get_execution_git_eligibility`, `prepare_commit`, `commit_execution`, `push_execution`, execution session/prompt/transparency, handoff accept/reject | Transitional compatibility | Passive transport or generated command contract | Execution/Git has typed request and response mirrors for summaries, status, commit preparation, eligibility, and push result; some session detail reads already use `Value`. | Push outcome, retryability, status, and error behavior are backend-owned semantics and require M1.3 passivity protection. |

## Rust Mirror Inventory

| Rust struct or group | Current state | Target state | Reason | Retirement or quarantine condition |
| --- | --- | --- | --- | --- |
| `Repository`, `RepositoryDashboardProjection`, `RepositoryContinuitySummary`, `RepositoryReasoningSummary` | Mirror | Retired | These mirror backend dashboard/workspace projection shape and can silently drift. | Replace with generated contract or opaque `serde_json::Value` relay, then remove manual mirror. |
| `RepositoryWorkspaceProjection`, `ArtifactInventory`, `Artifact`, `OperationalContextProposalSummary`, `OperationalContextProjection`, `OperationalContextItem` | Mirror | Retired | These mirror repository workspace and operational-context projection fields. | Replace with generated contract or opaque relay and prove no UI consumer loses fields. |
| `ExecutionSessionSummary`, `RepositoryDirtyState`, `RepositoryGitStatus`, `CommitScopeItem`, `CommitStatusSnapshot`, `CommitPreparation`, `PushAttemptResult` | Mirror | Retired | These mirror execution, Git, commit, and push results, including backend-owned state and retry semantics. | Replace with generated contract or opaque relay after Git/execution compatibility coverage exists. |
| `RegisterRepositoryRequest`, `SaveArtifactContentRequest`, `OperationalContextProposalContentRequest`, `OperationalContextProposalReviewRequest`, `ExecutionStartRequest`, `ExecutionAcceptanceRequest`, `CommitRequest`, `PushRequest`, `ExecutionGitActionEligibilityRequest` | Compatibility | Retired | These are shell-side request body adapters for backend command contracts. | Replace with generated command metadata/body types or a verified opaque body relay. |
| `ErrorResponse` | Compatibility | Quarantined | The shell currently parses backend errors to preserve boundary-violation envelopes while returning legacy string errors. | Replace with typed transport error preservation; keep only if documented as a compatibility adapter. |
| `BackendProcess` | Passive | Shell-owned | This is shell-owned sidecar lifecycle state, not a backend domain mirror. | Keep as shell-owned; do not classify as contract authority. |

## Regression Metadata

| Metadata field | Value |
| --- | --- |
| Invariant | Transport preserves request, response, status, null, empty, and error semantics without domain participation. |
| Mechanism | Shell command-family classification document plus backend architecture meta-regression. |
| Owner | Shell architecture tests |
| Severity | CI failure |
| Drift class | Transport responsibility growth |
| Evidence | `.agents/milestones/m0.3-shell-regression-classification-slice-0046.md` |
| Confidence | Inventory confidence |
| Lifecycle | Inventory |
| Certification use | Provides M0.3 shell-surface framework coverage; it does not certify passive transport behavior. |
