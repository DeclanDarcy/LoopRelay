# Roadmap Transition Semantic Wrapper Plan

## Purpose

The first semantic realization slice made `RepositoryWork` executable end to end:

```text
subject -> intent -> source -> admission -> interaction -> evidence -> candidate artifact
  -> promotion -> decision -> state -> recovery -> certification -> distillation -> capability
```

This plan targets the next vertical slice. It does not broaden the semantic framework. It uses the completed `RepositoryWork` path as the governing parent and brings one existing Roadmap CLI transition under semantic control.

The new slice answers:

```text
Can one existing Roadmap state-machine transition execute through RepositoryWork semantic admission,
leave durable evidence, and prove behavior equivalence without retiring the legacy path yet?
```

## Target Slice

The target governed subject is:

```text
RoadmapTransition
```

The target instance is the smallest existing transition that can prove the migration boundary:

```text
RepositoryWork/RoadmapTransition: StatusReport
```

Why this transition first:

- It is already executable from the Roadmap CLI through `status`.
- It is low risk because it is report-oriented and should not mutate roadmap state.
- It exercises current-state loading, transition classification, report rendering, and human navigability.
- It lets the semantic wrapper distinguish report output from execution authority.
- It creates the migration pattern before wrapping mutating transitions such as selection, promotion, unblock, or completion.

Do not select a mutating transition for this slice unless `StatusReport` is already semantically wrapped and behavior-equivalence evidence exists.

## Inputs

- `semantic-constitution.md`
- `canonical-semantic-architecture-roadmap.md`
- `semantic-architecture-migration-roadmap.md`
- Existing `RepositoryWork` semantic artifacts under `.agents/semantic/repository-work`
- Existing Roadmap CLI `status` behavior
- Existing roadmap state, artifact lifecycle, decision ledger, projection manifest, transition journal, and blocker records
- Existing Roadmap CLI tests

## Governing Relationship

This slice introduces a child governed subject without creating a broad subject registry:

```text
RepositoryWork
  owns semantic authority for
RoadmapTransition:StatusReport
```

The child identity must not be a method name, command string, or file path. It should be derived from:

```text
parent RepositoryWork subject id
  + subject type RoadmapTransition
  + transition key StatusReport
```

The transition may keep using existing Roadmap CLI state-store and report logic internally during this slice. The semantic wrapper owns admission, evidence, decision, and equivalence records. The legacy implementation remains the execution mechanism until a later retirement milestone.

## Non-Negotiable Boundaries

- `RepositoryWork` remains the parent governed continuity.
- `RoadmapTransition:StatusReport` is a child subject, not a new root.
- Protocol admission must happen before status execution.
- The status report is an observation until validated and bound as evidence.
- Report-only output cannot create execution, mutation, acceptance, recovery, or certification authority.
- Behavior equivalence must compare semantic-wrapper output against legacy status behavior.
- Legacy status behavior must not be retired in this slice.
- No generic subject registry, protocol registry, event store, or artifact platform should be introduced.

## Executable Outcome

A command or host operation can execute:

```text
semantic roadmap-transition status <repo>
```

or an equivalent internal operation, and persist:

```text
load RepositoryWork parent
  -> create or load RoadmapTransition:StatusReport subject
  -> capture intent
  -> capture current roadmap state source snapshot
  -> admit report-only transition protocol
  -> execute legacy status behavior through semantic wrapper
  -> capture raw status output as observation
  -> validate observation against current state and legacy behavior
  -> bind accepted evidence
  -> persist report-only decision
  -> write behavior-equivalence record
  -> emit semantic transition report
```

The legacy `status` command should continue to work unchanged.

## Durable Artifacts

Store artifacts beneath:

```text
.agents/semantic/repository-work/transitions/status-report/
```

Required artifacts:

- `subject-identity.json`
- `protocol-definition.json`
- `admissions/admission.<run-id>.json`
- `sources/current-roadmap-state.<run-id>.json` or markdown equivalent
- `observations/status-output.<run-id>.md`
- `evidence/status-evidence.<run-id>.md`
- `decisions/report-only-decision.<run-id>.json`
- `equivalence/equivalence.<run-id>.json`
- `reports/report.<run-id>.md`
- `reports/latest.md`
- `semantic-ledger.json`

The artifact names may vary if an existing naming convention is reused, but the semantic roles must remain distinguishable.

## Evaluation Axis

