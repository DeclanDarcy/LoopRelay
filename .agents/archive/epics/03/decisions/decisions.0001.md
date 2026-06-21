# Decisions

## Newly Authorized Decisions

- `OperationalContextDocument` is ratified as the canonical internal model for operational context; Markdown is repository serialization only.
- Parser, generator, diff, compression, and UI projection work must converge through `OperationalContextDocument` rather than independently interpreting Markdown.
- Unknown Markdown preservation is mandatory to avoid user data loss across parser, serializer, and future schema evolution.
- Initial semantic diffing must remain deterministic, coarse, and explainable; sophisticated understanding diffing is deferred until there is sufficient operational history.
- M1 must remain limited to operational-context presence, size, parsing, ordering, and passive consumption validation.
- M1 must explicitly reject operational-context confidence, context quality score, continuity score, understanding health, or similar diagnostic scoring.

## Pre-M1 Verification Items

- Confirm stable identity rules are explicit enough for future compression and certification to reason about surviving constraints, decisions, questions, and risks.
- Confirm section authority is explicit enough to distinguish AI generation, user edits, review edits, compression changes, and promotion behavior.
- Confirm unknown-section round-trip behavior is explicit enough that parse, store, and serialize paths preserve unknown content consistently.
