# Decisions

## Newly Authorized Decisions

- M5A.2 is authorized as the next slice.
- M5A.2 is UI-only acceptance controls.
- Add Tauri commands `accept_execution_handoff` and `reject_execution_handoff`.
- Tauri accept/reject commands must be thin HTTP bridges only.
- Do not add workflow logic to Tauri.
- Show accept/reject controls only when `RepositoryExecutionState == AwaitingAcceptance`.
- Do not infer accept/reject visibility from session state.
- Repository workflow state remains authoritative for acceptance control visibility.
- Accept action is a single `Accept Handoff` action.
- After accept succeeds, refresh workspace and dashboard projections.
- Do not manually transition client state after accept.
- Reject action must require confirmation before calling the backend.
- After reject succeeds, refresh workspace and dashboard projections.
- Display `AcceptedAt`, `RejectedAt`, and `DecisionNote` when available.
- Certification must verify controls are visible only in `AwaitingAcceptance`.
- Certification must verify controls are hidden for `Ready`, `Executing`, `Failed`, `Cancelled`, `AwaitingCommit`, and `AwaitingPush`.
- Certification must verify accept refreshes projection state to `AwaitingCommit` or `Ready`.
- Certification must verify reject confirmation then refreshes projection state to `Ready`.
- Certification must verify browser refresh, Tauri restart, and backend restart preserve accepted/rejected metadata visibility.

## Explicitly Deferred

- Do not implement Git automation in M5A.2.
- Do not implement commit scope, commit, push, or Git lifecycle UI until M6.
- Do not manually duplicate backend workflow transitions in the client.
