# Phase 9 - Unified Product Experience

Goal: make the product a coherent projection of the certified Repository Knowledge architecture, so planning, execution, decisions, understanding, history, evidence, lineage, queries, and health feel like one repository experience.

## Implementation

- [ ] Promote repository lifecycle into primary navigation and workspace composition, but make Repository Knowledge the product's center and Repository Understanding the default living view.
- [ ] Build a unified repository workspace that centers:
  - current understanding
  - repository knowledge
  - current objective
  - current plan
  - current execution turn
  - current decision review
  - knowledge and evidence
  - current health
  - conversation timeline
- [ ] Move diagnostics, reasoning internals, governance internals, continuity mechanics, contracts, and raw runtime health into secondary inspection surfaces.
- [ ] Keep prompt template mechanics out of primary workflows while exposing prompt provenance in secondary diagnostics when it explains history, recovery, or compatibility.
- [ ] Unify visible terminology:
  - Repository
  - Understanding
  - Plan
  - Execution
  - Decision
  - Knowledge
  - History
  - Health
- [ ] Hide runtime/session/registry/provider/transfer terminology from primary workflows unless the user is inspecting diagnostics.
- [ ] Hide prompt class names and source hashes from primary workflows unless the user is inspecting artifact lineage, provenance, or diagnostics.
- [ ] Replace fragmented streams with one repository conversation timeline backed by typed backend projections.
- [ ] Present operational context only as Repository Understanding in primary UI. Markdown and context mechanics remain implementation details.
- [ ] Complete decision-first review surface:
  - proposal
  - evidence
  - tradeoffs
  - editable human revision
  - submit
  - history
- [ ] Add knowledge, lineage, evidence, and understanding-evolution surfaces to the repository workspace without making them separate product silos.
- [ ] Add provenance inspection that can show which generated prompt and `SourceHash` produced a plan revision, execution handoff, decision output, operational delta, or understanding update.
- [ ] Add navigation and command palette flows that follow repository lifecycle and information needs, not project boundaries.
- [ ] Add product contracts for repository lifecycle, conversation timeline, repository dashboard, navigation, repository understanding, knowledge, history, evidence, prompt provenance, and decision review.

## Certification

- [ ] Users can plan, execute, review decisions, continue, inspect understanding, inspect Repository Knowledge, and view history without leaving the repository workspace.
- [ ] Runtime transitions are visible only as activity/health/progress, not implementation machinery.
- [ ] Product surfaces render backend-owned information instead of reconstructing it in React.
- [ ] Product surfaces never compose prompt text or infer prompt selection client-side.
- [ ] UI characterization and E2E tests cover the complete repository lifecycle and the Repository Understanding-centered workspace.
- [ ] Product terminology is consistent across components, empty states, buttons, headings, and diagnostics.
