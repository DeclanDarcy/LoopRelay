# Milestone 9 Product Cohesion Audit

## Scope

This audit starts Milestone 9 by classifying navigation, projections, endpoint surfaces, frontend state, and presentation duplication. It does not authorize semantic changes; it identifies the highest-leverage consolidation targets for follow-on implementation.

## Primary Homes

| Capability | Primary home | Allowed contextual links |
| --- | --- | --- |
| Repository overview | `Workspace` tab, `SelectedRepositorySummary` | Sidebar repository selector, command palette repository targets |
| Workflow | `Workspace` tab summary plus `WorkflowOperationsPanel` | Header workflow status, execution/git/continuity links that navigate back to workflow-owned actions |
| Execution | `Execution` tab | Workspace live activity, workspace inspector execution session link, command palette execution-session targets |
| Operational context | `Operational Context` tab | Workspace summary, continuity warning links, decision assimilation and proposal links |
| Governance / decision sessions | `Governance` tab | Repository summary governance snapshot, command palette governance lifecycle target |
| Decisions | `Decisions` tab | Execution influence, operational-context assimilation, governance status links |
| Reasoning | `Reasoning` tab | Decision, execution, and continuity evidence links that reference reasoning-owned evidence |
| Continuity | `Continuity` tab | Operational-context warnings/compression/retention links |
| Health | Capability-local panels using shared `HealthView` | Dashboard summary only after preserving decomposed dimension detail |
| Diagnostics | Capability-local panels using shared `DiagnosticList` | Dashboard summary only after preserving finding/evidence detail |
| Certification | Capability-local certification panels | Dashboard certification rollup as observational summary |

## Navigation Classification

- Keep: repository selection in `Sidebar`, primary workspace tabs in `WorkspaceTabs`, command palette targets from `buildNavigationTargets`.
- Keep: contextual discovery targets for pending proposal, current execution, handoff review, git workflow, and continuity warnings.
- Consolidate: duplicate workspace tab lists in `WorkspaceTabs.tsx` and `lib/navigation.ts` should share one typed tab definition.
- Consolidate: static section targets should become the single registry for command-palette sections and UI deep links; ad hoc section ids in `App.tsx` should be audited against that registry before adding more.
- Remove later: disabled global nav items `Overview`, `Executions`, and `Insights` should either become real primary surfaces or be removed to avoid implying unavailable routes.

## Endpoint Disposition

| Surface | Disposition | Rationale |
| --- | --- | --- |
| Repository dashboard/workspace endpoints | Keep | Primary repository and workspace projections consumed by shell and tabs. |
| Workflow projection, diagnostics, gates, recovery, health, reports, certification | Keep | Workflow is canonical operational lifecycle and timeline authority. |
| Workflow execution/handoff/decisions/operational-context/git subprojections | Compatibility | Useful while capability tabs still consume mixed projections; candidates for redirect/retire after dashboard consolidation proves equivalent coverage. |
| Decision-session lifecycle projection, eligibility, transfer, recovery, health, certification | Keep | Governance tab owns the decision-session lifecycle surface. |
| Decision-session analysis metrics/statistics/economics/coherence | Internal | Specialized diagnostics; avoid making them first-class navigation until product need is clear. |
| Decision discovery, proposal lifecycle, review, refinement, resolution, supersede, archive | Keep | Decision pipeline product actions depend on these routes. |
| Decision quality, governance, influence, generation certification, certification | Keep | Authoritative transparency surfaces used by decision/execution views. |
| Execution active/session/status/events/transparency/prompt manifest | Keep | Execution tab depends on session state, events, and transparency. |
| Git eligibility/prepare/commit/push | Keep | Explicit workflow actions; remain execution-session scoped. |
| Operational-context generate/proposal/review/promote | Keep | Operational-context lifecycle actions. |
| Continuity diagnostics/reports | Keep | Continuity tab owns diagnostics and report generation. |
| Reasoning events, threads, relationships, graph, trace, query, reconstruction, materialization, certification | Keep | Reasoning tab owns reasoning transparency and capture. |
| Planning endpoint | Compatibility | Workspace execution context still consumes planning readiness; classify again after workflow/dashboard consolidation. |
| Artifact inventory/content/rotate endpoints | Keep | Artifact workspace and user-authored artifact lifecycle depend on them. |

