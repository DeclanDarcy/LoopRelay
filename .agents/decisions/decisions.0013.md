# Decisions

## Newly Authorized Decisions

- M4A.1 boundary is accepted as correct.
- Provider completion and execution success are formally distinct lifecycle concepts.
- The current completion lifecycle is: provider exit code `0`, session `Completed`, handoff validation, then `AwaitingAcceptance`.
- Provider exit code `0` with no current handoff must transition to `Failed`.
- `ExecutionMonitoringService` owns provider lifecycle only.
- `HandoffService` owns completion validation and historical handoff preservation semantics.
- M4A.2 is authorized as the next implementation slice.
- M4A.2 scope is historical handoff preservation, archive failure handling, and sequence allocation certification.
- Historical archive workflow must run only after current handoff existence has already been established.
- On completion, compare the launch-time previous handoff snapshot with the current handoff.
- If prior and current handoff content are the same, create no archive and proceed to `AwaitingAcceptance`.
- If there was no launch-time previous handoff snapshot, create no archive and proceed to `AwaitingAcceptance`.
- If prior and current handoff content differ, write the previous snapshot to the next available `.agents/handoffs/handoff.NNNN.md`, then proceed to `AwaitingAcceptance`.
- Historical handoff sequence allocation must use the highest existing historical suffix plus one, not count-based numbering.
- If archive creation fails, keep the current handoff in place but mark the session and repository `Failed`.
- Archive failure must have an explicit stable failure reason.
- Do not silently continue to `AwaitingAcceptance` after archive failure.
- M4A.3 should wait until preservation is certified, then cover `AwaitingAcceptance` projection, restart restoration, handoff review endpoint, and handoff review UI.

## Explicitly Deferred

- Do not begin M4A.2 implementation in this slice.
- Do not expose acceptance or review behavior before archive semantics are certified.