Every milestone must include:

```text
Executable Outcome
What can now execute that previously could not?

Durable Evidence
What record proves the semantics were exercised?

Evaluation Gate
How can HITL or automation determine it worked?

Irreversible Commitment
What semantic or architectural commitment is now embodied in software?
```

Because this slice touches the existing Roadmap state machine, the evaluation gate must also answer:

- Can an engineer identify the admitted transition from one authoritative semantic report?
- Can an engineer understand current state without reconstructing hidden context from helpers?
- Can the path be followed from admission through observation, validation, evidence, decision, equivalence, and report?
- Can report text, raw observation, evidence, decision, and state-store data be distinguished?
- Can the legacy `status` command remain available while the semantic wrapper proves equivalence?

## Phase 1: Child Subject Identity

### Goal

Create one child governed subject:

```text
RoadmapTransition:StatusReport
```

### Smallest Implementation

Load the existing `RepositoryWork` subject identity and create a child identity record for the status transition.

### Executable Outcome

The semantic wrapper can inspect the repository and report:

```text
parent RepositoryWork subject
  -> child RoadmapTransition subject
  -> transition key StatusReport
  -> report-only lifecycle grammar
  -> authority scopes
```

### Durable Evidence

- Child subject identity record.
- Parent-child relation record or field.
- Report-only lifecycle vocabulary for this child subject.
- Authority scope declarations.

### Evaluation Gate

HITL can verify the transition subject identity is stable and is not merely:

- `status`
- `RoadmapStateMachine.StatusAsync`
- a file path
- a process id
- a transient command invocation

### Irreversible Commitment

Roadmap transitions can become governed child subjects of `RepositoryWork` without turning the Roadmap state machine itself into the subject identity.

## Phase 2: Transition Intent and Source Snapshot

### Goal

Capture subject-bound intent and source context before admission.

### Smallest Implementation

Capture an intent for:

```text
Produce a report-only semantic view of the current roadmap state.
```

Capture the current roadmap state source snapshot, including absence as a valid source condition.

### Executable Outcome

The wrapper can materialize:

```text
intent
  -> current state source snapshot
  -> freshness marker
  -> source/artifact boundary report
```

### Durable Evidence

- Intent record.
- Current roadmap state source snapshot.
- Source hash or explicit missing-state marker.
- Freshness rule.
- Trust boundary.

### Evaluation Gate

HITL can answer:

- Which state source was inspected?
- Was the source present, missing, empty, stale, or malformed?
- Which hash or absence marker was captured?
- Why is the captured snapshot not automatically state authority?

### Irreversible Commitment

Current roadmap state is a source for this report-only transition. The captured representation is not itself the authoritative state.

## Phase 3: Report-Only Protocol Admission

### Goal

Admit or reject the status transition before legacy status execution runs.

### Smallest Implementation

Define one protocol:

```text
roadmap-transition.status-report.v1
```

Allowed exits:

```text
report-only
blocked
denied
unsupported
```

### Executable Outcome

An attempted semantic status transition is classified before execution:

```text
intent
  -> child subject
  -> parent RepositoryWork authority
  -> source freshness check
  -> report-only authority check
  -> invariant check
  -> admission outcome
```

### Durable Evidence

- Protocol definition.
- Admission record.
- Authority check result.
- Invariant evaluation result.
- Admission report.

### Evaluation Gate

HITL can attempt:

- a valid report-only status transition;
- an unsupported transition key;
- a request missing report authority;
- a request that tries to claim mutation authority from report output.

Each attempt must persist a distinct admission outcome.

### Irreversible Commitment

Existing CLI command availability does not grant governed transition authority. The transition is executable only through admitted subject-bound intent.

## Phase 4: Legacy Execution as Observation

### Goal

Execute existing status behavior through the semantic wrapper and treat the result as observation.

### Smallest Implementation

Reuse existing status-state loading and status rendering behavior. Capture the rendered output as raw observation before it is accepted as evidence.

### Executable Outcome

The wrapper executes:

```text
admitted report-only transition
  -> legacy status behavior
  -> raw status observation
  -> validation
  -> evidence binding or retained observation
```

### Durable Evidence

- Interaction record.
- Input snapshot.
- Raw observation artifact.
- Validation record.
- Evidence record when accepted.
- Non-authoritative observation record when rejected.

### Evaluation Gate

HITL can distinguish:

