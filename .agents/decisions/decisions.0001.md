# Decisions

## Newly Authorized Decisions

- M1 is accepted as a clean completion aligned with the Epic 2 sequencing.
- Execution context resolution is accepted as a first-class backend capability that remains deterministic, reviewable, and separate from launch authority.
- The Epic 2 layering remains authoritative: M0 architecture, M1 context resolution, M2 session lifecycle, M3 monitoring, M4 handoff, M5 acceptance, M6 Git lifecycle, M7 unified workspace, and M8 repeatable execution loop.
- M2 should be split into M2A and M2B.
- M2A should prove session store, state transitions, active session rules, and fake provider behavior before any real Codex process launch.
- M2B should add prompt construction, real provider integration, process launch, and restart recovery after the session model is proven.
- `ExecutionSessionService` must own duplicate active-session protection as a backend invariant: one repository may have only one active execution session.
- Execution session persistence is now the critical path for downstream launch, monitoring, recovery, handoff validation, acceptance, commit, and push behavior.
- Prompt construction should be treated as a backend contract, preferably through `ExecutionPromptBuilder` and `ExecutionPrompt`, before provider invocation.
- Recovery semantics should stay within the existing session states: `Created`, `Executing`, `Completed`, `Failed`, and `Cancelled`; avoid adding intermediate states such as unknown, recovering, or reconnecting.

## Next Authorized Slice

- Proceed into M2A.1: session store, fake provider, start endpoint, active session endpoint, session lookup endpoint, and backend tests for launch, duplicate launch blocking, store reload, fake provider failure, and context-validation launch blocking.
