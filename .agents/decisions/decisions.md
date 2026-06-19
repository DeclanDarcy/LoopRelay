# Decisions

## Newly Authorized Decisions

- The shell/runtime certification path now counts as certification progress beyond backend validation.
- The validated runtime chain is Tauri shell to backend sidecar startup to backend operations to restart recovery.
- The shell/runtime certification substantially reduces uncertainty around process lifecycle, configuration persistence, projection rebuilds, repository recovery, and rotation persistence.
- The remaining M0-M4 certification gap is React-to-Tauri rendered UI behavior and user interaction, not backend behavior.
- Final desktop-window certification must cover the native picker flow, artifact editing and saving, artifact rotation, repository removal, and restart reconstruction through the UI.
- Final desktop-window certification must also verify that attempting to rotate a current handoff or decisions artifact with unsaved editor changes is blocked by the UI.
- No new architectural concerns surfaced during certification; filesystem authority, projection authority, archive-only rotation, and filesystem-derived readiness remain accepted invariants.
- If final desktop-window certification passes cleanly, M0-M4 should be considered fully certified.
- After clean final desktop-window certification, there is little value in continuing certification-only work before M5.
- The next intended sequence is final desktop certification, record certification decisions, rotate decisions, commit and push, then begin M5.
- Do not begin meaningful M5 implementation until the final desktop-window certification pass completes.

