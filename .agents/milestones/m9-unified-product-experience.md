# Phase 9 - Product Integration

Goal: make the concrete Plan Authoring -> Execution -> Decision Loop feel like one repository workflow.

## Implementation

- [ ] Integrate `PlanAuthoringScreen` into the selected-repository workspace when `plan/status.planExists == false`.
- [ ] Keep the same screen active through planning, revision, execution startup, decision review, submit, continuation, and next decision.
- [ ] Add reusable stream views for planning, execution, and decision output while preserving distinct stream sources.
- [ ] Show implementation diagnostics only in secondary details. Primary UI terminology is Repository, Plan, Execution, Decision, and Status.
- [ ] Disable actions based on backend-owned state and local input completeness:
  - [ ] Write Plan requires Roadmap text;
  - [ ] Revise Plan requires Feedback text and an open planning session;
  - [ ] Execute Plan requires verified `.agents/plan.md`;
  - [ ] Submit requires editable decisions from a completed decision turn.
- [ ] Add clipboard support for the plan Copy icon through Tauri clipboard or `navigator.clipboard`.
- [ ] Present decision output as read-only while streaming and editable only after completion.
- [ ] Keep prompt names, source hashes, session ids, and router mechanics out of the primary workflow unless shown in diagnostics.
- [ ] Add E2E coverage for the full happy path with mocked or test-provider streams.

## Certification

- [ ] Users can author, revise, execute, review decisions, submit, and continue without navigating away from the workflow.
- [ ] UI never composes prompt text or infers prompt selection.
- [ ] UI handles stream failure, retry/reconnect if supported, cancelled turns, and missing artifacts.
- [ ] Accessibility and layout checks cover the Roadmap/Specs editor, stream regions, editable decisions, and action buttons.
