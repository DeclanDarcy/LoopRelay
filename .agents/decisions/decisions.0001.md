# Decisions

## Newly Authorized Decisions

- M0 is functionally implemented but not certified.
- M0 milestone exit is not approved until the desktop runtime path is verified end to end.
- M1 is blocked until M0 runtime certification is complete.
- Backend sidecar lifecycle belongs in M0 rather than being deferred to M1.
- The preferred M0 certification path is: launch Tauri, Tauri starts backend, `Ping Backend` returns `Pong`, closing the app terminates the backend.
- Manual backend launch is not the preferred certification path because it creates immediate runtime-foundation debt.
- Current absence of historical handoff and decisions artifacts requires no archive creation.
- Current absence of `decisions.md` before this file was consistent with the milestone state.
