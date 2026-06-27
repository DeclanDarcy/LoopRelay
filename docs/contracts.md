# Contracts

This document defines the Contract Oracle direction for Command Center. It is intentionally seeded during Milestone 0.2 before golden fixtures are added, because fixtures must observe an identified authoritative contract rather than whichever parallel representation currently happens to exist.

## Oracle Definition

The canonical contract authority is the serialized external shape of backend-owned projections and command results under the backend JSON configuration.

The Oracle is an observation and drift-detection mechanism. It does not own domain meaning, projection construction, transport behavior, resource state, or presentation mapping.

Contract truth flows as:

```text
backend authority
  -> projection or command result
  -> backend JSON serialization
  -> Oracle fixture or comparison
  -> generated or verified consumers
```

## Boundary Taxonomy

| Boundary | Contract responsibility |
| --- | --- |
| Domain | Owns semantic rules, lifecycle legality, eligibility, diagnostics, recovery meaning, and source facts. |
| Projection | Exposes backend-owned read models and command results without creating new meaning. |
| Contract | Defines the externally observable serialized shape of projections, command requests, command results, and error envelopes. |
| Serialization | Applies backend JSON naming, enum, null, collection, date, identifier, ordering, and compatibility rules. |
| Transport | Preserves request, response, status, null/empty values, unknown fields, and error payloads without interpretation. |
| Resource | Owns frontend loading, refresh, invalidation, stale-response, and mutation mechanics for a contract consumer. |
| Controller | Builds feature view models and action sequencing from resource state and authoritative backend facts. |
| Presentation | Maps typed facts to labels, color, layout, icons, accessibility text, and local interaction affordances. |
| Persistence | Stores repository data and internal records; persisted shapes are not external contracts unless exposed across a boundary. |
| Runtime | Scopes failures, partial data, absence, retries, and recovery without changing contract meaning. |

## Initial Contract Relationship Matrix

This matrix records contract families discovered in the first Milestone 0.2 inventory slice. Later slices must expand each family into endpoint-level and field-level entries before fixtures are certified.

