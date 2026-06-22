# Decisions

## Newly Authorized

- M4 backend read models are accepted as aligned with the intended architecture.
- The UI phase is now authorized to begin because backend contracts are mature enough for UI consumption.
- The UI must consume backend-owned read models rather than co-designing or reconstructing lifecycle contracts.
- Proposal browser, option comparison, evidence inspection, and source attribution remain backend-owned interpretations of authority, not authority itself.
- The active UI layering remains repository authority to decision records to review workspace to read models to UI.
- The primary UI failure mode to avoid is client-side lifecycle reconstruction.
- The next UI work should proceed in phases:
  1. Shell integration only: `DecisionLifecycleTab`, API client, types, and hook wiring.
  2. Proposal browser and state filters using the browser projection directly.
  3. Proposal viewer and option comparison using backend models as-is.
  4. Evidence viewer and source attribution viewer before review notes.
  5. Review notes and review workspace last.
- UI state checks should avoid spreading ad hoc `selectedProposal.status` style logic.
- Lifecycle authority remains backend-owned; the UI remains observational.

## Current Milestone Status

- M0 Domain Foundation is complete.
- M1 Context Resolution is complete.
- M2 Discovery is complete.
- M3 Proposal Lifecycle is complete.
- M4 Review Workspace is in progress.

## Newly Authorized Next Slice

- Begin M4 UI with Phase 1 shell integration only:
  - add decision UI types
  - add decision API client methods
  - add hook wiring
  - add `DecisionLifecycleTab`
  - wire the Decisions tab into the shell without complex workflow behavior
