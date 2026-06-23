# Decisions

## Newly Authorized

- Treat the template-to-UI-to-capture flow as the correct Milestone 2 UI completion shape.
- Keep the backend as the reasoning vocabulary authority; the UI consumes approved capture templates rather than inventing reasoning vocabulary.
- Keep event-family UI language in classification terms such as "Contradiction Events", "Direction Events", and "Hypothesis Events"; do not use language that implies first-class contradiction, direction, or hypothesis entities.
- Treat manual reasoning capture as user-selected template capture into immutable events, not creation of mutable reasoning objects.
- Treat `UserSupplied` provenance as the correct representation for human-originated reasoning observations.
- Treat the Tauri bridge operations `list_reasoning_manual_capture_templates` and `capture_manual_reasoning` as appropriately scoped capture operations.
- Treat M0 Boundary Foundation, M1 Event Substrate, and M2 Cross-Artifact Capture as complete in a meaningful architectural sense.
- Treat contextual "Record Reasoning" affordances as the next improvement after M2, focused on provenance/reference prefill quality without changing the event-led model.
- Preserve the invariant for contextual capture: context may prefill manual capture inputs, but must not auto-capture events unless the source transition qualifies for inferred capture.
- Shift the next major architectural center of gravity toward M3/M4 concerns: graph navigation, causality tracing, narrative reconstruction, and explaining why decisions changed.