| Contract identity | Owning projection or command | Serialization authority | Producer | Consumers | Parallel representations | Compatibility obligations | Planned Oracle fixture | Migration priority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Repository dashboard | `RepositoryDashboardProjection` | Backend JSON serialization | `RepositoriesEndpoints` | Tauri `list_repositories`, TS `RepositoryDashboardProjection`, dev mock, repository hooks/UI | C# projection, Rust `RepositoryDashboardProjection`, TS type, mock data | Preserve repository identity, path/name, availability, and summary fields while manual consumers exist. | Yes, representative read model. | High |
| Repository workspace | `RepositoryWorkspaceProjection` | Backend JSON serialization | `RepositoriesEndpoints`, artifact rotation endpoints | Tauri workspace commands, TS `RepositoryWorkspaceProjection`, artifact APIs, dev mock, workspace UI | C# projection, Rust `RepositoryWorkspaceProjection`, TS type, mock data | Preserve nested artifact, execution, continuity, reasoning, decision-session, and workspace summary shape. | Yes, representative aggregate workspace. | High |
| Artifact inventory/content | Artifact inventory projection and content commands | Backend JSON serialization for inventory; string payload for content | `ArtifactsEndpoints` | Tauri artifact commands, TS artifact types, editor/workspace UI | C# endpoint result, Rust command result, TS types, mock data | Preserve content as opaque text and inventory classification until generated consumers exist. | Inventory yes; content no unless content envelope is introduced. | Medium |
| Execution context and session | Execution projections and command results | Backend JSON serialization | `ExecutionEndpoints`, `ExecutionSessionsEndpoints` | Tauri execution commands, TS execution types, execution hooks/UI, dev mock | C# projections, Rust `ExecutionSessionSummary` and related mirrors, TS types, mock data | Preserve session identity, state, prompt manifest, transparency, status, events, and handoff result compatibility. | Yes, summary/status and prompt manifest. | High |
| Execution event stream | Execution event stream contract | Backend event serialization | `ExecutionSessionsEndpoints` stream route | Browser/event consumers and future transport passivity tests | C# event records, stream formatting, TS event type | Preserve event order, event payload shape, and failure boundary semantics. | Later, stream-specific fixture or trace. | Medium |
| Git status and execution Git actions | Git status projection and execution Git command results | Backend JSON serialization | `GitEndpoints` | Tauri Git/commit/push commands, TS git/execution types, Git UI, dev mock | C# projections, Rust request/result mirrors, TS types, mock data | Preserve dirty-state, change scope, eligibility, commit preparation, commit, and push result semantics. | Yes, status and action eligibility. | High |
| Operational-context proposals | Operational context proposal projection and review commands | Backend JSON serialization | `OperationalContextEndpoints` | Tauri proposal commands, TS operational context types, continuity UI, dev mock | C# projection, Rust command requests, TS types, mock data | Preserve proposal status, markdown content, semantic change summaries, compression, assimilation, and review states. | Yes, proposal read model. | High |
| Continuity diagnostics/reports | Continuity projections and report command results | Backend JSON serialization | `ContinuityEndpoints` | Tauri continuity commands, TS continuity types, continuity UI, dev mock | C# projections, TS types, mock data | Preserve diagnostic group semantics, trends, report history, and null/empty report behavior. | Yes, diagnostics. | Medium |
| Decision lifecycle eligibility | Decision lifecycle eligibility projection | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, decision UI, dev mock | C# projections, TS union/string literals, mock data | Preserve backend-owned eligibility and blocked-state semantics; UI must not infer legality. | Yes, required representative contract. | High |
| Decision proposal browser/review | Decision proposal, browser, review, option, evidence, lineage, revision contracts | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, decision UI, dev mock | C# projections, TS types, mock data | Preserve proposal lifecycle, review authority, recommendation evidence, option comparison, source attribution, and lineage fields. | Yes, proposal browser. | High |
| Decision governance/quality/certification | Governance, quality, certification projections and reports | Backend JSON serialization | `DecisionEndpoints` | Tauri decision commands, TS decision types, governance/quality UI, dev mock | C# projections, TS types, mock data | Preserve severity, findings, ratings, evidence, reports, and certification result meaning. | Governance snapshot/report yes. | Medium |
| Decision-session governance | Decision-session lifecycle, analysis, recovery, workflow, certification projections | Backend JSON serialization | `DecisionSessionEndpoints` | Tauri decision-session commands, TS decision-session/workflow types, governance UI, dev mock | C# projections, TS types, mock data | Preserve active/null session semantics, transfer eligibility, recovery diagnostics, workflow influence, and certification reports. | Yes, governance snapshot. | High |
| Reasoning graph/report | Reasoning events, threads, relationships, graph, trace, query, reconstruction, materialization, certification projections | Backend JSON serialization | `ReasoningEndpoints` | Tauri reasoning commands, TS reasoning types, reasoning UI, dev mock | C# projections/records, TS types, mock data | Preserve schema-versioned reasoning records, boundary violations, diagnostics, graph identity, trace direction, and report history. | Yes, graph/report. | High |
| Workflow projection | Workflow instance and related diagnostics, gates, history, recovery, continuation, preparation, health, reports, certification | Backend JSON serialization | `WorkflowEndpoints` | Tauri workflow commands, TS workflow types, workflow UI, dev mock | C# projections, TS types, mock data | Preserve workflow-owned stage, gates, legality, diagnostics, recovery, health, and certification semantics. | Yes, required representative contract. | High |
| Planning projection | Planning milestones projection | Backend JSON serialization | `PlanningEndpoints` | TS planning types and planning UI when consumed | C# projection, TS type | Preserve milestone list shape and ordering. | Later. | Low |
| Error envelope and boundary violation | Backend error response envelope, including `boundaryViolation` when present | Backend JSON serialization | Endpoint exception/error handling | Tauri error channel, TS `TransportError`, UI boundary notices | C# anonymous/envelope shapes, Rust `ErrorResponse`, TS `BoundaryViolationProjection`, API parser | Preserve status, error text, structured boundary violation, null, and unknown fields through transport. | Yes, required representative contract. | High |
| Tauri command request envelope | Shell command arguments and backend request bodies | Shell should be passive transport except shell-owned commands | `src/CommandCenter.Shell/src/main.rs` | TS API wrappers and Tauri runtime | Rust request structs, TS API arg objects, backend request DTOs | Temporary mirrors must be inventoried and either generated, verified, or retired in Milestone 1.3. | No, unless classified as shell-owned. | High |
| Dev Tauri mock contracts | Development mock command responses | Must be generated or Oracle-verified against backend contracts | `src/CommandCenter.UI/src/devTauriMock.ts` | Frontend development and characterization tests | Mock object literals parallel backend, Rust, and TS shapes | Must not become independent contract authority. | No direct fixture; compare through generated or verified mock data later. | High |

## Initial Parallel Truths

The first inventory slice found these active parallel contract truth sources:

