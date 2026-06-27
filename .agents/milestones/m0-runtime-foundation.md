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
- [ ] Keep operational concepts in `CommandCenter.Execution`: Git, execution context, handoff, operational prompt inputs, execution session state, commit, push, and operational evidence. Execution must not own canonical prompt text.
- [ ] Introduce shared runtime primitives:
  - [x] `SessionIdentity`
  - [x] `SessionRole`
  - [x] `AgentSessionSpec`
  - [x] `SandboxProfile`
  - [x] `EffortProfile`
  - [x] `AgentProcessState`
  - [x] `AgentTurnState`
- [ ] Add generated prompt infrastructure under `CommandCenter.Core.Prompts` using `Lib.Prompts`:
  - [ ] Wire `Lib.Prompts` as an analyzer-only build dependency for `CommandCenter.Core`.
  - [ ] Treat `src/CommandCenter.Core/Prompts/*.prompt` as the authored source of truth.
  - [ ] Generate static prompt classes with `Template`, `SourceHash`, and `Render(...)`.
  - [ ] Fail builds on malformed prompt placeholders through `PROMPT001`-`PROMPT004`.
  - [ ] Certify prompt discovery so `PROMPT100` cannot be ignored accidentally.
- [ ] Establish the canonical prompt catalog:
  - [ ] Planning: `WritePlanAgainstCodebase`, `WritePlanForNewCodebase`, `RevisePlan`, `ExtractMilestones`.
  - [ ] Operational execution: `StartExecution`, `ContinueExecution`.
  - [ ] Decision sessions: `StartDecisionSession`, `StartDecisionSessionFromTransfer`, `GetNextDecisions`.
  - [ ] Continuity: `ProduceOperationalDelta`, `UpdateOperationalContext`.
- [ ] Add prompt selection and rendering adapters that call generated prompt classes from domain-owned inputs:
  - [ ] Planning selects initial-plan, revision, and milestone-extraction prompts.
  - [ ] Execution selects start and continuation prompts.
  - [ ] DecisionSessions select start, transfer-start, and next-decision prompts.
  - [ ] Continuity selects operational-delta and context-update prompts.
- [ ] Existing literal prompt composition in Execution, Planning, DecisionSessions, Workflow, Continuity, Backend, UI, tests, and compatibility paths must become compatibility layers over generated prompt output or be removed.
- [ ] Add prompt provenance models used by future runtime turns:
  - prompt name
  - generated type
  - `SourceHash`
  - session role
  - workflow phase
  - input artifact identities
  - output artifact identities
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
  - Runtime objects selecting or composing canonical prompt text.
  - Canonical prompt text existing outside `src/CommandCenter.Core/Prompts/*.prompt` and generated `CommandCenter.Core.Prompts` output.
  - UI-local semantic inference for runtime lifecycle, health, eligibility, recovery, and certification.
- [ ] Harden persistence with a shared atomic JSON file writer used by application configuration, artifact store write paths where appropriate, execution session store, decision session repository, workflow repository, and continuity proposal store.
- [ ] Centralize existing execution and continuity state transition validation before new runtime state is added.

## Certification

- [ ] Existing backend and UI behavior remains unchanged.
- [ ] Execution still works through the existing public APIs.
- [ ] DecisionSessions still compile without referencing operational Execution orchestration.
- [ ] Architecture governance tests cover Agent Runtime boundaries, prompt authority, repository lifecycle ownership, and information authority.
- [ ] Generated prompt classes compile and expose stable `Template`, `SourceHash`, and `Render(...)` APIs for every canonical prompt.
- [ ] Prompt selection tests prove each session role and workflow phase uses the expected generated prompt.
- [ ] Prompt provenance is captured for rendered prompts before any persistent agent runtime depends on it.
- [ ] No literal canonical prompt strings remain outside authored `.prompt` files and generated code.
- [ ] Contract fixtures and generated artifact freshness remain current.
