# Milestone 9 Workflow Consolidation

## Scope

- Continued Milestone 9 duplicate-surface consolidation for workflow displays.
- Preserved `WorkflowOperationsPanel` in the Workspace tab as the primary detailed workflow presentation.
- Converted the Execution tab workflow rail into a contextual summary panel.
- Added a `workflow-operations` section anchor and navigation target for contextual links into the primary workflow surface.

## Consolidation

- Execution now renders authoritative workflow summary facts only: current stage, progress, blocking gate, required action, open-gate count, and next stages.
- Execution no longer renders the primary five-row workflow lifecycle rail.
- The Execution workflow summary includes a `Workflow` action that navigates to the Workspace tab workflow operations section.
- Workflow operations remains the detailed surface for overview, recovery, health, certification, gates, reports, history, and continuation.

## Verification

- `npm test -- executionWorkflowRail.test.tsx navigation.test.ts workflowAuthority.test.ts`
- `npm test -- executionWorkflowRail.test.tsx workflowPanels.test.tsx workflowAuthority.test.ts navigation.test.ts workspaceLiveActivityPanel.test.tsx workspaceInspectorRail.test.tsx executionHistoryPanel.test.tsx executionEventFeed.test.tsx shellHeader.test.tsx selectedRepositorySummary.test.tsx`
- `npm run build`

## Residual Risk

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
- The Workspace tab still contains both the compact workspace workflow rail and full workflow operations panel; this is intentionally left for a later pass because the current slice focused on removing workflow duplication from Execution.
- Remaining Milestone 9 consolidation targets include governance summaries, reasoning confidence displays, continuity evolution summaries, health widgets, and certification summaries.
