# Phase 0 - Runtime Foundation

Goal: ratify the revised session-role invariant, extract shared process infrastructure, and establish generated prompt authority before product behavior changes.

## Implementation

- [x] Add `src/CommandCenter.Agents` to the solution.
- [x] Preserve the already-started extraction of role-agnostic process primitives into `CommandCenter.Agents`, including `IProcessRunner`, `ProcessRunner`, process run/start result models, `IAgentProcess`, stream/event primitives, `SessionIdentity`, `SessionRole`, `AgentSessionSpec`, `SandboxProfile`, `EffortProfile`, `AgentProcessState`, and `AgentTurnState`.
- [ ] Finish the extraction so `CommandCenter.Agents` owns only process spawning, streaming, lifecycle, session identity, role, sandbox, effort, working directory, and session handles.
- [ ] Keep operational concepts in `CommandCenter.Execution`: Git, code mutation, commit, push, handoff creation, execution context, operational prompt input shaping, execution evidence, and execution session state.
- [ ] Record the governing invariant: Operational Session and Decision Session are distinct roles; both are backed by real Codex processes; both use `CommandCenter.Agents`; DecisionSessions must not reference Execution operational orchestration.
- [ ] Add or complete `Lib.Prompts` wiring for `CommandCenter.Core` as an analyzer-only dependency.
- [ ] Treat `src/CommandCenter.Core/Prompts/*.prompt` as the only authored prompt source.
- [ ] Certify the canonical prompt catalog:
  - [ ] `WritePlanForNewCodebase.Text`
  - [ ] `WritePlanAgainstCodebase.Text`
  - [ ] `ExtractMilestones.Text`
  - [ ] `ProduceOperationalDelta.Text`
  - [ ] `UpdateOperationalContext.Text`
  - [ ] `RevisePlan.Render(feedback)`
  - [ ] `StartExecution.Render(plan)`
  - [ ] `GetNextDecisions.Render(handoff)`
  - [ ] `StartDecisionSession.Render(operationalContext)`
  - [ ] `StartDecisionSessionFromTransfer.Render(operationalContext)`
  - [ ] `ContinueExecution.Render(plan, handoff, decisions)`
- [ ] Add prompt provenance models for prompt name, generated type, `SourceHash`, session role, workflow phase, input artifacts, and output artifacts.
- [ ] Add architecture tests preventing prompt literals outside `.prompt` files/generated output and preventing `CommandCenter.Agents` from depending on Execution, DecisionSessions, Decisions, Workflow, Continuity, Backend, UI, or shell projects.

## Certification

- [ ] Existing execution behavior remains unchanged through compatibility adapters.
- [ ] Generated prompt classes compile and expose the expected signatures.
- [ ] No runtime service selects or composes canonical prompt text outside generated prompt renderers.
- [ ] DecisionSessions and Execution remain separate role/domain concepts.
- [ ] Backend tests and architecture governance tests pass.
