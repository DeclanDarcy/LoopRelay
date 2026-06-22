# Decisions

## Newly Authorized

- Treat the emerging M0.6 authority model as a certified frontend authority model, not a loose collection of characterization tests.
- Use this invariant as a primary frontend architecture rule: navigation state, draft state, and projection state must not mutate workflow state; only explicit workflow actions may mutate workflow state.
- Treat the earlier commit-preparation leak as evidence that M0.6 characterizations are validating real architectural boundaries.
- Make continuity diagnostics and report generation the next M0.6 characterization target.
- Characterize continuity authority with this split:
  - diagnostics load and refresh are projection retrieval only;
  - repository/tab/navigation changes must not generate reports;
  - `Generate Report` is the only path that may invoke report generation.
- Consider M0.6 substantially complete when every workflow mutation path has been checked against navigation, projection refresh, draft edit, and explicit workflow action triggers.
- After continuity report characterization, inventory remaining workflow-mutating backend commands and characterize them systematically rather than opportunistically.
