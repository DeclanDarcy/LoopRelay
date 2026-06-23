# Decisions

## Newly Authorized

- Continue M5 as a mechanism to evaluate materialization pressure, not to materialize artifacts.
- Keep materialization review outputs advisory findings unless the Decision Lifecycle explicitly adopts them.
- Preserve ownership:
  - Reasoning identifies materialization pressure.
  - Decision Lifecycle decides any authoritative action.
- Keep the burden of proof high before strengthening `Direction` beyond derived reconstruction.
- Treat evidence such as repeated reconstruction failure, repeated user inability to answer questions, significant performance constraints, or demonstrable ambiguity as the kind of pressure required before considering stronger materialization.
- Continue flagging event family/type growth when classifications start resembling hidden lifecycle states.
- Reaffirm:
  - Thread equals navigation/grouping.
  - Trace equals reconstruction unit.
- Build the M5 UI slice next using:
  - Tauri bridge.
  - UI API.
  - Hook.
  - Mock support.
  - `ReasoningMaterializationReviewPanel`.
  - Characterization tests.
- Keep the first materialization review UI intentionally simple:
  - Finding.
  - Evidence.
  - Recommendation.
  - Rationale.
- The materialization review UI must feel like architecture review, not an artifact approval workflow.
- Avoid UI labels and flows that imply governance authority, including `Approved` and `Rejected`.
- Prefer advisory UI language such as:
  - Derived remains sufficient.
  - Materialization pressure observed.
  - Evidence insufficient.
  - Further review recommended.
- Do not add approval workflows, status management, or review-history lifecycles for materialization review until demonstrated need exists.
