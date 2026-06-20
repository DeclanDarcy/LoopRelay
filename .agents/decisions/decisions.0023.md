# Decisions

## Newly Authorized Decisions

- M7.2 is authorized as Workspace Certification & Hardening.
- The next slice must focus on certification, not new features.
- M7 must continue preserving the backend authority model while consolidating visibility.
- The unified execution workspace must remain a projection of existing authorities, not a new workspace state machine.
- Backend authorities remain distinct for Context, Execution, Handoff, Acceptance, Commit, and Push.
- The lifecycle rail is authorized only as status explanation.
- The lifecycle rail must not become navigation or a second control surface.
- M7.2 certification must verify both individual lifecycle states and state transitions.
- M7.2 certification must cover `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- M7.2 transition certification must verify:
  - `Executing -> AwaitingAcceptance`;
  - `AwaitingAcceptance -> AwaitingCommit`;
  - `AwaitingCommit -> AwaitingPush`;
  - `AwaitingPush -> Ready`.
- M7.2 hardening must deliberately inspect density risks:
  - long milestone identifiers;
  - large commit scopes;
  - large handoff content;
  - large output streams;
  - responsive layout behavior;
  - visual hierarchy under load.
- M7.2 must add no new workflow behavior, no new authority, and no new lifecycle states.

## Explicitly Deferred

- M8 remains focused on the repeatable execution loop after M7 certification.