- Backend endpoint response shapes under `src/CommandCenter.Backend/Endpoints`.
- Backend projection and command result records across domain projects.
- Rust Tauri command return/request mirrors in `src/CommandCenter.Shell/src/main.rs`.
- Manual TypeScript types in `src/CommandCenter.UI/src/types`.
- TypeScript API command names and generic return types in `src/CommandCenter.UI/src/api`.
- Development mock payloads in `src/CommandCenter.UI/src/devTauriMock.ts`.
- Characterization and backend tests that encode expected shape indirectly.
- Durable docs for operational-context and reasoning repository persistence contracts.

## Endpoint Catalog and Consumer Taxonomy

The endpoint-level inventory is maintained in `docs/contract-endpoint-catalog.md`.

Current catalog scope:

- 177 backend endpoint mappings under `src/CommandCenter.Backend/Endpoints`.
- Consumer taxonomy covering backend tests, Rust shell commands, TypeScript API wrappers, manual TypeScript types, dev Tauri mocks, React consumers, characterization/E2E tests, and durable docs.
- Narrow serialization rules for identifiers, enum-like strings, null versus omitted fields, empty collections, date/time capture, ordering, unknown fields, error envelopes, streams, and compatibility fields.
- Backend JSON serialization observations from `Program.CreateApp`: web defaults plus string enum conversion.
- Repository dashboard field ownership pilot for `GET /api/repositories`, including top-level fields, nested summary fields, nullability, derived status, and known compatibility drift.
- Repository dashboard golden fixture at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.golden.json`, protected by `ContractOracleFixtureTests.RepositoryDashboardGoldenFixtureMatchesBackendSerialization`.
- Repository dashboard drift policy classification in `ContractOracleFixtureTests`, with structural drift failing immediately and additive field drift requiring explicit compatibility review.
- Repository dashboard Rust, TypeScript, and dev mock consumer verification in `ContractConsumerVerificationTests`, backed by shared test-support infrastructure in `ContractVerification/ContractConsumerVerificationSupport.cs`, which recursively compares the backend golden fixture shape against downstream consumer shapes, reports the known Rust `decisionSessionSummary` omission as downstream consumer drift, verifies the manual TypeScript dashboard type as current, and verifies the `devTauriMock` dashboard entry shape for the pilot fixture.
- Repository dashboard contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `repository-dashboard.artifact-freshness.json`, which hashes the Oracle fixture and the current TypeScript repository contract artifact to distinguish stale artifacts, unexpected artifact edits, and missing expected artifacts.
- Repository dashboard request-boundary verification in `ContractRequestBoundaryTests`, which pins `GET /api/repositories` as a no-argument backend route, `list_repositories` as a no-argument Rust Tauri command that forwards a GET without request-body construction, and `listRepositories()` as a TypeScript API wrapper that invokes `list_repositories` without command arguments.
- Repository workspace field ownership and golden fixture pilot for `GET /api/repositories/{repositoryId}/workspace`, including top-level workspace fields, artifact inventory, full operational-context projection shape, nested execution, reasoning, and decision-session summaries, and known Rust workspace mirror drift.
- Repository workspace golden fixture at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.golden.json`, protected by `ContractOracleFixtureTests.RepositoryWorkspaceGoldenFixtureMatchesBackendSerialization`.
- Repository workspace Rust, TypeScript, and dev mock consumer verification in `ContractConsumerVerificationTests`, using the shared verifier support to report the known Rust `decisionSessionSummary` omission, verify the manual TypeScript workspace type as current, and verify the `devTauriMock` workspace command payload shape through the typed mock workspace store.
- Repository workspace contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `repository-workspace.artifact-freshness.json`, which hashes the workspace Oracle fixture and the current TypeScript repository contract artifact.
- Repository workspace request-boundary verification in `ContractRequestBoundaryTests`, which pins `GET /api/repositories/{repositoryId}/workspace` as a single route-argument, bodyless backend route, `get_repository_workspace(repository_id)` as a Rust Tauri command that forwards a GET without request-body construction, and `getRepositoryWorkspace(repositoryId)` as a TypeScript API wrapper that passes only the repository id command argument.
- Workflow contract artifact freshness verification in `ContractGeneratedArtifactFreshnessTests`, backed by `workflow-instance.artifact-freshness.json`, which hashes the workflow Oracle fixture and the current TypeScript workflow contract artifact.
- Priority endpoint rows for the first fixture candidates.

The catalog is not a generated schema. It is an inventory and fixture-selection mechanism used to prevent fixtures from certifying accidental or consumer-owned shape.

