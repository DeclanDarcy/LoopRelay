# Decisions

## Newly Authorized

- Continue Milestone 6 with capture-specific grouped diagnostics next.
- Capture-specific grouped diagnostics should remain backend-owned and render-only in the UI.
- Capture mode should determine the diagnostic group a user sees, not merely appear as an event label.
- Backend capture diagnostic groups should include manual capture, assisted capture, inferred capture, skipped capture, and duplicate capture.
- Capture diagnostic groups should expose relevant existing structured facts, including capture reason, provenance, source transition, source artifact, duplicate fingerprint, skip reason, existing event reference, and capture-attempt diagnostics.
- Event feed and capture surfaces should explain why capture succeeded, was skipped, or resolved to an existing event without frontend synthesis.
