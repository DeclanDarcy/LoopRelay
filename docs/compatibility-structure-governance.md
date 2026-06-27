# Compatibility Structure Governance

This inventory is an M0.4 governance detector for compatibility structures. It makes transitional compatibility visible before later contract generation, passive transport, authority restoration, and structural debt milestones decide whether each structure is removed, generated, replaced, or retained under a narrower exception.

## Detection Scope

The executable guard validates the inventory shape below and requires each entry to carry the same governance metadata:

- owner,
- consumers,
- replacement path,
- retirement condition,
- reachable evidence.

The guard also checks that this document remains aligned with the active bounded compatibility routes in `tests/CommandCenter.Backend.Tests/BackendEndpointDispositionTests.cs` and the transitional compatibility command families and Rust mirror inventory in `docs/shell-transport-classification.md`.

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
| `GET /api/ping` | Compatibility route | Backend endpoint disposition tests | Shell backend health command, local development health checks | Keep only as bounded diagnostics compatibility or replace with a typed runtime health/status contract. | Retire compatibility classification if diagnostics health becomes an explicitly shell-owned or backend-runtime contract with dedicated evidence. | `tests/CommandCenter.Backend.Tests/BackendEndpointDispositionTests.cs` |
| `GET /api/repositories/{repositoryId:guid}/planning` | Compatibility route | Backend endpoint disposition tests | Planning readiness consumers and repository workspace planning surfaces | Replace with workflow preparation/readiness projection consumption when planning readiness is folded into the canonical workflow contract. | Retire after workflow readiness consumers no longer require the planning endpoint and endpoint disposition evidence is updated. | `tests/CommandCenter.Backend.Tests/BackendEndpointDispositionTests.cs` |

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
