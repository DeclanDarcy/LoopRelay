# Milestone 0 Capability Disposition Register

| Capability group | Disposition | Rationale |
| --- | --- | --- |
| Repository registration, inventory, refresh, dashboard, workspace | Core MVP | Required entry point for every workspace. |
| Artifact storage, content, and handoff/decision rotation | Core MVP | Required for execution lifecycle and agent continuity. |
| Planning readiness | Core MVP | Existing repository readiness signal consumed by workspace status. |
| Workflow projection, gates, history, timeline, continuation, preparation, recovery, health, reports, certification | Core MVP | Canonical operational lifecycle and next milestone backbone. |
| Execution context, launch, monitoring, handoff accept/reject, commit, push | Core MVP | Required operational workflow from repository state to published result. |
| Decision-session registry, lifecycle, transfer eligibility/execution, recovery, observability, health, certification | Core MVP | Governance lifecycle is part of the MVP surface and feeds workflow gates. |
| Decision context, discovery, candidates, proposals, review, refinement, resolution, supersession, archive | Core MVP | Decision pipeline completion is explicitly in scope. |
| Decision quality, governance, influence, and certification | Core MVP | Required semantic transparency around decisions and execution influence. |
| Reasoning events, threads, graph, query, traces, reconstruction, materialization review, certification | Core MVP | Required reasoning transparency milestone. |
| Operational context lifecycle, proposal review, promotion, diagnostics, reports, compression, semantic diff | Core MVP | Required continuity and operational-context transparency milestone. |
| Middle repository composition projections | Infrastructure | Composition layer only; it must not own lifecycle rules. |
| Backend endpoint maps | Infrastructure | Transport surface over existing authorities. |
| Tauri command bridge | Infrastructure | Transport bridge; preserves backend semantics. |
| React hooks, API modules, and feature panels | Infrastructure | Presentation and command invocation only. |
| Fake/noop execution providers | Extension Point | Useful provider abstractions for local testing and development. |
| Generated shell schemas and static public assets | Infrastructure | Build/runtime support. |
| Client-side execution workflow derivation in `executionWorkflow.ts` | Retire | Competes with authoritative workflow projection and must be removed in Milestone 1. |
| Repository summary lifecycle labels derived from execution state | Retire | Keep only until workflow projection replaces them as operational status. |
| Workflow-like advisory evidence in decision-generation certification UI | Deferred | Useful evidence, but must remain advisory until unified explainability/workflow consumption is in place. |
| Richer reasoning reconstruction confidence rationale and scope | Deferred | Needs model/projection extension in Milestone 6. |
| Identity-aware operational-context modification detection | Deferred | Belongs to Milestone 7 continuity transparency. |
| Shared explainability presentation layer | Deferred | Belongs to Milestone 8 after authoritative fields are surfaced. |