## Projection Classification

| Projection | Classification | Notes |
| --- | --- | --- |
| `RepositoryDashboardProjection` | Authoritative dashboard projection | Primary repository list summary. |
| `RepositoryWorkspaceProjection` | Authoritative workspace projection | Primary repository workspace composition. |
| `RepositoryDecisionSessionSummary` | Derived consumer projection | Summary view of governance-owned state for repository surfaces. |
| `RepositoryReasoningSummary` | Derived consumer projection | Summary view of reasoning-owned state for repository surfaces. |
| Workflow projection types | Authoritative lifecycle projection | Canonical operational state and timeline. |
| Decision lifecycle eligibility projection | Authoritative eligibility read model | UI actions must consume this rather than revalidating lifecycle rules. |
| Explainability adapters | Derived presentation adapters | Disposable UI shaping; must not become authorities. |

## Frontend State Classification

| State | Classification | Notes |
| --- | --- | --- |
| `selectedRepositoryId`, selected artifact/milestone paths, active tab, section target, command palette visibility | Authoritative view state | User navigation state only. |
| `selectedCommitPaths`, `commitMessage`, artifact draft content, operational-context proposal draft/review note | Disposable UI state | User edits or selections; not domain authority until command submission. |
| Loading/mutating flags and toast message/error state | Disposable UI state | Operation feedback only. |
| Hook-owned projection data (`workspace`, workflow, governance, decisions, execution, reasoning, continuity) | Authoritative projection cache | Cache of backend-owned facts; refresh after mutations. |
| `selectedExecutionStatus` | Compatibility state | Mirrors session status and should be reviewed when execution/session projections are consolidated. |
| `operationalContextCurrentContent` and `generatedHandoffContent` | Compatibility content cache | Artifact content convenience; preserve as cache, not semantic authority. |
| Derived memo values such as execution history/display, git path counts, blocked reasons from projected eligibility | Derived display state | Allowed if recomputable from authoritative projections. |

## Duplicate Presentation Candidates

- Execution history is the first density target. `ExecutionHistoryPanel`, workspace live activity, workspace inspector execution summary, and command-palette execution targets all expose similar session/event facts.
- Workflow state appears in `Header`, `SelectedRepositorySummary`, `WorkflowOperationsPanel`, workflow panels, and execution/git panels. Keep workflow authority but reduce repeated status prose.
- Operational-context warnings appear in workspace inspector, command palette discovery, operational-context tab, and continuity tab. Keep contextual links but define one primary continuity warning detail presentation.
- Health and diagnostics mostly use shared primitives now; Milestone 9 should remove remaining local summary-only labels that hide dimensions, evidence, or findings.
- Disabled global navigation suggests product surfaces that do not exist. Either implement a unified operational dashboard or remove those items until the dashboard is real.

## Interaction Pattern Baseline

All lifecycle controls should converge on this visible sequence:

1. Action button or command.
2. Eligibility state and blocked reasons from an authority-owned projection.
3. Evidence supporting the current state or recommendation.
4. Result from the mutation.
5. Diagnostics if blocked, degraded, or failed.

First normalization targets:

- Handoff accept/reject, operational-context accept/reject/promote, governance transfer/recover, git commit/push, decision promote/dismiss/expire/duplicate, proposal generate/refine/archive/supersede.

## Next Implementation Target

Build the first product-cohesion change around navigation and density:

- centralize workspace tab metadata and static section ids into one navigation registry,
- remove or implement disabled global navigation entries,
- add characterization coverage proving every major capability remains reachable through one primary tab and selected contextual command-palette links,
- then collapse execution history/live-activity duplication without changing execution semantics.
