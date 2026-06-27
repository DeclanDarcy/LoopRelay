# Decisions: 2026-06-27 Phase 0 Agent Event Vocabulary

These decisions capture only newly authorized direction for the next Phase 0 stream/event primitive slice.

## Evidence

- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `.agents/milestones/m0-runtime-foundation.md`
- `.agents/handoffs/handoff.md`

## Authorized Decisions

1. Proceed with Phase 0 stream/event primitives from the certified backend baseline.
   - The next implementation target is a canonical event vocabulary in `CommandCenter.Agents`.
   - The event layer is not a streaming subsystem or orchestration layer.

2. Keep the process state machine as lifecycle authority.
   - `AgentProcessStateMachine` remains the source of lifecycle facts.
   - Agent events are projections of those facts for observers.
   - Supervisors, future runtimes, and repository runtimes may observe events but must not derive lifecycle authority from them.

3. Prohibit command semantics in the event layer.
   - Events describe facts only.
   - Events may describe start, output, completion, failure, cancellation, disposal, and narrowly scoped diagnostics if needed.
   - Events must not express restart, retry, transfer, runtime creation, repository run creation, or other orchestration intent.

4. Keep the event vocabulary intentionally small and role-agnostic.
   - Initial vocabulary should stay close to `ProcessStarted`, `ProcessOutput`, `ProcessCompleted`, `ProcessFailed`, `ProcessCancelled`, and `ProcessDisposed`.
   - `ProcessDiagnostic` is allowed only if implementation needs a factual diagnostic channel.
   - Event payloads must not mention repository, planning, execution, decisions, workflow, Git, operational context, or other higher-layer concepts.

5. Design events for future replay without implementing replay in this slice.
   - Event records should naturally carry stable identity, process identity, timestamp, event kind, ordering semantics, and payload.
   - Replay, reconstruction, repository timelines, and conversation timelines remain future-layer responsibilities.

## Next Authorized Sequence

1. Stage this decision rotation only.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