The repository dashboard pilot currently exposes one executable compatibility finding: the Rust `RepositoryDashboardProjection` mirror omits `decisionSessionSummary`, while the backend, TypeScript dashboard contract, and dev mock dashboard entry include it. `ContractConsumerVerificationTests` records this as downstream consumer drift and separately verifies the manual TypeScript `RepositoryDashboardProjection` shape and `devTauriMock` dashboard entry shape against the same Oracle fixture. This is evidence for the Oracle and a later shell/manual-mirror migration; the Oracle fixture does not treat any downstream mirror as contract authority.

## Fixture Gating Rule

Golden fixtures may be introduced only after the target contract has:

- a contract identity,
- an owning backend projection or command result,
- a producer endpoint,
- a known consumer set,
- known parallel representations,
- compatibility obligations,
- serialization rules relevant to the contract,
- and an update workflow for fixture review and consumer regeneration.

The first fixture candidates are repository dashboard, repository workspace, workflow projection, decision lifecycle eligibility, decision proposal browser, decision-session governance snapshot, reasoning graph/report, continuity diagnostics, execution summary/status, and error envelope.

The repository dashboard candidate now has the first golden fixture and recursive backend serialization comparison. The fixture intentionally covers explicit nulls, populated arrays, non-empty execution summary and history, decision-session summary, timestamps, durations, enum strings, and nested summary objects. Empty-array coverage remains represented by nested zero-count reasoning fields and will need a second dashboard variant or another fixture if empty collection serialization must be pinned for this contract specifically.

The repository workspace candidate now has the second golden fixture, recursive backend serialization comparison, consumer verification against Rust, TypeScript, and dev mock downstream shapes, artifact freshness verification for the shared TypeScript repository contract artifact, and request-boundary verification for the primary workspace GET path. The fixture intentionally covers artifact inventory nulls and populated arrays, full operational-context item arrays, proposal summary enum/null/date fields, execution summary accepted/commit/push fields, empty decision-session arrays, and the backend-owned `decisionSessionSummary` field that is missing from the Rust workspace mirror. This proves the Oracle pattern can repeat across a second contract family, and local repository workspace Oracle certification is recorded in `.agents/milestones/m0.2-repository-workspace-oracle-certification-slice-0024.md`.

