# Decisions

## Newly Authorized

- Continue Milestone 6 with a capture-attempt result model for skipped and deduplicated inferred reasoning capture.
- Skipped or deduplicated inferred capture must be observable without creating a fake reasoning event.
- The capture-attempt result should expose attempted capture mode, result, skip reason, duplicate signal, existing event reference, source transition, source artifact, source timestamp, and diagnostics.
- Capture-attempt results should distinguish captured, skipped, and duplicate outcomes.
- Wire only the surfaces that need to show capture attempts.
- Skipped attempts must not be treated as durable reasoning events.
