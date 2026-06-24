# Milestone 0 Authority Review

| Semantic concept | Authority | Projection | Consumers | Adjustment |
| --- | --- | --- | --- | --- |
| Workflow lifecycle | `CommandCenter.Workflow` | Workflow projection, history, timeline, gates | Repository workspace, execution workspace, governance, continuity | Milestone 1 must make this the sole UI workflow source. |
| Execution lifecycle | `CommandCenter.Execution` | Execution session summaries, events, context, git state; workflow execution projection contextualizes lifecycle stage | Execution workspace, workflow | Keep mutations in execution services; workflow observes and projects. |
| Decision lifecycle | `CommandCenter.Decisions` | Decision projections, proposals, review workspace, quality/governance/certification reports | Decision workspace, execution influence, workflow decisions | Add read-only lifecycle eligibility/action availability before enabling broader UI actions. |
| Decision-session lifecycle | `CommandCenter.DecisionSessions` | Registry, lifecycle projection/history, transfer eligibility/history, health/certification | Governance workspace, workflow decision-session summary | Add transfer/recovery command endpoints and shell/UI bridge in Milestone 2. |
| Reasoning | `CommandCenter.Reasoning` | Events, threads, graph, traces, reconstructions, certification | Reasoning workspace, future explainability | Extend reconstruction projection in Milestone 6 rather than inferring in UI. |
| Operational context | `CommandCenter.Continuity` | Current context, proposals, diagnostics, reports, semantic diff | Operational-context workspace, decisions, workflow | Add identity-aware diff details in Milestone 7. |
| Certification | Owning domain service per subsystem | Domain-specific certification result/report | Subsystem panels and final readiness | Certification remains observational and must not mutate authoritative state. |
| Recovery | Owning lifecycle service per subsystem | Workflow recovery, decision-session recovery, execution recovery state | Workflow/governance/execution UI | Route recovery commands through owning service, not UI-derived repair. |
| Observability | Owning subsystem service | Diagnostics, reports, timeline/history, findings | UI panels, tests | Render decomposed evidence; avoid summary-only health/status. |
| Repository summaries | `CommandCenter.Middle` composition | Dashboard/workspace projections | Dashboard/workspace UI | Keep as composition. Add missing UI type for `decisionSessionSummary`. |
| Health | Owning subsystem service | Health dimensions/findings/diagnostics | Workflow/governance/decision/reasoning panels | Render dimensions and evidence; no UI recomputation. |
| Diagnostics | Owning subsystem service | Diagnostics models | Feature panels and tests | Keep backend authoritative and presentation-only in UI. |

## Ambiguities To Resolve

- `RepositoryExecutionState` currently drives visual workflow state in React; this must be replaced by `CommandCenter.Workflow` projection data.
- Middle projections already include `RepositoryDecisionSessionSummary`, but TypeScript repository models do not expose it.
- Decision-session transfer and recovery services exist, but command endpoints and shell/UI invocation are incomplete.
- Decision lifecycle rules exist, but there is no UI-safe eligibility projection for action enablement.
- Execution push failures persist retry state and then throw; endpoint/client behavior must surface the refreshed retry context.