Initial cross-pilot repeatability evidence is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0025.md`. The first repeatability claim was limited to the repository dashboard and repository workspace pilots: both used the same field-inventory, golden-fixture, drift-classification, consumer-verification, artifact-freshness, request-boundary, and local-certification lifecycle without an Oracle framework redesign.

Workflow projection coverage started with gated field inventory in `.agents/milestones/m0.2-workflow-projection-field-inventory-slice-0026.md`. The workflow inventory identifies `WorkflowInstance` as the primary contract for `GET /api/repositories/{repositoryId}/workflow`, maps the backend producer, shell and TypeScript request boundary, manual TypeScript response mirror, UI consumers, absent dev mock command handler, and semantic lifecycle field groups that must be represented before a workflow golden fixture can be approved. Field-role classification for the primary workflow fixture candidate is recorded in `.agents/milestones/m0.2-workflow-fixture-field-classification-slice-0027.md`; it classifies each top-level `WorkflowInstance` field, establishes nested field classification rules, and keeps flattened status/eligibility fields under compatibility review before fixture capture. The first workflow golden fixture is `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json`, protected by `ContractOracleFixtureTests.WorkflowInstanceGoldenFixtureMatchesBackendSerialization`; it covers explicit nulls, empty and non-empty diagnostics arrays, backend-owned eligibility booleans, ordered timeline/transition/gate arrays, flattened compatibility fields, and `decisionSession: null`. Workflow TypeScript consumer verification is recorded in `.agents/milestones/m0.2-workflow-typescript-consumer-verification-slice-0029.md`; `workflowContractFixture.test.ts` reads the backend golden fixture and verifies the manual TypeScript `WorkflowInstance` shape for the represented fixture variant. Workflow request-boundary verification is recorded in `.agents/milestones/m0.2-workflow-request-boundary-slice-0030.md`; `ContractRequestBoundaryTests` now verifies the backend route, passive Rust command, and TypeScript command argument shape for the primary workflow projection. Workflow artifact freshness is recorded in `.agents/milestones/m0.2-workflow-artifact-freshness-slice-0031.md`; `workflow-instance.artifact-freshness.json` now ties the workflow golden fixture to the manual TypeScript workflow contract artifact. Local workflow Oracle certification is recorded in `.agents/milestones/m0.2-workflow-oracle-certification-slice-0032.md`. Populated `decisionSession` coverage and dev mock workflow handler coverage are accepted gaps for the initial workflow pilot; sibling workflow endpoint fixtures remain pending.

Cross-family repeatability evidence across repository dashboard, repository workspace, and primary workflow projection is recorded in `.agents/milestones/m0.2-oracle-repeatability-evidence-slice-0033.md`. The repeatability claim covers two repository read models and one richer semantic workflow family using the same Oracle lifecycle without framework redesign. Milestone-level certification review is recorded in `.agents/milestones/m0.2-oracle-certification-review-slice-0034.md`; it certifies the Phase 0 Contract Oracle foundation with explicit accepted limitations and does not claim full contract-surface coverage, generated contract lifecycle, or passive transport certification. Decision lifecycle eligibility remains the preferred fourth family only if a future review identifies a concrete uncovered backend-owned eligibility property.

## Initial Oracle Fixture Workflow

The initial executable workflow for the repository dashboard and repository workspace fixture pilots is:

1. Build representative backend projection data with stable identifiers and timestamps.
2. Serialize with `JsonSerializerDefaults.Web` plus `JsonStringEnumConverter`.
3. Compare serialized JSON recursively against the golden fixture while treating object property ordering as non-semantic.
4. Fail structural drift immediately for missing fields, type changes, null/object changes, array/scalar changes, changed values, or array length changes.
5. Classify additive backend fields as compatibility-review drift unless their exact JSON path is explicitly recorded as a reviewed compatibility addition for that fixture.
6. Review fixture updates through Milestone 0.2 evidence before downstream Rust, TypeScript, mock, or generated consumers are changed.

This workflow is a pilot mechanism, not the full generated Oracle lifecycle. It still needs fixture update tooling, consumer regeneration or verification, and broader endpoint coverage before full contract-surface certification. Milestone 0.2 certification is scoped to the Phase 0 Oracle foundation and representative fixture lifecycle.

## Drift Policy Classification

The repository dashboard Oracle pilot currently recognizes these drift categories:

| Category | Examples | Oracle behavior |
| --- | --- | --- |
| Structural drift | Missing fixture field, type change, null/object mismatch, array/scalar mismatch, changed value, array length change, serializer behavior change. | Hard failure. The backend serialized shape no longer matches the accepted contract fixture. |
| Compatibility-review drift | Additive backend field not present in the fixture. | Failure until the field is reviewed, documented, and either added to the fixture or explicitly recorded as a reviewed compatibility addition. |
| Consumer drift | Rust, TypeScript, mock, or characterization representation differs from backend Oracle truth. | Must be surfaced by consumer verification. It must not weaken the backend Oracle fixture. |

Reviewed compatibility additions are path-specific. A reviewed additive field does not make the consumer current; it only records that the backend fixture comparison may permit that additive field while the compatibility path is handled.

## Consumer Verification Pilot

Consumer verification is separate from Oracle fixture comparison. The Oracle compares backend serialization to accepted backend-owned fixture truth. Consumer verification compares downstream representations against that Oracle-observed truth and reports where a consumer is stale, invented, or structurally incompatible.

The consumer verification pilot now uses shared test-support infrastructure with a reusable verifier specification, recursive comparison engine, consumer shape model, and source-specific shape providers. The Rust shape provider parses `src/CommandCenter.Shell/src/main.rs`, follows nested struct references, unwraps `Option<T>` nullability, honors explicit `#[serde(rename = "...")]` field names before camel-casing, compares `Vec<T>` array item shape when the fixture contains an item, and treats `serde_json::Value` as opaque transport shape. The TypeScript shape provider parses exported type aliases under `src/CommandCenter.UI/src/types`, resolves imported/manual aliases through the shared type folder, treats string-literal unions as string-valued contracts, unwraps nullable unions, and compares array item shape when the fixture contains an item. The dev mock shape provider parses `src/CommandCenter.UI/src/devTauriMock.ts`, extracts the `dashboardEntry(workspace)` returned object shape, resolves `workspace.*` references through the TypeScript workspace type, recognizes inline object literals, treats `.length` projections as numeric fields, and verifies that the repository workspace mock command returns the typed `state.workspaces[repositoryId]` payload.

Consumer categories currently reported by the verifier are:

| Consumer | Category |
| --- | --- |
| Rust `RepositoryDashboardProjection` mirror | Runtime consumer |
| Rust `RepositoryWorkspaceProjection` mirror | Runtime consumer |
| TypeScript `RepositoryDashboardProjection` type | Compile-time consumer |
| TypeScript `RepositoryWorkspaceProjection` type | Compile-time consumer |
| `devTauriMock` `dashboardEntry` object | Development/test consumer |
| `devTauriMock` `get_repository_workspace` payload | Development/test consumer |

It classifies:

