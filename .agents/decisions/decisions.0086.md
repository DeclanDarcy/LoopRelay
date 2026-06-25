# Decisions

## Newly Authorized

- Continue Milestone 9 using the established cohesion rule:
  - detailed lifecycle operations have one primary home,
  - secondary surfaces summarize authoritative state and navigate to the primary home.
- Preserve the current primary/secondary surface map:
  - Workflow primary presentation: Workflow Operations.
  - Workflow secondary surfaces: contextual summaries plus navigation.
  - Execution primary presentation: Execution tab.
  - Execution secondary surfaces: workspace summaries plus navigation.
  - Governance primary presentation: Governance Workspace at `#governance-workspace`.
  - Governance secondary surfaces: repository summaries plus navigation.
- Treat the Governance consolidation slice as accepted Milestone 9 work because `SelectedRepositorySummary` is now an overview, not a duplicate governance workspace.
- Continue Milestone 9 with Decision consolidation as the next implementation slice.
- For Decision consolidation:
  - keep the Decisions tab as the authoritative detailed lifecycle workspace,
  - reduce dashboard, repository, workspace, and adjacent decision surfaces to current decision status, active proposal summary, outstanding review state, key recommendation indicator, and navigation into Decisions,
  - avoid reproducing option evaluation, recommendation reasoning, quality analysis, burden analysis, governance explanation, and execution influence outside the Decisions workspace,
  - add characterization coverage protecting architectural intent, not only rendering.
- After Decision consolidation, continue the remaining Milestone 9 sequence:
  - reasoning summary consolidation,
  - continuity summary consolidation,
  - information-density refinement,
  - interaction normalization,
  - endpoint/projection/component retirement,
  - final cohesion audit.
