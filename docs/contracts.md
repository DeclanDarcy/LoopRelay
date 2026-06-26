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
- Priority endpoint rows for the first fixture candidates.

The catalog is not a generated schema. It is an inventory and fixture-selection mechanism used to prevent fixtures from certifying accidental or consumer-owned shape.

The repository dashboard pilot currently exposes one compatibility finding: the Rust `RepositoryDashboardProjection` mirror omits `decisionSessionSummary`, while the backend and TypeScript dashboard contracts include it. This is evidence for the Oracle and a later shell/manual-mirror migration; the Oracle fixture must not treat the Rust mirror as contract authority.

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

The repository dashboard candidate now satisfies the pre-fixture field ownership and serialization-observation gate. Fixture capture still requires an explicit representative-data choice so the golden sample exercises nulls, empty arrays, non-empty execution data, and non-empty decision-session data intentionally.