| Consumer drift kind | Meaning |
| --- | --- |
| Missing downstream field | Backend serialized field exists in the Oracle fixture, but the consumer mirror omits it. |
| Extra downstream field | Consumer mirror declares a field not present in the backend Oracle fixture. |
| Value-kind changed | Backend serialized value kind is not accepted by the downstream mirror shape. |

Current finding:

- `src/CommandCenter.Shell/src/main.rs` omits `$[].decisionSessionSummary` from `RepositoryDashboardProjection`.
- `src/CommandCenter.Shell/src/main.rs` omits `$.decisionSessionSummary` from `RepositoryWorkspaceProjection`.
- `src/CommandCenter.UI/src/types/repositories.ts` currently matches the repository dashboard Oracle fixture shape, including imported execution summaries and nested decision-session summary arrays.
- `src/CommandCenter.UI/src/types/repositories.ts` currently matches the repository workspace Oracle fixture shape, including artifact inventory, operational-context, reasoning, execution, and decision-session summary shapes.
- `src/CommandCenter.UI/src/devTauriMock.ts` currently matches the repository dashboard Oracle fixture shape for the `dashboardEntry(workspace)` mock projection, including inline continuity summary fields and workspace-derived reasoning and decision-session summaries.
- `src/CommandCenter.UI/src/devTauriMock.ts` currently returns typed workspace mock payloads for `get_repository_workspace` that match the repository workspace Oracle fixture shape.

Current protection:

- `RepositoryDashboardRustMirrorReportsKnownDecisionSessionSummaryOmission` keeps the known root-level Rust omission executable.
- `RepositoryDashboardRustMirrorRecursivelyVerifiesMirroredNestedShape` proves the Rust mirror's existing nested repository, execution summary/history, continuity summary, and reasoning summary shapes still conform to the backend fixture.
- `RepositoryDashboardTypeScriptTypeMatchesGoldenFixture` proves the manual TypeScript dashboard type has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryDashboardTypeScriptTypeRecursivelyVerifiesImportedNestedShape` proves imported execution summary aliases and nested decision-session summary arrays are resolved by the shared verifier pipeline.
- `RepositoryDashboardDevTauriMockMatchesGoldenFixture` proves the dev mock dashboard entry has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryDashboardDevTauriMockRecursivelyVerifiesInlineContinuityShape` proves inline mock object literals and workspace-derived nested summaries participate in the shared verifier pipeline.
- `RepositoryWorkspaceRustMirrorReportsKnownDecisionSessionSummaryOmission` keeps the known root-level Rust workspace omission executable.
- `RepositoryWorkspaceRustMirrorRecursivelyVerifiesMirroredNestedShape` proves the Rust mirror's existing nested repository, execution, artifact inventory, operational-context, and reasoning shapes still conform to the backend fixture.
- `RepositoryWorkspaceTypeScriptTypeMatchesGoldenFixture` proves the manual TypeScript workspace type has no missing, extra, or value-kind drift against the pilot fixture.
- `RepositoryWorkspaceDevTauriMockPayloadMatchesGoldenFixture` proves the dev mock workspace command payload has no missing, extra, or value-kind drift against the pilot fixture.
- `ConsumerVerifierReportsNestedMissingFields` protects recursive missing-field behavior independent of the Rust parser.

This pilot does not yet compare non-empty command argument bodies, additional mock command payloads, or semantic reinterpretation. Those remain later Milestone 0.2 and Milestone 1.2/1.3 work.

## Request Boundary Verification Pilot

Request boundary verification is separate from response fixture comparison and downstream response-shape consumer verification. It asks whether the backend endpoint, shell command, and TypeScript API wrapper still agree on the externally observable request shape for a contract boundary.

The repository dashboard request boundary is intentionally no-argument:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories`, no route parameters, no body metadata. |
| Rust Tauri command | `list_repositories()`, no command parameters, backend `GET /api/repositories`, no client request-body construction. |
| TypeScript API wrapper | `listRepositories()`, invokes `list_repositories` without an argument object. |

The repository workspace request boundary is the first non-empty request-boundary pilot:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories/{repositoryId:guid}/workspace`, one required `repositoryId` GUID route parameter, no body metadata. |
| Rust Tauri command | `get_repository_workspace(repository_id: String)`, backend `GET /api/repositories/{repository_id}/workspace`, no client request-body construction. |
| TypeScript API wrapper | `getRepositoryWorkspace(repositoryId: string)`, invokes `get_repository_workspace` with `{ repositoryId }` and no additional command fields. |

The primary workflow projection request boundary now repeats that single-route-argument pattern while preserving passive Rust transport:

