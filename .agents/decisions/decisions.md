# Decisions

## Newly Authorized

- Keeping M8 open after extracting `ArtifactWorkspace` was correct because the extraction proved `App.tsx` still contained presentation responsibilities, not only authority.
- Use the tightened M8 closure question: extract any remaining `App.tsx` code that can move without transferring authority; close M8 only when no such extractable code remains.
- Continue the responsibility inventory on Git workflow surfaces and generated handoff surfaces.
- Classify each remaining block by asking whether it decides or displays.
- Extract presentation-only Git workflow rendering such as status display, commit preview display, push preview display, history display, and validation display.
- Keep Git workflow authority in `App.tsx`, including commit dispatch, push dispatch, readiness ownership, draft ownership, and backend invocation.
- Extract generated handoff presentation such as handoff rendering, preview, metadata display, and generated content presentation.
- Keep generated handoff authority in `App.tsx`, including generation, refresh, save, and backend invocation.
- Document any technically presentational block intentionally retained in `App.tsx` only when extraction would create disproportionate complexity.
- Treat the remaining M8 question as whether `App.tsx` has reached its natural authority boundary.
