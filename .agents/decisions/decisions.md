# Decisions

## Newly Authorized Decisions

- M0-M4 are architecturally certified.
- M0-M4 are functionally certified except for native picker repository registration.
- M0-M4 remain awaiting final desktop certification signoff until the actual user path through the native folder picker is manually verified once.
- The only remaining uncertified workflow is:
  - click `Add Repository`.
  - select a repository through the native folder picker.
  - receive the selected path from the picker.
  - register the repository.
  - update the dashboard.
  - open the repository workspace.
- The remaining risk is localized to platform UI integration, not repository, artifact, planning, lifecycle, projection, or React-to-Tauri-to-backend behavior.
- The previous rendered UI certification slice counts as evidence collection, not an architecture change.
- Do not record final M0-M4 certification authorization until native picker repository registration succeeds through one manual desktop pass.
- If the native picker pass succeeds, M0-M4 certification may be considered complete without requiring additional certification work before proceeding to M5.
- After successful native picker verification, the next authorized workflow is:
  - rotate `decisions.md`.
  - record M0-M4 certification authorization.
  - stage.
  - commit.
  - push.
  - begin M5.