| Boundary participant | Expected request shape |
| --- | --- |
| Backend endpoint | `GET /api/repositories/{repositoryId:guid}/workflow`, one required `repositoryId` GUID route parameter, no body metadata. |
| Rust Tauri command | `get_workflow_projection(repository_id: String)`, backend `GET /api/repositories/{repository_id}/workflow` through `backend_get_value`, no client request-body construction. |
| TypeScript API wrapper | `getWorkflowProjection(repositoryId: string)`, invokes `get_workflow_projection` with `{ repositoryId }` and no additional command fields. |

Current protection:

- `RepositoryDashboardBackendEndpointHasNoRequestArguments` verifies the backend route method, pattern, route parameters, and body metadata.
- `RepositoryDashboardRustCommandHasNoCommandArgumentsAndForwardsGetWithoutBody` verifies the Rust command signature and GET forwarding path.
- `RepositoryDashboardTypeScriptApiInvokesCommandWithoutArguments` verifies the TypeScript command invocation has no argument object.
- `RepositoryWorkspaceBackendEndpointHasRepositoryIdRouteArgumentAndNoBody` verifies the backend route method, pattern, required GUID route parameter, and absence of body metadata.
- `RepositoryWorkspaceRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody` verifies the Rust command accepts a repository id, forwards the backend GET path, and constructs no request body.
- `RepositoryWorkspaceTypeScriptApiInvokesCommandWithRepositoryIdArgument` verifies the TypeScript wrapper invokes `get_repository_workspace` with the expected repository id command argument.
- `WorkflowProjectionBackendEndpointHasRepositoryIdRouteArgumentAndNoBody` verifies the backend workflow route method, required GUID route parameter, and absence of body metadata.
- `WorkflowProjectionRustCommandHasRepositoryIdArgumentAndForwardsGetWithoutBody` verifies the Rust workflow command accepts a repository id, forwards through `backend_get_value`, and constructs no request body.
- `WorkflowProjectionTypeScriptApiInvokesCommandWithRepositoryIdArgument` verifies the TypeScript workflow wrapper invokes `get_workflow_projection` with only the expected repository id command argument.

This is still a narrow pilot check. It does not introduce a general request-contract model, does not verify non-empty command DTOs, and does not classify request compatibility for route, query, or body evolution.

## Contract Artifact Freshness Pilot

Artifact freshness verification is separate from both Oracle fixture comparison and consumer verification.

The Oracle fixture comparison asks whether backend serialization still matches accepted backend-owned fixture truth. Consumer verification asks whether downstream shapes conform to that Oracle-observed truth. Artifact freshness asks whether a tracked contract artifact baseline has moved in lockstep with the Oracle source that justifies it.

The repository dashboard pilot stores its freshness manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.artifact-freshness.json`, the repository workspace pilot stores its manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.artifact-freshness.json`, and the workflow pilot stores its manifest at `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.artifact-freshness.json`. Each manifest records:

- contract identity,
- Oracle source path and SHA-256,
- expected contract artifact path,
- artifact kind,
- expected artifact SHA-256.

Current artifact coverage:

