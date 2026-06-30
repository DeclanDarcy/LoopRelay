# Phase 3 - Plan Authoring Workflow

Goal: implement the Write Plan and Revise Plan flow from the original design.

## Implementation

- [ ] Add backend commands/endpoints:
  - [ ] `POST /api/repositories/{id}/plan/write` with `{ roadmap, specs[], newCodebase }`;
  - [ ] `POST /api/repositories/{id}/plan/revise` with `{ feedback }`;
  - [ ] `GET /api/repositories/{id}/plan/stream`;
  - [ ] `POST /api/repositories/{id}/plan/execute` as the handoff to Phase 4.
- [ ] On Write Plan, persist:
  - [ ] Roadmap -> `.agents/specs/roadmap.md`;
  - [ ] each Spec -> `.agents/specs/s{index}.md`.
- [ ] Open an Operational, ExtraHigh, held-open planning process.
- [ ] Select the generated prompt:
  - [ ] `WritePlanForNewCodebase.Text` when New Codebase is checked;
  - [ ] `WritePlanAgainstCodebase.Text` when unchecked.
- [ ] Stream the planning turn to the UI.
- [ ] On turn completion, verify `.agents/plan.md` exists and render it.
- [ ] Hold the planning process open for revisions.
- [ ] On Revise Plan, submit `RevisePlan.Render(feedback)` to the same held-open planning process.
- [ ] Record prompt provenance for initial plan and each revision.
- [ ] Add `src/CommandCenter.UI/src/features/planning/PlanAuthoringScreen.tsx`.
- [ ] Add UI controls:
  - [ ] Roadmap textarea;
  - [ ] unbounded Specs textareas plus Add Spec;
  - [ ] New Codebase checkbox, default unchecked;
  - [ ] Write Plan disabled until Roadmap has non-empty text;
  - [ ] planning stream;
  - [ ] rendered plan view;
  - [ ] copy icon button with no text label;
  - [ ] Feedback textarea;
  - [ ] Revise Plan disabled until Feedback has non-empty text;
  - [ ] Execute Plan button.

## Certification

- [ ] A repository without `.agents/plan.md` enters Plan Authoring.
- [ ] Roadmap/spec files are written before the initial planning prompt runs.
- [ ] Plan revision reuses the warm planning process.
- [ ] The UI does not compose prompt text or select prompt classes client-side.
- [ ] Planning stream completion leads to a verified rendered plan.
- [ ] Backend and UI tests cover write, revise, validation, stream completion, and missing-plan failure.
