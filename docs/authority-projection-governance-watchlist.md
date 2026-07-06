# Authority and Projection Governance Watchlist

This watchlist is an M0.4 governance detector for authority-like and projection-like source file names. It is intentionally narrow: new entries do not certify semantic correctness, but they make new named authority or projection artifacts visible before later authority and projection milestones add stronger purity checks.

## Detection Scope

The executable guard scans source file names under `src/` and `tests/LoopRelay.Backend.Tests/` with extensions `.cs`, `.ts`, `.tsx`, and `.rs`.

Watched names contain `Authority` or `Projection` in the file name. A new matching file must be added to the appropriate inventory below with scoped rationale before the governance suite can pass.

## Exclusions

- File contents, type names, method names, field names, and markdown references are not scanned by this slice.
- Generated outputs are not separately classified here; generated-artifact governance remains owned by contract generation and artifact freshness mechanisms.
- Archived `.agents/` material is excluded because it is evidence history, not active source shape.
- Frontend architecture tests outside the watched name pattern are excluded until frontend authority and presentation guards are introduced.

## Accepted Exceptions

The current watchlist includes tests and hooks because their file names are intentional architectural signals. Their presence does not make tests or hooks semantic authorities.

The watchlist also includes repository and workflow projection services that are existing projection surfaces. This M0.4 slice records their names only; projection purity and source authority remain later milestone work.

## Authority-Like File Inventory

| Source file | Current classification | Governance rationale |
| --- | --- | --- |
| `src/LoopRelay.Decisions/Models/DecisionReviewAuthority.cs` | Existing authority-like model | Records a decision review authority model name that must remain visible until the authority taxonomy and semantic authority inventory certify ownership. |
| `src/LoopRelay.UI/src/test/characterization/decisionTransparencyAuthority.test.ts` | Existing authority characterization test | Protects frontend characterization around authority transparency without making the UI authoritative. |
| `src/LoopRelay.UI/src/test/characterization/workflowAuthority.test.ts` | Existing authority characterization test | Protects workflow authority characterization without making the UI authoritative. |

## Projection-Like File Inventory

