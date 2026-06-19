# Decisions

## Newly Authorized Decisions

- The backend API certification smoke is accepted as real certification progress for M1-M4, not just additional automated testing.
- Backend certification for M1-M4 is largely complete.
- Desktop certification remains the primary remaining gate before M5.
- The remaining risk is actual desktop workflow behavior through the Tauri application, not foundational backend service correctness.
- Continue certification only until full Tauri desktop-path certification completes.
- Do not begin meaningful M5 implementation until desktop certification completes.
- Desktop certification before M5 must cover repository lifecycle, artifact lifecycle, rotation, refresh, planning readiness, and restart recovery.
- Repository lifecycle desktop certification must verify app launch, repository registration, workspace open, repository removal, restart, and restored state.
- Artifact lifecycle desktop certification must verify artifact open, edit, save, refresh, persistence, restart, and persistence after restart.
- Rotation desktop certification must verify repeated handoff rotation creates `handoff.0001.md` and `handoff.0002.md`, and repeated decision rotation creates `decisions.0001.md` and `decisions.0002.md`.
- Refresh desktop certification must verify externally added artifacts appear after refresh and externally deleted artifacts disappear after refresh.
- Planning desktop certification must verify UI rendering for `MissingPlan`, `MissingMilestones`, and `Ready` matches backend state.
- Restart recovery desktop certification must verify repositories, artifacts, readiness, and workspace state restore after closing and restarting the app.
- If desktop certification passes cleanly, M0-M4 can be considered fully certified and M5 may proceed as lower-risk workspace composition, navigation refinement, state restoration, dashboard polish, and missing-state polish.
