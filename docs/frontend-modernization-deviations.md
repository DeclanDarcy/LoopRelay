# Frontend Modernization Deviations

This ledger records intentional differences between the target Command Center frontend and the current product implementation. Missing workflow capability is treated as a backend-owned gap unless the backend already exposes the required projection or command.

## User-invokable Abort Execution

- Location or surface: Execution workspace, command palette.
- Description: The UI does not expose an abort/cancel execution action.
- Reason: There is no backend abort contract, endpoint, Tauri command, provider cancellation behavior, or execution-session transition authority.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Abort/cancel service contract, state transition tests, provider/process cancellation, monitoring event, endpoint, Tauri command, and updated execution projections.
- Follow-up owner or issue reference: None recorded.

## Global Overview

- Location or surface: Sidebar global navigation.
- Description: Overview is visible as a global navigation placement but remains disabled.
- Reason: The only cross-repository projection currently available is the repository dashboard list; there is no separate backend-owned global overview projection or product behavior.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Either a repository landing page explicitly backed by dashboard projections or a new overview projection.
- Follow-up owner or issue reference: None recorded.

## Global Executions

- Location or surface: Sidebar global navigation.
- Description: Executions is visible as a global navigation placement but remains disabled.
- Reason: Execution details and event streams are repository/session scoped. There is no cross-repository execution projection.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Cross-repository execution list/projection with repository identity, session state, activity, and authorized workflow destinations.
- Follow-up owner or issue reference: None recorded.

## Insights

- Location or surface: Sidebar global navigation.
- Description: Insights is visible as a global navigation placement but remains disabled.
- Reason: Continuity diagnostics and operational-context projections are repository scoped. There is no backend-owned insight or rollup projection.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Cross-repository continuity and operational-context insight projection.
- Follow-up owner or issue reference: None recorded.

## Notifications

- Location or surface: Header notification placement.
- Description: The header keeps a disabled notification placement instead of showing counts or a notification menu.
- Reason: There is no backend notification projection, and showing a synthetic count would misrepresent product state.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Notification projection with count, severity, destination, timestamps, and read/unread state if notifications become functional.
- Follow-up owner or issue reference: None recorded.

## Repository Git Summary For All Repositories

- Location or surface: Sidebar repository list, repository dashboard list, header.
- Description: Branch, dirty count, ahead count, and behind count are not shown for every repository.
- Reason: Git status exists for the selected repository or execution evidence only. React should not fan out per-repository git calls to synthesize dashboard state.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Extend `RepositoryDashboardProjection` with branch, dirty count, ahead count, behind count, and captured timestamp if global repository git summaries are required.
- Follow-up owner or issue reference: None recorded.

## Ahead/Behind Counts Outside Selected Repository Status

- Location or surface: Sidebar, header, repository dashboard.
- Description: Ahead/behind counts appear only where selected repository git status, commit preparation, push review, or execution snapshots provide them.
- Reason: The backend does not project ahead/behind counts for all repositories.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Dashboard-level git summary fields captured by backend repository projection services.
- Follow-up owner or issue reference: None recorded.

## Milestone Criteria Progress

- Location or surface: Workspace milestone rail and milestone selector.
- Description: Milestones show artifact identity and selection state, but not criteria/progress metrics.
- Reason: Milestone artifact inventory does not include parsed criteria, progress state, or completion authority.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Backend milestone criteria parser and progress projection, if criteria progress becomes product behavior.
- Follow-up owner or issue reference: None recorded.

## Cross-repository Execution Views

- Location or surface: Global Executions navigation, command palette discovery.
- Description: Execution navigation is repository/session scoped, not a global execution command center.
- Reason: Existing backend commands and projections are repository/session scoped.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Cross-repository execution projection and navigation semantics.
- Follow-up owner or issue reference: None recorded.

## Cross-repository Continuity And Insight Rollups

- Location or surface: Global Insights navigation, Continuity workspace.
- Description: Continuity and insight views are scoped to the selected repository.
- Reason: Existing continuity diagnostics, reports, and operational-context projections are repository scoped.
- Category: Capability.
- Outcome: Deferred.
- Required backend projection or capability: Cross-repository continuity/insight rollup projection.
- Follow-up owner or issue reference: None recorded.