| Source file | Current classification | Governance rationale |
| --- | --- | --- |
| `src/LoopRelay.Continuity/Models/DecisionAssimilationProjection.cs` | Existing projection model | Current projection-shaped continuity read model; semantic source and purity remain later authority/projection work. |
| `src/LoopRelay.Continuity/Models/OperationalContextProjection.cs` | Existing projection model | Current projection-shaped operational-context read model; semantic source and purity remain later authority/projection work. |
| `src/LoopRelay.Core/Planning/PlanningProjection.cs` | Existing projection model | Current planning projection model; ownership remains visible for projection taxonomy work. |
| `src/LoopRelay.Decisions/Abstractions/IDecisionArtifactProjectionService.cs` | Existing projection service contract | Current projection service boundary; later milestones must classify ownership and invalidation. |
| `src/LoopRelay.Decisions/Abstractions/IDecisionContextProjectionService.cs` | Existing projection service contract | Current projection service boundary; later milestones must classify ownership and invalidation. |
| `src/LoopRelay.Decisions/Abstractions/IDecisionProjectionService.cs` | Existing projection service contract | Current projection service boundary; later milestones must classify ownership and invalidation. |
| `src/LoopRelay.Decisions/Models/DecisionProjectionDiagnostics.cs` | Existing projection diagnostics model | Current diagnostics attached to projection-shaped decision output; semantic source remains later authority work. |
| `src/LoopRelay.Decisions/Models/ExecutionDecisionProjection.cs` | Existing projection model | Current projection-shaped execution/decision read model; ownership remains visible for projection taxonomy work. |
| `src/LoopRelay.Decisions/Primitives/ExecutionProjectionKind.cs` | Existing projection primitive | Current projection-kind primitive; later milestones must ensure it remains classification, not downstream authority. |
| `src/LoopRelay.Decisions/Services/DecisionArtifactProjectionService.cs` | Existing projection service | Current projection service implementation; purity and authority source remain later milestone work. |
| `src/LoopRelay.Decisions/Services/DecisionProjectionService.cs` | Existing projection service | Current projection service implementation; purity and authority source remain later milestone work. |
| `src/LoopRelay.Middle/Projections/IRepositoryProjectionService.cs` | Existing projection service contract | Current middle-layer projection boundary; contract and projection ownership remain visible. |
| `src/LoopRelay.Middle/Projections/RepositoryDashboardProjection.cs` | Existing projection model | Current repository dashboard projection; already covered by Oracle pilots but not globally certified as projection taxonomy. |
| `src/LoopRelay.Middle/Projections/RepositoryProjectionService.cs` | Existing projection service | Current repository projection service; already covered by Oracle pilots for selected contracts only. |
| `src/LoopRelay.Middle/Projections/RepositoryWorkspaceProjection.cs` | Existing projection model | Current repository workspace projection; already covered by Oracle pilots but not globally certified as projection taxonomy. |
| `src/LoopRelay.Reasoning/Abstractions/IReasoningArtifactProjectionService.cs` | Existing projection service contract | Current reasoning projection service boundary; later milestones must classify ownership and invalidation. |
| `src/LoopRelay.Reasoning/Projections/ReasoningArtifactProjectionService.cs` | Existing projection service | Current reasoning projection service implementation; purity and authority source remain later milestone work. |
| `src/LoopRelay.UI/src/hooks/useWorkflowProjection.ts` | Existing projection-consuming hook | Frontend consumer of backend projection; watchlist entry does not authorize frontend semantic projection ownership. |
| `src/LoopRelay.UI/src/test/characterization/projectionHooks.test.tsx` | Existing projection characterization test | Test file for projection-consuming hooks; does not create projection authority. |
| `src/LoopRelay.Workflow/Abstractions/IWorkflowProjectionService.cs` | Existing projection service contract | Current workflow projection service boundary; later milestones must classify ownership and invalidation. |
| `src/LoopRelay.Workflow/Models/WorkflowDecisionProjection.cs` | Existing projection model | Current workflow decision projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowExecutionProjection.cs` | Existing projection model | Current workflow execution projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowGateCatalogProjection.cs` | Existing projection model | Current workflow gate catalog projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowGateHistoryProjection.cs` | Existing projection model | Current workflow gate history projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowGitProjection.cs` | Existing projection model | Current workflow Git projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowHandoffProjection.cs` | Existing projection model | Current workflow handoff projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowHistoryProjection.cs` | Existing projection model | Current workflow history projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowOperationalContextProjection.cs` | Existing projection model | Current workflow operational-context projection model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Models/WorkflowProjectionDiagnostics.cs` | Existing projection diagnostics model | Current workflow projection diagnostics model; semantic source remains later authority work. |
| `src/LoopRelay.Workflow/Services/WorkflowProjectionService.cs` | Existing projection service | Current workflow projection service implementation; purity and authority source remain later milestone work. |
| `tests/LoopRelay.Backend.Tests/DecisionArtifactProjectionServiceTests.cs` | Existing projection test | Backend test for an existing projection service; included because file names are watched. |
| `tests/LoopRelay.Backend.Tests/DecisionProjectionServiceTests.cs` | Existing projection test | Backend test for an existing projection service; included because file names are watched. |
| `tests/LoopRelay.Backend.Tests/RepositoryProjectionServiceTests.cs` | Existing projection test | Backend test for an existing projection service; included because file names are watched. |
| `tests/LoopRelay.Backend.Tests/WorkflowProjectionServiceTests.cs` | Existing projection test | Backend test for an existing projection service; included because file names are watched. |

## Non-Claims

This watchlist does not prove:

- that a listed authority-like file is the correct semantic authority,
- that a listed projection is pure,
- that every authority or projection in the codebase has a matching file-name signal,
- that downstream consumers do not infer semantics,
- or that future authority/projection taxonomy work can be skipped.
