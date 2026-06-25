# Decisions

## Newly Authorized

- Treat the workflow presentation consolidation slice as aligned with Milestone 9 because presentation hierarchy now mirrors domain authority.
- Keep `WorkflowOperationsPanel` via `#workflow-operations` as the single detailed workflow home.
- Treat `ExecutionWorkflowRail` as a contextual workflow summary only, showing current workflow summary, contextual status, and navigation back to Workflow.
- Preserve the cohesion pattern that each capability has one primary presentation and secondary surfaces summarize and deep-link:
  - Execution primary presentation: Execution tab.
  - Execution contextual surfaces: Workspace summaries.
  - Workflow primary presentation: Workflow Operations.
  - Workflow contextual surfaces: Execution workflow summary.
- Preserve regression coverage that protects architectural rules, especially navigation and workflow-authority characterization tests.
- Continue Milestone 9 with governance summary consolidation as the next slice.
- For governance consolidation:
  - inventory governance presentations,
  - classify each governance surface as primary, contextual summary, compatibility, or retire candidate,
  - keep the Governance tab as the only detailed location for lifecycle, recovery, transfer, certification, health, and observability,
  - convert other governance surfaces to summaries and deep-links,
  - add characterization tests proving secondary governance surfaces expose summary information, navigate to Governance, and do not reproduce complete lifecycle semantics.
- After governance consolidation, continue with decision summaries, reasoning summaries, and continuity summaries before information density, layout refinement, terminology normalization, and broader UX polish.
