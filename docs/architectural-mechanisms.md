# Architectural Mechanisms

Architectural mechanisms are executable or governed protections that make architecture invariants observable, verifiable, enforceable, recoverable, or easier to maintain.

## Verification Baseline

Introduced: Milestone 0.1.

Status: certified for local command-line verification with bounded quarantines.

Primary evidence:

- `.agents/milestones/m0.1-structural-verification-slice-0001.md`
- `.agents/milestones/m0.1-structural-verification-slice-0002.md`
- `.agents/milestones/m0.1-structural-verification-certification.md`

Accepted verifier entry points:

| Surface | Command | Notes |
| --- | --- | --- |
| .NET build | `dotnet build CommandCenter.slnx` | Run serially with backend tests. |
| Backend tests | `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` | Run serially with solution build. |
| TypeScript build | `npm run build` in `src/CommandCenter.UI` | Runs `tsc -b` before Vite. |
| Frontend lint | `npm run lint` in `src/CommandCenter.UI` | Static frontend verification. |
| Frontend tests | `npm run test` in `src/CommandCenter.UI` | Vitest characterization suite. |
| Browser E2E | `npm run test:e2e` in `src/CommandCenter.UI` | Playwright workspace coverage. |
| Rust build | `cargo build` in `src/CommandCenter.Shell` | Shell compiler health. |
| Rust tests | `cargo test` in `src/CommandCenter.Shell` | Test harness executes and includes shell behavior regressions for successful opaque JSON relay and boundary-violation error-envelope relay. |

## Current Quarantines

| Mechanism gap | Risk | Retirement condition |
| --- | --- | --- |
| Missing CI baseline | Local-only verification is not remotely enforced. | Add CI that runs the accepted baseline or an approved supported subset. |
| Serialized .NET execution | Parallel build/test runs can contend for shared outputs. | Prove isolated output paths or another build isolation mechanism. |
| Rust shell behavioral coverage partial | Shell transport can still drift outside the protected generic GET helper paths. | Add command-family classification, POST relay, non-boundary error semantics, and domain-mirror retirement coverage before shell behavior certification. |
| IDE verification path unknown | IDE feedback can disagree with command-line verification. | Inventory IDE validation or formally scope it outside verifier authority. |
| Tauri packaged release path unknown | Release packaging can fail outside local compile checks. | Add or quarantine a release verifier before release-path claims. |

## Passive Transport Invariants

Introduced: Milestone 0.1 follow-on preparation for Milestones 0.3, 1.2, and 1.3.

Status: seeded with executable Rust regressions for successful opaque JSON relay and boundary-violation error-envelope relay.

Primary evidence:

- `src/CommandCenter.Shell/src/main.rs` test `backend_get_value_relays_opaque_json_without_interpretation`
- `src/CommandCenter.Shell/src/main.rs` test `backend_get_value_preserves_boundary_violation_error_envelope`

Invariant matrix:

| Transport invariant | Current protection | Remaining gap |
| --- | --- | --- |
| Transport preserves payload semantics | Successful JSON responses that flow through `serde_json::Value` are compared as opaque values in the Rust shell test. Boundary-violation error envelopes are preserved as backend-owned JSON errors. | Domain-shaped command mirrors still exist and are not yet inventoried or retired. |
| Transport preserves unknown fields | The Rust shell test includes unknown nested objects, arrays, nulls, empty strings, empty arrays, and enum-like strings. | Protection currently covers the generic GET value helper only. |
| Transport preserves null and empty values | The Rust shell test asserts explicit null, empty object, empty array, and empty string preservation. | Additional command families still need migration or classification. |
| Transport preserves backend errors | Boundary-violation error envelopes returned through the generic GET value helper are serialized back to JSON and compared for preservation. | Non-boundary error semantics and additional command families still need migration or classification before passive transport certification. |

## Contract Oracle

Introduced: Milestone 0.2.

Status: defined and inventoried at the contract-family level; locally certified for the repository dashboard, repository workspace, and primary workflow projection pilots.

Primary evidence:

- `docs/contracts.md`
- `docs/contract-endpoint-catalog.md`
- `.agents/milestones/m0.2-contract-inventory-slice-0006.md`
- `.agents/milestones/m0.2-contract-endpoint-catalog-slice-0007.md`
- `.agents/milestones/m0.2-repository-dashboard-field-catalog-slice-0008.md`
- `.agents/milestones/m0.2-repository-dashboard-fixture-slice-0009.md`
- `.agents/milestones/m0.2-oracle-drift-policy-slice-0010.md`
- `.agents/milestones/m0.2-consumer-verification-slice-0011.md`
- `.agents/milestones/m0.2-recursive-consumer-verification-slice-0012.md`
- `.agents/milestones/m0.2-typescript-consumer-verification-slice-0013.md`
- `.agents/milestones/m0.2-dev-mock-consumer-verification-slice-0014.md`
- `.agents/milestones/m0.2-consumer-verifier-extraction-slice-0015.md`
- `.agents/milestones/m0.2-artifact-freshness-slice-0016.md`
- `.agents/milestones/m0.2-oracle-change-workflow-slice-0017.md`
- `.agents/milestones/m0.2-repository-dashboard-oracle-certification-slice-0018.md`
- `.agents/milestones/m0.2-repository-dashboard-request-boundary-slice-0019.md`
- `.agents/milestones/m0.2-repository-workspace-fixture-slice-0020.md`
- `.agents/milestones/m0.2-repository-workspace-consumer-verification-slice-0021.md`
- `.agents/milestones/m0.2-repository-workspace-artifact-freshness-slice-0022.md`
- `.agents/milestones/m0.2-repository-workspace-request-boundary-slice-0023.md`
- `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md`
- `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0025.md`
- `.agents/milestones/m0.2-workflow-projection-field-inventory-slice-0026.md`
- `.agents/milestones/m0.2-workflow-fixture-field-classification-slice-0027.md`
- `.agents/milestones/m0.2-workflow-instance-fixture-slice-0028.md`
- `.agents/milestones/m0.2-workflow-typescript-consumer-verification-slice-0029.md`
- `.agents/milestones/m0.2-workflow-request-boundary-slice-0030.md`
- `.agents/milestones/m0.2-workflow-artifact-freshness-slice-0031.md`
- `.agents/milestones/m0.2-workflow-oracle-certification-slice-0032.md`
- `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0033.md`

Mechanism intent:

| Oracle mechanism | Current protection | Remaining gap |
| --- | --- | --- |
| Canonical contract authority | `docs/contracts.md` defines contract truth as backend-owned projection and command-result shape after backend JSON serialization. The repository dashboard pilot records field-level ownership and has a golden serialized fixture. The repository workspace pilot records field-level ownership and has a second golden serialized fixture. Workflow projection now has gated field inventory, fixture field classification, and a golden serialized fixture for `WorkflowInstance`. | Most endpoint families still need field-level inventory. |
| Endpoint and consumer visibility | `docs/contract-endpoint-catalog.md` catalogs 177 backend endpoint mappings by family, defines required endpoint-level inventory fields, identifies consumer classes, records priority fixture candidates, and catalogs repository dashboard consumers. | A full dependency graph still needs inventory. |
| Fixture gating | `docs/contracts.md` forbids golden fixtures before identity, owner, producer, consumers, parallel representations, compatibility, and serialization rules are known. The repository dashboard and repository workspace fixtures were added only after that gate was satisfied. Workflow projection passed inventory and field-classification gates before adding the `WorkflowInstance` fixture. | Broader fixture selection still needs endpoint-by-endpoint gating. |
| Parallel truth visibility | Initial matrix identifies backend, Rust, TypeScript, API wrapper, mock, test, and docs surfaces that can drift. Endpoint catalog records compatibility consumer classes, records the Rust dashboard and workspace mirrors missing `decisionSessionSummary`, verifies the manual TypeScript dashboard and workspace types against Oracle fixtures, verifies the dev mock dashboard entry as a downstream development/test consumer, verifies the typed dev mock workspace command payload, and now verifies the manual TypeScript `WorkflowInstance` shape against the workflow fixture. | Additional mock commands, workflow dev mock handler coverage, and full dependency graph coverage remain pending. |
| Oracle drift detection | `ContractOracleFixtureTests.RepositoryDashboardGoldenFixtureMatchesBackendSerialization` recursively compares backend JSON serialization against `repository-dashboard.golden.json`; `RepositoryWorkspaceGoldenFixtureMatchesBackendSerialization` does the same for `repository-workspace.golden.json`; `WorkflowInstanceGoldenFixtureMatchesBackendSerialization` does the same for `workflow-instance.golden.json`. All ignore object property ordering. | Certified drift detection covers repository dashboard, repository workspace, and primary workflow projection pilots only; workflow dev mock coverage, populated `decisionSession` coverage, and sibling workflow endpoint coverage remain pending. |
| Oracle drift policy | `ContractOracleFixtureTests` classifies missing fields, type/value drift, and array length changes as structural drift; additive backend fields are compatibility-review drift unless explicitly allowlisted by JSON path as reviewed compatibility additions. | Policy classification is still local to the pilot helper and has no fixture update tooling or consumer verification chain yet. |
| Consumer verification | `ContractConsumerVerificationTests` uses shared test-support infrastructure in `ContractVerification/ContractConsumerVerificationSupport.cs` to recursively compare repository dashboard and repository workspace Oracle fixture shapes against Rust, TypeScript, and dev mock downstream shapes. It reports the known Rust missing `decisionSessionSummary` fields as downstream consumer drift, verifies the manual TypeScript dashboard and workspace types as current, verifies the dev mock dashboard entry and typed workspace command payload as current, resolves imported TS aliases, resolves mock `workspace.*` references, honors explicit Rust serde field renames, reports consumer category, and protects recursive missing-field behavior with a synthetic verifier regression. `workflowContractFixture.test.ts` separately verifies the manual TypeScript `WorkflowInstance` shape against `workflow-instance.golden.json` for the first workflow fixture variant. | Shared backend consumer-verification infrastructure does not yet cover workflow; workflow dev mock coverage, additional mock commands, and semantic reinterpretation verification remain pending. |
| Contract artifact freshness | `ContractGeneratedArtifactFreshnessTests` uses `repository-dashboard.artifact-freshness.json`, `repository-workspace.artifact-freshness.json`, `workflow-instance.artifact-freshness.json`, and shared test-support infrastructure in `ContractVerification/ContractGeneratedArtifactFreshnessSupport.cs` to hash the repository dashboard/workspace/workflow Oracle fixtures and the current TypeScript contract artifacts. It fails distinctly for stale artifacts, unexpected manual artifact modification, and missing expected artifacts. | Coverage is repository dashboard, repository workspace, and the primary workflow instance only; artifacts are Phase 0 verified manual contract artifacts, not generated output. Deterministic generation, generated headers, artifact writing, command argument artifacts, and generated ecosystem certification remain pending. |
| Oracle change workflow | `docs/contracts.md` defines the procedural workflow for classifying fixture comparison, consumer verification, artifact freshness, and request-boundary drift before accepting a new baseline. It names the required change record, canonical sequence, acceptance rules, pilot commands, evidence requirements, and rollback requirement. | The workflow is procedural, not automated. It does not assign versions mechanically, generate artifacts, update freshness manifests, or certify contract families beyond the locally certified pilots. |
| Repository dashboard request boundary | `ContractRequestBoundaryTests` verifies `GET /api/repositories` has no route parameters or body metadata, Rust `list_repositories()` has no command arguments and forwards a backend GET without request-body construction, and TypeScript `listRepositories()` invokes `list_repositories` without command arguments. | Coverage is only the repository dashboard no-argument request boundary for this contract family. Non-empty command argument/body verification, route/query/body compatibility classification, and a general request-contract model remain pending. |
| Repository workspace request boundary | `ContractRequestBoundaryTests` verifies `GET /api/repositories/{repositoryId:guid}/workspace` has exactly one required GUID route parameter and no body metadata, Rust `get_repository_workspace(repository_id)` forwards a backend GET without request-body construction, and TypeScript `getRepositoryWorkspace(repositoryId)` invokes `get_repository_workspace` with only `{ repositoryId }`. | Coverage is only the primary workspace GET path. Refresh, artifact rotation, query/body request shapes, route/query/body compatibility classification, and a general request-contract model remain pending. |
| Workflow projection request boundary | `ContractRequestBoundaryTests` verifies `GET /api/repositories/{repositoryId:guid}/workflow` has exactly one required GUID route parameter and no body metadata, Rust `get_workflow_projection(repository_id)` forwards through the passive `backend_get_value` helper without request-body construction, and TypeScript `getWorkflowProjection(repositoryId)` invokes `get_workflow_projection` with only `{ repositoryId }`. | Coverage is only the primary workflow projection GET path. Sibling workflow endpoints, query/body request shapes, route/query/body compatibility classification, and a general request-contract model remain pending. |
| Repository dashboard pilot certification | `.agents/milestones/m0.2-repository-dashboard-oracle-certification-slice-0018.md` records local certification evidence for the repository dashboard fixture comparison, consumer verification, artifact freshness verification, and full backend test suite. | Certification is limited to the repository dashboard pilot as of Slice 0018. It does not certify Milestone 0.2 globally, request-boundary Slice 0019, semantic reinterpretation checks, additional contract families, or generated artifact determinism. |
| Repository workspace pilot certification | `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md` records local certification evidence for the repository workspace fixture comparison, consumer verification, artifact freshness verification, primary GET request-boundary verification, combined Oracle mechanism filter, and full backend test suite. | Certification is limited to the repository workspace pilot as of Slice 0024. It does not certify Milestone 0.2 globally, refresh or artifact rotation request boundaries, semantic reinterpretation checks, additional contract families, or generated artifact determinism. |
| Cross-family repeatability evidence | `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0025.md` records repository dashboard/workspace repeatability, and `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0033.md` records that the same Oracle lifecycle repeated across repository dashboard, repository workspace, and primary workflow projection without framework redesign: field inventory or fixture gate, golden fixture, drift classification, consumer verification, artifact freshness, request-boundary verification, and local certification. | Repeatability is proven across three pilots, not globally. Decision lifecycle eligibility remains the preferred fourth family only if certification review finds backend-owned eligibility semantics underrepresented; reasoning, execution, streams, errors, and non-empty command bodies still need later coverage expansion. |
| Workflow projection field inventory | `.agents/milestones/m0.2-workflow-projection-field-inventory-slice-0026.md` identifies `WorkflowInstance` as the next primary Oracle contract, maps producer, request boundary, shell pass-through, TypeScript type/API consumers, UI consumers, absent dev mock handler, top-level semantic field groups, fixture candidate requirements, and sibling endpoint limits. | Populated `decisionSession` variant, dev mock workflow coverage, and sibling endpoint fixtures remain pending; local certification is recorded separately for the initial primary projection pilot. |
| Workflow fixture field classification | `.agents/milestones/m0.2-workflow-fixture-field-classification-slice-0027.md` classifies each top-level `WorkflowInstance` field as semantic authority, structural metadata, compatibility field, diagnostic field, or derived presentation helper; defines nested path classification rules; and blocks fixture capture until explicit nulls, diagnostics arrays, ordered arrays, backend eligibility booleans, and flattened compatibility fields are represented. | The gate was satisfied for the first `WorkflowInstance` fixture only; populated `decisionSession` coverage and sibling workflow endpoints remain pending. |
| Workflow instance fixture comparison | `.agents/milestones/m0.2-workflow-instance-fixture-slice-0028.md` adds `workflow-instance.golden.json` and `ContractOracleFixtureTests.WorkflowInstanceGoldenFixtureMatchesBackendSerialization` for the primary workflow endpoint response shape. | Dev mock workflow handler verification and populated `decisionSession` fixture coverage remain pending; local workflow Oracle certification is recorded in Slice 0032. |
| Workflow TypeScript consumer verification | `.agents/milestones/m0.2-workflow-typescript-consumer-verification-slice-0029.md` adds `workflowContractFixture.test.ts`, which reads `workflow-instance.golden.json` and checks the manual TypeScript `WorkflowInstance` shape plus represented nested workflow shapes. | This is a UI characterization verifier, not generated contract infrastructure. Rust workflow commands are pass-through `serde_json::Value`; dev mock workflow handler coverage remains absent. |
| Workflow request-boundary verification | `.agents/milestones/m0.2-workflow-request-boundary-slice-0030.md` extends `ContractRequestBoundaryTests` for the primary workflow projection backend route, Rust command, and TypeScript API wrapper. | This protects request shape only; it does not verify dev mock response coverage, populated `decisionSession` fixtures, sibling workflow endpoints, or local workflow Oracle certification. |
| Workflow artifact freshness | `.agents/milestones/m0.2-workflow-artifact-freshness-slice-0031.md` adds `workflow-instance.artifact-freshness.json` and `ContractGeneratedArtifactFreshnessTests.WorkflowInstanceTypeScriptContractArtifactMatchesFreshnessManifest`, tying the workflow golden fixture to `src/CommandCenter.UI/src/types/workflow.ts`. | This is still a Phase 0 verified manual artifact freshness check, not generated contract infrastructure. Dev mock workflow coverage, populated `decisionSession` coverage, and sibling workflow endpoint fixtures remain pending. |
| Workflow pilot certification | `.agents/milestones/m0.2-workflow-oracle-certification-slice-0032.md` records local certification for the primary workflow projection fixture comparison, TypeScript consumer verification, request-boundary verification, artifact freshness verification, and full backend test suite. | Certification is limited to the primary workflow projection. Dev mock workflow coverage and populated `decisionSession` coverage are accepted initial-pilot gaps; sibling workflow endpoint fixtures remain pending. |

## Mechanism Lifecycle Rule

A verifier can be treated as architectural protection only when its command or source, protected surface, known gaps, owner, and retirement criteria for any quarantine are recorded.

Weakening, removing, or bypassing an accepted verifier requires a decision record and replacement or quarantine evidence.
