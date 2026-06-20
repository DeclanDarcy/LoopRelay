# Decisions

## Newly Authorized Decisions

- M2B.1 is accepted as architecturally complete.
- Treat the prompt boundary as a certified architecture rule:
  - Providers consume `ExecutionPrompt`.
  - Providers do not consume `ExecutionContext`.
  - Providers do not discover repository artifacts.
- The next authorized slice is M2B.2: real provider launch and PID persistence.
- M2B.2 should answer only whether Command Center can launch a real Codex process.
- Add `CodexExecutionProvider` and a Codex executable resolver, or equivalent structure.
- Resolve the Codex executable from `COMMAND_CENTER_CODEX_PATH` first, then `PATH`.
- Provider launch errors should be structured as executable-not-found, executable-not-executable, and launch-failed cases rather than generic failures.
- The Codex process must start with the repository root as the working directory.
- Capture and persist provider name, executable path, process id, and started time after successful launch.
- Persist prompt metadata, not full prompt text, unless a later explicit audit decision changes that.
- Suggested prompt metadata includes prompt length, generated time, included artifacts, and whether dirty state was present.
- Add certification for provider start failure leaving the repository `Ready` and the session `Failed`.
- Add certification that process id persists after session-store reload.
- Add or preserve certification that providers receive `ExecutionPrompt` and never receive `ExecutionContext`.

## Explicitly Deferred

- Do not add stdout capture.
- Do not add stderr capture.
- Do not add execution event models.
- Do not add `ExecutionMonitoringService` behavior.
- Do not add SSE or `EventSource` integration.
- Do not add output retention.
- Do not add completion detection.
- Do not add handoff validation, handoff archive rotation, or `AwaitingAcceptance`.
- Do not add accept, reject, commit, or push workflow.

## Next Authorized Slice

- Proceed with M2B.2 real provider launch and PID persistence, then stop before monitoring work.
