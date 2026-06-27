# Decisions: 2026-06-27 Phase 0 Prompt Infrastructure Direction

These decisions capture only newly authorized direction introduced after acceptance of the Phase 0 agent event primitive slice.

## Evidence

- `.agents/handoffs/handoff.md`
- `.agents/milestones/m0-runtime-foundation.md`
- `C:\Users\dfdar\.codex\attachments\2736e3e7-9aef-4497-9732-bb234c2d560f\pasted-text.txt`

## Authorized Decisions

1. Accept the Phase 0 agent event primitive slice.
   - The event layer remains observational only.
   - `AgentProcessStateMachine` remains lifecycle authority.
   - Process-local in-memory event streams are the correct current scope.
   - Durable replay, stream contracts, repository timelines, and UI consumers remain future work.

2. Proceed next with generated prompt infrastructure under `CommandCenter.Core.Prompts`.
   - Prompt work is the next Phase 0 implementation slice.
   - The goal is to move descriptions into shared infrastructure before moving behavior.

3. Treat prompts as generated artifacts, not executable logic.
   - Prompt builders transform authoritative information into prompt documents.
   - Agent runtime consumes generated prompt documents.
   - Prompt composition must remain deterministic, testable, and independent of process execution.

4. Establish a common prompt generation abstraction before specialized role builders.
   - The initial shape should separate prompt inputs, prompt builders, and prompt documents.
   - Planning, execution, decision, transfer, operational delta, and context update prompt builders should be role-specific implementations.
   - Runtime should not accumulate role-switch prompt composition logic.

5. Keep prompt builders dependent on information, not orchestration services.
   - Builders may consume prepared information records such as Planning Intent, Operational Context, Repository Understanding, and Decision Context.
   - Builders must not reach into repositories, runtimes, orchestration services, or process services to gather their own inputs.

## Next Authorized Sequence

1. Stage the completed event primitive slice, handoff rotation, milestone update, and decision rotation.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
