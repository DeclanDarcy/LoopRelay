# Decisions

## Newly Authorized

- Proceed with the next Milestone 7 backend-only slice.
- Wire structured semantic modifications through operational evolution reporting and existing continuity diagnostics/certification surfaces.
- Preserve `ItemChanged` as a first-class semantic change, not a compatibility alias for remove/add.
- Diagnostics and certification must report modified item count, identity basis, previous/current state, and supporting evidence.
- Add backend regression tests for evolution reporting and diagnostics/certification consumption.
- Keep UI rendering deferred until the backend operational evolution projection is stable.
- Stage all resulting changes, create one commit for this execution session, push the branch, and then stop.
