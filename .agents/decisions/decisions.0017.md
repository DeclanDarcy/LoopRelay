# Decisions

## Newly Authorized Decisions

- The native runtime path is considered substantially de-risked because `cargo run` exercised the actual Tauri shell, backend sidecar startup, HTTP connectivity, repository API access, and shutdown cleanup.
- The remaining M5 uncertainty is behavioral rather than infrastructural.
- M5 remains categorized as certification, polish, and acceptance work rather than implementation work.
- The next native certification pass should be one continuous desktop workflow rather than isolated behavior checks.
- The native certification workflow should cover repository registration, repository switching, artifact selection restoration, artifact edit/save, refresh persistence, handoff rotation, decision rotation, repository removal, and restart recovery.
- The highest-value M5-specific edge case is removing a repository that has a remembered selected artifact, verifying no orphaned selection state and no stale editor content remain.
- If the full native desktop certification pass succeeds without defects, effort should shift immediately to Epic 1 acceptance closure.
- Additional workspace mechanics should be avoided before formal acceptance unless certification discovers a defect.
