# Decisions

## Newly Authorized Decisions

- M5 remaining uncertainty is operational rather than architectural.
- Backend workspace logic is considered fully exercised within authorized M5 scope through tests, API certification, browser mock certification, and native shell smoke.
- The `APPDATA` override behavior is classified as a testability concern, not an M5 functional defect.
- Future hardening should make the Command Center configuration location explicitly injectable or testable instead of relying on environment assumptions.
- Remaining uncertified M5 surface is limited to the rendered desktop window workflows.
- The final M5 certification pass should be one uninterrupted native desktop scenario covering repository setup, workspace switching and selection restore, persistence, lifecycle rotation, cleanup, and quit/restart recovery.
- No additional M5 implementation work should be added unless final native desktop certification discovers a concrete defect.
- If the final native desktop pass finds no defects, M5 should be closed and work should move to Epic 1 acceptance closure.
