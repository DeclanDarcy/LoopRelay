# Decisions

## Newly Authorized

- Treat the discovery of unfinished M4 work as material to the closure review: the roadmap was not complete until those gaps were resolved.
- Treat the completed M4 additions as architecturally consistent because they resolved the gap without changing the architecture.
- Treat `ReasoningQuery.HistoricalAt` as the approved historical reconstruction mechanism, provided historical state remains derived from event timelines, graph, trace, and reconstruction rather than persisted as authority.
- Preserve the reconstruction/report boundary:
  - `POST /reasoning/reconstructions` remains non-persisting.
  - `POST /reasoning/reconstructions/reports` is the explicit persisted artifact-generation path.
- Continue treating report discovery and artifact inventory exposure as visibility, not authority.
- Run the closure review against the original answerability questions and require restart, recovery, scale, historical point-in-time, and certification evidence.
- Limit closure-review risk analysis to:
  - historical reconstruction correctness,
  - reference integrity,
  - recovery determinism,
  - answerability determinism,
  - authority leakage.
- Continue rejecting hypothesis, alternative, contradiction, direction, graph, narrative, specialized read-model, specialized reconstruction-engine, and historical-state persistence absent a concrete failure case.
- Deliberately attempt to falsify the central hypothesis that derived reasoning infrastructure is sufficient.
- If the closure review cannot produce a concrete failure case, close Reasoning Trajectory Preservation rather than creating an exploratory Milestone 9.
