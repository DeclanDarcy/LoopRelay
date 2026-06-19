# Decisions

## Newly Authorized Decisions

- Backend certification for M0-M4 is effectively complete.
- Repository registration, repository persistence, `.agents` creation, artifact discovery, artifact load/save, refresh rebuilds, planning/readiness, handoff rotation, decision rotation, restart recovery, and projection composition are now low-risk certification areas.
- The remaining M0-M4 certification uncertainty is confined to the actual rendered UI interaction path: user to React to Tauri IPC to backend.
- M1, M2, M3, and M4 are functionally certified with final UI validation pending.
- The final desktop UI certification pass must validate:
  - Add Repository through the native picker.
  - repository selection and workspace opening.
  - artifact open, edit, save, refresh, and persisted content.
  - handoff rotation twice, producing first and second historical files.
  - decision rotation twice, producing first and second historical files.
  - unsaved-change rotation blocking.
  - repository removal through confirmation.
  - restart restoration of repositories and workspace state.
  - visible readiness states for `MissingPlan`, `MissingMilestones`, and `Ready`.
- Unsaved-change rotation blocking must be explicitly validated manually because it combines UI state and artifact lifecycle behavior.
- `cargo tauri build` being unavailable is a tooling absence and does not block M0-M4 functional certification.
- `cargo build --release` failing with Rust compiler `STATUS_ACCESS_VIOLATION` is a known environmental build issue, not an M0-M4 functional certification failure, unless Epic 1 later requires release packaging certification.
- If the final desktop UI certification pass succeeds cleanly, M0-M4 should be considered fully certified.
- After clean M0-M4 desktop UI certification, rotate decisions, record certification authorization, commit, push, and begin M5.