- admission result;
- input source snapshot;
- raw legacy status output;
- validator result;
- accepted evidence;
- semantic report.

### Irreversible Commitment

Legacy transition output is not evidence because it was printed. It becomes evidence only after validation binds it to subject, protocol, source snapshot, and consumer scope.

## Phase 5: Report-Only Decision and State Non-Mutation

### Goal

Persist a decision that the status transition is report-only and prove it did not mutate roadmap state.

### Smallest Implementation

Validate the observation and persist a decision:

```text
accepted choice: emit-report-only
authorized effect: report artifact only
```

### Executable Outcome

The wrapper can produce a report-only decision and verify no roadmap state mutation occurred.

### Durable Evidence

- Decision record.
- Evidence consumption relation.
- Pre/post state source hashes or absence markers.
- Report artifact.

### Evaluation Gate

HITL can verify:

- the accepted decision is distinct from raw output;
- the authorized effect is report-only;
- roadmap state did not change;
- no report field grants future execution authority.

### Irreversible Commitment

Report-only transitions can have accepted decisions, but those decisions authorize only report effects.

## Phase 6: Behavior Equivalence Record

### Goal

Prove the semantic wrapper preserves current status behavior.

### Smallest Implementation

Compare semantic-wrapper status observation with legacy status output for the same source snapshot.

The comparison does not need to be a byte-for-byte golden if the legacy output contains timestamps or formatting noise. It must compare the behaviorally meaningful fields:

- persisted roadmap state or no-state condition;
- startup plan reason when state exists;
- transition intent;
- blockers;
- terminal outcome classification.

### Executable Outcome

The wrapper persists:

```text
legacy status behavior
  -> semantic status behavior
  -> equivalence validator
  -> accepted or rejected equivalence record
```

### Durable Evidence

- Legacy status observation.
- Semantic status observation.
- Equivalence record.
- Equivalence report.

### Evaluation Gate

HITL can see whether the semantic wrapper preserved status behavior and, if not, which field diverged.

### Irreversible Commitment

A legacy Roadmap transition may be wrapped only when behavior equivalence is evidenced. Retirement remains a separate governed act.

## Phase 7: Semantic Transition Ledger

### Goal

Make repeated status-transition runs inspectable without turning the ledger into a general event store.

### Smallest Implementation

Append one run record to a transition-specific semantic ledger.

### Executable Outcome

The wrapper can load prior runs and report:

```text
latest admission
latest evidence
latest decision
latest equivalence status
latest report
```

### Durable Evidence

- Transition semantic ledger.
- Latest report pointer or artifact.
- Run-level artifact references.

### Evaluation Gate

HITL can inspect the latest status-transition semantic condition without reading implementation source.

### Irreversible Commitment

Transition history is preserved as semantic lineage. The ledger records governed runs; it is not a substitute for roadmap state.

## Implementation Acceptance Baseline

This plan is implemented when the system can execute:

```text
load RepositoryWork subject
  -> load or create RoadmapTransition:StatusReport child subject
  -> capture status intent
  -> capture current roadmap state source snapshot
  -> admit report-only transition protocol
  -> execute legacy status behavior through the wrapper
  -> capture raw observation
  -> validate and bind evidence
  -> persist report-only decision
  -> prove no roadmap state mutation
  -> compare behavior equivalence with legacy status behavior
  -> write semantic transition ledger
  -> emit latest semantic transition report
```

The legacy `status` command must still work independently after this slice.

## Explicit Non-Goals

- Do not retire the legacy `status` command.
- Do not wrap `run`, `unblock`, selection, promotion, execution, or completion in this slice.
- Do not create a general subject registry.
- Do not create a general protocol registry.
- Do not create a general event store.
- Do not migrate every Roadmap state.
- Do not rewrite `RoadmapStateMachine`.
- Do not treat report output as evidence without validation.
- Do not treat report-only admission as mutation authority.
- Do not treat behavior equivalence as permission to retire legacy behavior.

## Next Slice After This

After `RoadmapTransition:StatusReport` is wrapped and equivalence is evidenced, the next candidate should be one narrow mutating transition with contained effects. Preferred candidates:

1. `RoadmapTransition:CoreReadyInitialization`
2. `RoadmapTransition:SelectionReportOnlyPreparation`
3. `RoadmapTransition:UnblockReview`

Choose the next candidate only after this slice proves the wrapper can preserve human navigability and behavior equivalence.
