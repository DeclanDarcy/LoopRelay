# Decisions

## Newly Authorized

- Accept the completed Milestone 5 structured governed conflict diagnostics slice as aligned with the execution authority model.
- Preserve the split between compatibility validation strings and structured semantic authority projection: `validationErrors` remains compatibility surface, while `governedConflicts` is the semantic execution validation authority projection.
- Treat governed execution conflicts as authority collisions, not generic errors.
- Proceed next with handoff processing transparency as the next high-value Milestone 5 slice.
- Handoff processing transparency should expose handoff produced or missing state, archive state, archive path or sequence, archive failure, validation state, validation failure, resulting session state, and whether provider failure differs from handoff processing failure.
- Wire the handoff processing transparency slice through the established vertical path: backend projection, endpoint, Tauri command, TypeScript client/types, hook, UI, and tests.
- After handoff processing transparency, continue with semantic execution event grouping and then final Milestone 5 exit audit and cleanup.
- Stage, commit, push this decision-rotation update and current structured-conflict slice, then stop.