| Contract | Oracle source | Artifact | Artifact kind |
| --- | --- | --- | --- |
| Repository dashboard | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-dashboard.golden.json` | `src/CommandCenter.UI/src/types/repositories.ts` | Phase 0 verified contract artifact |
| Repository workspace | `tests/CommandCenter.Backend.Tests/ContractFixtures/repository-workspace.golden.json` | `src/CommandCenter.UI/src/types/repositories.ts` | Phase 0 verified contract artifact |
| Workflow instance | `tests/CommandCenter.Backend.Tests/ContractFixtures/workflow-instance.golden.json` | `src/CommandCenter.UI/src/types/workflow.ts` | Phase 0 verified contract artifact |

This artifact is still a manual TypeScript contract file, not a generated Milestone 1.2 output. The freshness verifier intentionally treats it as a Phase 0 verified contract artifact so stale/missing/manual-edit failure semantics exist before the generated contract ecosystem is introduced.

Freshness failure modes:

| Failure mode | Meaning | Remediation |
| --- | --- | --- |
| Stale generated artifact | Oracle source hash changed while the artifact still matches the previous baseline, or both source and artifact changed without updated regeneration evidence. | Review the Oracle change, regenerate or update the artifact through the approved workflow, then update the freshness manifest with evidence. |
| Unexpected manual artifact modification | Artifact hash changed while the Oracle source baseline did not change. | Revert or justify the artifact change through decision/evidence governance before accepting the new baseline. |
| Missing expected artifact | The manifest names an artifact that no longer exists. | Restore the artifact, update the manifest after an approved relocation, or retire the artifact through the compatibility path. |

This pilot does not generate TypeScript, prove artifact determinism, detect manual edits inside generated headers, compare command argument bodies, or certify the generated ecosystem. Those remain Milestone 1.2 responsibilities.

## Oracle Change Workflow

The Oracle change workflow governs how a detected contract drift becomes an accepted contract baseline. It is procedural during Milestone 0.2 so the review path is explicit before generation, regeneration, and lifecycle automation are introduced in later milestones.

The workflow applies to Oracle-managed contracts when one of these mechanisms reports drift:

- golden fixture comparison,
- downstream consumer verification,
- contract artifact freshness verification.
- request-boundary verification.

### Required Change Record

Every accepted Oracle change must produce evidence that records:

| Field | Requirement |
| --- | --- |
| Contract identity | Stable contract family or endpoint identity affected by the change. |
| Oracle source | Backend projection or command result and serialized fixture path. |
| Drift source | Fixture comparison, consumer verification, artifact freshness, or manual inventory finding. |
| Drift classification | Structural drift, compatibility-review drift, consumer drift, stale artifact, unexpected artifact modification, or missing artifact. |
| Authority owner | Backend authority responsible for the semantic or structural contract change. |
| Affected consumers | Rust shell, TypeScript types/API wrappers, dev Tauri mock, UI resources/hooks/components, tests, docs, or generated artifacts. |
| Compatibility path | Required compatibility field, version rule, consumer migration, or explicit statement that no compatibility path is required. |
| Fixture action | Preserve, update, add, split, or retire the fixture. |
| Artifact action | Preserve, refresh, regenerate, relocate, or retire each verified artifact. |
| Verification | Commands and test results proving the accepted baseline. |
| Rollback path | How to restore the prior accepted contract or compatibility behavior if downstream validation fails. |

### Canonical Sequence

1. Run the relevant Oracle verifier and capture the failing mechanism.
2. Classify the drift before changing fixtures or consumers.
3. Identify whether the backend projection or command result is the intended authority change.
4. If the backend change is not authoritative, repair the producer and keep the existing fixture baseline.
5. If the backend change is authoritative, record compatibility impact and affected consumers before updating any downstream artifact.
6. Update the golden fixture only after the authority and compatibility review is complete.
7. Refresh or regenerate verified consumer artifacts from the accepted fixture path. During Phase 0, manual artifacts may be updated only as verified contract artifacts with evidence.
8. Re-run fixture comparison, consumer verification, artifact freshness, and request-boundary verification for the affected contract family where applicable.
9. Update milestone evidence with the drift classification, fixture/artifact actions, verification commands, and rollback path.
10. Update durable contract documentation when the change alters contract lifecycle, versioning, compatibility, authority ownership, or consumer obligations.

### Classification Rules

| Drift classification | Acceptance rule |
| --- | --- |
| Structural drift | Do not update fixtures first. Prove the backend change is authoritative or restore the previous serialized shape. |
| Compatibility-review drift | Record additive path, consumer impact, compatibility owner, and retirement condition before allowing the fixture baseline to move. |
| Consumer drift | Keep the Oracle fixture unchanged. Update or quarantine the stale consumer with owner, risk, and retirement criteria. |
| Stale artifact | Refresh or regenerate the artifact through the approved workflow, then update the freshness manifest with evidence. |
| Unexpected manual artifact modification | Revert the artifact or accept it only with explicit authority, consumer, verification, and rollback evidence. |
| Missing expected artifact | Restore the artifact, approve relocation, or retire it through a documented compatibility path. |

### Fixture Pilot Workflow

For the current fixture pilots, the minimum fixture comparison command is:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractOracleFixtureTests
```

For the locally certified repository dashboard, repository workspace, and primary workflow projection pilots, the minimum backend acceptance command set also includes:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractConsumerVerificationTests
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractGeneratedArtifactFreshnessTests
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter FullyQualifiedName~ContractRequestBoundaryTests
```

The repository workspace local certification used the combined Oracle mechanism filter:

```powershell
dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter "FullyQualifiedName~ContractRequestBoundaryTests|FullyQualifiedName~ContractGeneratedArtifactFreshnessTests|FullyQualifiedName~ContractConsumerVerificationTests|FullyQualifiedName~ContractOracleFixtureTests"
```

The full backend test project remains the certification check before accepting a Milestone 0.2 checkpoint.

This workflow is not yet automation. It does not generate artifacts, assign contract versions mechanically, update manifests automatically, or certify additional contract families. Those remain pending Oracle lifecycle and generated ecosystem work.
