# Decisions

## Newly Authorized

- Structured consequence and contradiction transparency must be implemented before Milestone 7 UI work.
- Consequences and contradictions must become first-class continuity concepts instead of remaining embedded only in warning text.
- Consequence records must include originating decision references, operational statement, affected area, supporting evidence, and operational impact.
- Contradiction records must model a symmetric relationship between two decision references plus conflict type, conflict evidence, severity, and resolution guidance.
- Existing warning-string surfaces must remain compatible, but should be generated from structured backend records where possible.
- Backend regression tests for this slice must cover consequence generation, contradiction detection, multiple contradictions, severity, and compatibility string generation from structured records.
- This slice remains backend-only; defer UI rendering until the continuity projection surface is sufficiently complete.
