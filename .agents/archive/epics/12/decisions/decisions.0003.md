# Decisions: 2026-06-27 Phase 0 Runtime Primitives Acceptance

These decisions capture only newly authorized direction from the user response accepting the Phase 0 runtime primitives slice.

## Authorized Decisions

1. Accept the Phase 0 runtime primitives slice.
   - `SessionIdentity`, `SessionRole`, `AgentSessionSpec`, `SandboxProfile`, `EffortProfile`, `AgentProcessState`, and `AgentTurnState` are accepted as the shared runtime vocabulary for later runtime components.
   - The primitives remaining information-first and protected by architecture tests is accepted as aligned with the roadmap.

2. Treat `AgentSessionSpec` as the canonical description of a session.
   - Future services should ask what the session specifies rather than independently deriving sandbox, effort, startup, or role facts.
   - Immutable snapshot behavior for startup options is accepted as the correct direction for preserving session authority.

3. Keep `SessionRole` free of direct policy, prompt, permission, routing, and default mappings.
   - Role-to-session mappings should eventually live in an explicit builder or equivalent composition mechanism.
   - Avoid coupling runtime behavior around scattered `if (role == ...)` checks.

4. Preserve the ordered runtime layering.
   - Continue in the sequence: process runner, runtime primitives, `IAgentProcess`, `IAgentRuntime`, then Repository Runtime.
   - Do not introduce `IAgentRuntime` before `IAgentProcess`.

5. Keep Session, Process, and Turn separate.
   - Session is durable identity and specification.
   - Process is the live operating-system resource.
   - Turn is a unit of interaction within a session.
   - Conversation turns must not inherit process lifecycle semantics.

6. Use the next-slice sequence authorized by the acceptance response.
   - Review `CodexExecutableResolver`.
   - Introduce `IAgentProcess`.
   - Add process supervision.
   - Add stream abstraction.
   - Add registry.
   - Add `IAgentRuntime`.
   - Add compatibility adapter.
   - Begin persistent sessions only after those layers are independently protected.

## Evidence Targets

- `.agents/decisions/decisions.0002.md`
- `.agents/decisions/decisions.md`
- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/handoff.0001.md`
- `.agents/milestones/m0-runtime-foundation.md`
- `src/CommandCenter.Agents/Models/`
- `tests/CommandCenter.Backend.Tests/Architecture/AgentRuntimeBoundaryTests.cs`

## Next Authorized Sequence

1. Stage the completed Phase 0 runtime primitives slice and this decision rotation.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
