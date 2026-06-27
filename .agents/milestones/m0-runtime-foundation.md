# Phase 0 - Runtime Foundation

Goal: establish permanent runtime boundaries without changing observable product behavior.

## Implementation

- [x] Add `src/CommandCenter.Agents` to the solution.
- [ ] Move role-agnostic process abstractions and implementations from `CommandCenter.Execution` into `CommandCenter.Agents`:
  - [x] `IProcessRunner`
  - [x] `ProcessRunner`
  - [x] process run/start result models
  - [x] Codex executable resolution reviewed and intentionally kept in Execution because it returns Execution-owned provider models and structured provider errors.
  - [x] `IAgentProcess`
  - [x] provider/process lifecycle primitives beyond the initial process handle
  - [x] stream/event primitives that are not execution-specific
- [ ] Keep operational concepts in `CommandCenter.Execution`: Git, execution context, handoff, operational prompts, execution session state, commit, push, and operational evidence.
- [ ] Introduce shared runtime primitives:
  - [x] `SessionIdentity`
  - [x] `SessionRole`
  - [x] `AgentSessionSpec`
  - [x] `SandboxProfile`
  - [x] `EffortProfile`
  - [x] `AgentProcessState`
  - [x] `AgentTurnState`
- [ ] Add generated prompt infrastructure under `CommandCenter.Core.Prompts` with named prompt builders for planning, execution, decisions, transfer, operational deltas, and context updates. Existing literal prompt composition in Execution must become a compatibility layer over generated prompt output.
- [ ] Add initial repository lifecycle models in Core or a new runtime-neutral model package:
  - `Idle`
  - `PlanAuthoring`
  - `PlanReady`
  - `ExecutingPlan`
  - `Completed`
- [ ] Add first-class information records for Planning Intent, Plan, Plan Revision, and Repository Run Identity without changing existing markdown persistence.
- [ ] Add architecture tests that prevent:
  - `CommandCenter.Agents` referencing Execution, Decisions, Workflow, Continuity, Reasoning, Middle, Backend, or UI.
  - DecisionSessions referencing operational Execution orchestration.
  - Runtime objects owning domain semantic decisions.
  - UI-local semantic inference for runtime lifecycle, health, eligibility, recovery, and certification.
- [ ] Harden persistence with a shared atomic JSON file writer used by application configuration, artifact store write paths where appropriate, execution session store, decision session repository, workflow repository, and continuity proposal store.
- [ ] Centralize existing execution and continuity state transition validation before new runtime state is added.

## Certification

- [ ] Existing backend and UI behavior remains unchanged.
- [ ] Execution still works through the existing public APIs.
- [ ] DecisionSessions still compile without referencing operational Execution orchestration.
- [ ] Architecture governance tests cover Agent Runtime boundaries, prompt authority, repository lifecycle ownership, and information authority.
- [ ] Contract fixtures and generated artifact freshness remain current.
