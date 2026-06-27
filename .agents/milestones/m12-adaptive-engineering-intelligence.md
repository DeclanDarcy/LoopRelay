# Phase 12 - Deferred Non-Goals and Final Definition of Done

Goal: close the implementation against the original design and explicitly defer broader product expansion.

## Non-Goals For This Plan

- [ ] Do not build a Repository Knowledge platform as part of this flow.
- [ ] Do not add adaptive engineering intelligence, opportunity discovery, recommendation generation, or platform-wide learning.
- [ ] Do not add knowledge graph, lineage explorer, repository query surface, or trend analysis unless separately approved after this design is complete.
- [ ] Do not let the UI infer lifecycle legality, decision validity, router behavior, prompt selection, or artifact authority.
- [ ] Do not let prompts become semantic authority; they remain generated communication mechanisms.
- [ ] Do not let the orchestrator become a domain service for Execution, Decisions, Continuity, Git, Workflow, or contracts.

## Final Acceptance

- [ ] From a repository with no `.agents/plan.md`, Plan Authoring is shown.
- [ ] Roadmap and Specs are written to `.agents/specs`.
- [ ] Write Plan uses the correct generated prompt and creates `.agents/plan.md`.
- [ ] Revise Plan uses `RevisePlan.Render(feedback)` in the same planning process.
- [ ] Execute Plan closes planning, copies operational context, caches plan text, extracts milestones, commits/pushes, sets `ExecutingPlan`, starts execution, and rotates `handoff.0001.md`.
- [ ] Decision session starts in a separate zero-permission Codex process.
- [ ] `GetNextDecisions.Render(handoff)` streams proposed decisions that become editable after completion.
- [ ] Submit persists edited decisions and runs `ContinueExecution.Render(plan, handoff, decisions)`.
- [ ] Each continuation produces and rotates the next handoff.
- [ ] Router `Continue` reuses the warm Decision process.
- [ ] Router `Transfer` writes operational delta, rewrites operational context, starts a fresh Decision process, and resumes decision streaming.
- [ ] All prompt text comes from generated `CommandCenter.Core.Prompts` classes.
- [ ] Execution and DecisionSessions reach Codex only through `CommandCenter.Agents`.
- [ ] Full certification commands and relevant contract/governance suites pass.

## Completion Statement

The design is complete when the user can stay on one repository screen from initial roadmap/spec authoring through repeated decision-mediated execution turns, with persistent planning and decision Codex processes where required, faithful artifact writes under `.agents`, generated prompt provenance for every agent turn, and router-driven reuse or transfer of the active Decision process.
