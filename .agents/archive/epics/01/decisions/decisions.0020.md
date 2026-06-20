# Decisions

## Newly Authorized Decisions

- M5 must not be declared closed yet.
- M5 remains `Certification In Progress` until the actual native Tauri desktop workflow is observed end-to-end.
- The remaining M5 gap is specifically the native desktop path: Tauri window, native directory picker, and real user interaction.
- The `APPDATA` override behavior is a known testability limitation and certification-environment concern, not a workspace defect.
- For Epic 1 certification, backing up and restoring the real Command Center configuration is sufficient; a configuration-root seam may be introduced later as future hardening.
- The next pass should be a closure exercise, not a search for new functionality.
- The final M5 certification pass should be one continuous native run covering config backup, Tauri launch, repository registration, repository switching, artifact edit/save, refresh, handoff rotation twice, decisions rotation twice, repository removal, app restart, recovery verification, and config restore.
- If the native certification pass succeeds, M5 should be considered certified and Epic 1 complete.
- After successful native certification, rotate `decisions.md`, record M5 certification authorization and Epic 1 acceptance authorization, then stage, commit, push, and begin the next Epic.
- Do not add more workspace mechanics, UI state, or dashboard features before the native certification pass.
