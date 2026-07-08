# Roadmap CLI Transition Extraction Universal Details

This file contains only rules that apply across extracted Roadmap CLI
transitions. Milestone-specific prompt names, artifact paths, sequencing,
service contracts, route outputs, handler boundaries, and characterization
priorities live in `.agents/milestones/m*.md`.

The extraction remains a behavior-preserving refactor. Do not treat any detail
file or milestone checklist as permission to redesign state names, prompt names,
prompt contracts, artifact paths, journal events, lifecycle states, decision
text, recovery intents, or caller-visible outcomes.

## Universal Equivalence Rules

All extracted handlers must preserve these cross-cutting behaviors.

- Project Context preflight, prompt-contract snapshot emission, startup
  planning, resume planning, unblock planning, status reporting, cancellation
  persistence, terminal selection route persistence, and generic error
  reporting remain owned by `RoadmapStateMachine` unless a later explicit plan
  changes that.
- Projection resolution must keep current `ProjectionCache` behavior:
  manifest entry upsert happens before validation or stale-projection blocking,
  generated projection content is written after validation and manifest upsert,
  invalid or stale blocked projections write
  `.agents/evidence/blockers/projection-blocked.NNNN.md`, and projection
  failures occur before transition-start state persistence for the affected
  runtime prompt.
- Runtime and projection prompts still run through the read-only planning agent
  and still append the implementation-first prompt policy. Non-completed agent
  turns still fail with diagnostics. Silent output echo behavior remains in the
  prompt runner.
- `OperationCanceledException` must not be converted into durable transition
  failure by prompt helpers. It must propagate to outer `RunAsync`, which writes
  the cancelled state.
- Optional HITL capture is always a no-op when the capture service is absent or
  content is blank.
- Parse failures after artifact materialization must not be retroactively
  converted into prompt transition failures unless the current implementation
  already does so. Several transitions deliberately have completed state and
  evidence before parsing.
- Do not roll back partially materialized artifacts unless the current
  implementation already does. Split output is the main positive
  no-partial-write exception: the whole split bundle is interpreted before child
  epic files are written.
- Prompt-completed state is not artifact completion. Promotion-candidate flows
  must still perform their current post-prompt classification, validation,
  materialization, lifecycle, provenance, and final-state steps before claiming
  transition success.

## State Summary Preservation

`SaveStateAsync` state summaries must continue to refresh:

- active artifact rows for `.agents/core/roadmap-completion-context.md`,
  `.agents/selection.md`, and `.agents/epic.md`
- projection manifest counts
- split-family JSON count
- last decision id
- retained retired epics
- retained blockers
- carried transition intent unless explicitly replaced
- default next transitions unless explicitly supplied

Default next transitions remain:

- `CoreReady`: `BootstrapRoadmapCompletionContext`,
  `SelectNextStrategicInitiative`
- `RoadmapCompletionContextReady`: `SelectNextStrategicInitiative`
- `SelectNextStrategicInitiative`: `SelectNextEpic`
- `ActiveEpicReady`: `GenerateMilestoneDeepDives`
- `MilestoneSpecsReady`: none
- `EpicPreparationAudit`: `EpicPreparationAudit`
- `RetireEpic`: `SelectNextStrategicInitiative`
- `EvidenceGathering`: `GatherAdditionalEvidence`,
  `EvaluateEpicCompletionAndDrift`
- `EvidenceBlocked`: `Resolve blocker and rerun`

## Transition Input Snapshot Invariants

Prompt-specific input roles and secondary inputs are owned by the relevant
milestone files. Across every transition input snapshot, keep these details
identical to the current implementation:

- path ordering
- role joining
- required and present flags
- SHA-256 hashes for present inputs
- projection identity
- prompt context hash
- secondary input hash
- overall snapshot hash
