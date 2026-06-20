# Decisions

## Newly Authorized Decisions

- M2B.2 provider-launch boundary is accepted as correctly scoped.
- The launch path may be real without treating launch as execution completion.
- The next authorized slice is M2B.3: restart and orphan recovery.
- Backend startup must load persisted `Executing` sessions.
- Codex reattach is unsupported for this phase.
- An unrecoverable persisted `Executing` Codex session must be marked `Failed`.
- The repository execution state for that session must become `Failed`.
- The failure reason must be explicit and stable: `Active provider process could not be reattached after backend restart.`
- Unrecoverable sessions must not remain `Executing`.
- Unrecoverable sessions must not be silently converted to `Completed` or `Ready`.
- Non-executing sessions must restore without recovery mutation.
- Existing session metadata, including provider path, PID, and prompt metadata, must remain preserved during recovery mutation.

## Certification Required

- Startup reload finds a persisted `Executing` Codex session.
- Unsupported reattach marks the session `Failed`.
- Repository execution state becomes `Failed`.
- Failure reason is explicit and stable.
- Non-executing sessions restore without recovery mutation.
- Provider path, PID, and prompt metadata remain preserved.

## Explicitly Deferred

- Do not begin M3 monitoring or output capture during M2B.3.
- Do not add stdout or stderr event capture.
- Do not add SSE.
- Do not add completion detection.
- Do not add handoff validation.

## Next Authorized Slice

- Proceed with M2B.3 restart/orphan recovery, then close M2 as launch infrastructure complete.
