# Canonical Semantic Architecture Implementation Plan

## Purpose

This plan adapts `canonical-semantic-architecture-roadmap.md` into an actionable implementation sequence.

The roadmap answers:

```text
Which semantic truths must exist before other semantic truths can exist?
```

This plan answers:

```text
What is the smallest executable realization that proves each semantic truth now exists?
```

The implementation strategy is deliberately vertical. Do not begin by building complete registries for every primitive. Begin with one executable governed subject, prove the semantics through durable behavior and evaluation artifacts, then generalize only when the second use case would otherwise duplicate meaning.

## Inputs

- `semantic-constitution.md`
- `canonical-semantic-architecture-roadmap.md`
- Existing LoopRelay artifact and orchestration behavior
- Existing governance, projection, completion, roadmap, permission, and CLI test surfaces

## Implementation Posture

The implementation should embody the constitution without making the first milestone a grand semantic framework.

Use this order of force:

```text
Minimal executable interaction
  -> durable evidence that the semantics worked
  -> narrow model extraction
  -> second executable use
  -> generalization
```

Rules:

- Prefer one real governed subject before a subject registry.
- Prefer one real protocol before a protocol framework.
- Prefer one persisted interaction record before a general event store.
- Prefer one evidence-bound decision before a decision taxonomy.
- Prefer one candidate-to-promoted artifact path before an artifact ecosystem.
- Prefer one recoverable blocker before a recovery platform.
- Generalize only when repeated semantics would otherwise diverge.

## First Executable Subject

The initial subject is:

```text
RepositoryWork
```

This is the governed continuity of a repository's current LoopRelay work: intent, sources, artifacts, decisions, state, evidence, reports, blockers, completion claims, and recovery.

Why this subject:

- It is already central to LoopRelay behavior.
- It has existing artifacts, prompts, decisions, handoffs, projections, completion claims, and blockers.
- It can prove the constitution without inventing a synthetic demo domain.
- It is broad enough to exercise subject identity, lifecycle, authority, evidence, artifacts, decisions, state, recovery, certification, and distillation.
- It is narrow enough to avoid implementing every future subject class on day one.

The first implementation should not create a full subject registry. It should create one durable subject identity for RepositoryWork and use it until a second subject requires common registry behavior.

## Evaluation Axis

Every phase must produce an executable outcome:

```text
What can now execute that previously could not?
```

This is not the same as a test list. Tests verify the outcome, but the outcome is the new behavior itself.

Each phase must produce:

- Executable outcome: the smallest behavior now possible.
- Durable artifact: the record left behind by that behavior.
- Evaluation gate: how HITL or automation can tell the behavior is real.
- Irreversible commitment: the architectural choice that should not be undone without governance.

## Semantic Stability Commitments

These concepts are expected to remain stable for the lifetime of the project:

- Constitutional laws.
- Primitive ontology.
- Subject-bound intent as the start of governed action.
- Protocol admission as the only path from intent to governed interaction.
- Interaction model.
- Authority model.
- Identity model.
- Lifecycle grammar.
- Source vs artifact vs projection boundary.
- Observation vs evidence boundary.
- Evidence vs decision boundary.
- Candidate vs promoted artifact boundary.
- Protocol vs state machine boundary.
- Reporting vs execution boundary.
- Recovery as protocol over preserved evidence and intent.

These concepts are expected to evolve:

- Capability decomposition.
- Protocol inventory.
- Concrete subject classes.
- Artifact role families.
- Decision classes.
- Report surfaces.
- Projection families.
- Concrete state machines.
- Storage layout.
- CLI commands and UI surfaces.
- Code organization.
- Generated artifact mechanisms.
- Certification policies.

Changing stable concepts requires explicit constitutional governance. Changing evolving concepts requires ordinary architectural evidence that the new shape still preserves the stable commitments.

## Vertical Slice Strategy

The roadmap's vertical slices become implementation proof points. They should appear before broad abstraction work.

| Slice | Earliest executable realization | Durable artifact |
| --- | --- | --- |
| Source ingestion to governed artifact | Capture one source for `RepositoryWork` and write a provenance-bearing captured artifact | Source capture record plus artifact provenance |
| Protocol admission | Attempt one RepositoryWork operation and receive admitted, denied, blocked, or report-only outcome | Admission record |
| Governed interaction | Execute one admitted read-only interaction and persist observation plus validation result | Interaction record plus evidence record |
| Candidate artifact promotion | Generate one candidate artifact and promote it only after validation | Candidate artifact, promotion record, authoritative artifact version |
| Decision route from evidence | Parse one route suggestion and persist it only after evidence validation | Decision record plus effect record |
| Report-only projection | Produce one current semantic report that cannot mutate state | Report artifact |
| Blocker and recovery | Create one blocker with preserved evidence, then recover or retain it by protocol | Blocker record plus recovery review |
| Completion certification | Certify or reject one completion claim from evidence | Certification record |
| History distillation | Distill one historical evidence set into current understanding while preserving lineage | Distilled artifact plus lineage relation |

## Phase 1: Single Subject Kernel

### Goal

Make one real governed subject executable: `RepositoryWork`.

### Smallest Implementation

Introduce a narrow subject identity record for the current repository's work. It should be enough to answer:

- What subject is being governed?
- What stable identity persists across artifact versions and interactions?
- Who owns mutation and acceptance authority?
- What lifecycle grammar applies?
- Which invariants constrain this subject?

Do not implement a generic subject registry yet.

### Executable Outcome

A command or host operation can inspect the current repository and produce a `RepositoryWork` semantic identity report.

The behavior is:

```text
load repository
  -> create or load RepositoryWork subject identity
  -> evaluate initial lifecycle and authority facts
  -> emit report-only semantic summary
```

### Durable Artifacts

- `RepositoryWork` subject identity record.
- Initial lifecycle record.
- Initial owner or ownerless rule.
- Initial invariant set.
- Report-only semantic summary.

Exact storage may use the existing repository artifact area, but the stored shape must not make file location the subject identity.

### Implementation Steps

1. Add a minimal `RepositoryWork` subject model.
2. Assign stable identity from repository identity plus semantic subject type, not from a transient process or file path.
3. Add lifecycle vocabulary for the single subject only.
4. Add owner and authority placeholders that distinguish permission from acceptance authority.
5. Add absolute invariants needed by the constitution:
   - no subjectless interaction;
   - no intentless interaction;
   - no authority without scope;
   - no report field creates execution authority.
6. Add a report-only inspection operation.

### Evaluation Gate

HITL can run the inspection behavior and see:

- subject identity;
- owner or ownerless rule;
- lifecycle state;
- authority scopes;
- active invariant declarations;
- statement that the report is non-mutating.

### Irreversible Commitment

`RepositoryWork` identity is semantic continuity, not a path, process id, prompt name, CLI command, or current implementation type.

### Do Not Do Yet

- Do not build a full subject registry.
- Do not model every future subject.
- Do not migrate all existing state machines.
- Do not implement recovery, certification, or artifact promotion.

## Phase 2: Intent and Source Capture

### Goal

Make work purpose and source origin executable before protocol admission.

### Smallest Implementation

Capture one subject-bound intent and one source snapshot for `RepositoryWork`.

Use the first source that already matters to current behavior, such as a plan, constitution, roadmap, handoff, decision file, or repository status snapshot. The source must retain origin, version or hash, freshness, trust boundary, and consumer scope.

### Executable Outcome

A host operation can capture a source for `RepositoryWork` and produce an artifact representation without making that artifact authoritative by storage alone.

The behavior is:

```text
capture intent
  -> capture source snapshot
  -> materialize captured representation
  -> record provenance from source to artifact
  -> report freshness and trust boundary
```

### Durable Artifacts

- Intent record.
- Source capture record.
- Captured artifact representation.
- Provenance relation.
- Freshness marker.

### Implementation Steps

1. Add a minimal intent record for one operation.
2. Add source capture metadata:
   - origin;
   - subject;
   - capture method;
   - version, hash, or snapshot identity;
   - freshness rule;
   - trust boundary;
   - consumer scope.
3. Store captured representation as an artifact with role `captured-source-view`.
4. Record a provenance relation from source to captured artifact.
5. Add a report that explicitly says the captured artifact is not source authority.

### Evaluation Gate

HITL can inspect the captured source and answer:

- Why was this captured?
- What subject does it concern?
- Where did it originate?
- Which version or snapshot was captured?
- What freshness rule applies?
- Which artifact represents it?
- Why is that artifact not automatically authority?

### Irreversible Commitment

Source authority and artifact authority are separate. Ingestion preserves origin; it does not merge source and artifact identity.

### Do Not Do Yet

- Do not infer decisions from captured source content.
- Do not promote captured artifacts.
- Do not generate broad projections.
- Do not create source-specific special cases that skip provenance.

## Phase 3: Protocol Admission

### Goal

Make protocol admission the only executable path from intent to governed work.

### Smallest Implementation

Define one protocol for `RepositoryWork`, then route one operation through it.

The first protocol should be intentionally narrow. A good candidate is a report-only or source-capture protocol because it can prove admission without requiring mutation authority.

### Executable Outcome

An attempted operation is classified as:

```text
admitted
denied
blocked
report-only
unsupported
```

before the operation executes.

The behavior is:

```text
intent
  -> RepositoryWork subject
  -> protocol lookup
  -> source freshness check
  -> authority scope check
  -> invariant check
  -> admission outcome
```

### Durable Artifacts

- Protocol definition for the first `RepositoryWork` protocol.
- Admission record.
- Authority check result.
- Invariant evaluation result.
- Report-only denial or admission report.

### Implementation Steps

1. Define one protocol contract in code for one executable operation.
2. Declare:
   - protocol owner;
   - subject class;
   - accepted intent shape;
   - required inputs;
   - source freshness requirements;
   - authority scope;
   - invariants;
   - allowed exits.
3. Add admission evaluation before any operation body runs.
4. Persist admission outcome.
5. Prevent direct execution of that operation without admission.

### Evaluation Gate

HITL can attempt a valid operation and an invalid operation and see:

- which protocol was considered;
- why admission succeeded or failed;
- which authority scope was checked;
- which invariants ran;
- whether the outcome was active, denied, blocked, report-only, or unsupported.

### Irreversible Commitment

No governed work executes because a command can run. It executes only because a protocol admitted subject-bound intent under scoped authority.

### Do Not Do Yet

- Do not build the complete protocol registry.
- Do not rewrite every workflow into protocols.
- Do not collapse protocol admission into state checks.
- Do not treat report-only output as authority.

## Phase 4: Interaction and Evidence Lineage

### Goal

Make one admitted operation leave durable interaction and evidence lineage.

### Smallest Implementation

Wrap the first admitted protocol operation in an interaction envelope.

The operation may remain read-only or report-only. The key implementation proof is that raw observation is captured, validated, bound, and persisted as evidence only when accepted.

### Executable Outcome

The system executes one admitted interaction and persists:

```text
interaction
  -> observation
  -> validation
  -> evidence binding
  -> outcome
  -> report
```

### Durable Artifacts

- Interaction record.
- Input snapshot.
- Observation record.
- Evidence record.
- Outcome record.
- Report artifact.

### Implementation Steps

1. Add an interaction envelope for the first protocol.
2. Capture input snapshots before execution.
3. Capture raw observation after execution.
4. Add one validator that can accept or reject the observation.
5. Bind accepted observation as evidence for a concrete subject and consumer scope.
6. Persist outcome and report.
7. Preserve failed validation as non-authoritative observation plus report.

### Evaluation Gate

HITL can inspect an executed operation and identify:

- admitted intent;
- subject;
- protocol;
- authority scope;
- input snapshot;
- raw observation;
- validation result;
- evidence binding;
- outcome;
- report.

### Irreversible Commitment

Raw output, parser output, model output, command output, and report text are observations until validated and bound as evidence.

### Do Not Do Yet

- Do not use evidence records as a general logging system.
- Do not let memory replace protocol owners.
- Do not treat failed validation as success with warnings.
- Do not route decisions from unbound observations.

## Phase 5: Candidate Artifact and Promotion

### Goal

Make generated or prepared artifacts non-authoritative until promoted by protocol authority.

### Smallest Implementation

Choose one artifact already produced by the system and turn it into a candidate artifact before it can become current.

A good first candidate is a generated projection, report, planning artifact, or captured source view. The artifact should be small enough that validation and promotion are straightforward.

### Executable Outcome

The system can generate one candidate artifact, validate it, and either promote it or retain it as non-authoritative.

The behavior is:

```text
admitted interaction
  -> candidate artifact
  -> artifact validation
  -> promotion decision or rejection
  -> authoritative artifact version
  -> supersession/provenance record
```

### Durable Artifacts

- Candidate artifact.
- Artifact validation record.
- Promotion record.
- Authoritative artifact version when accepted.
- Supersession relation when a prior current artifact exists.
- Provenance relation to interaction and sources.

### Implementation Steps

1. Add artifact role metadata for one artifact type:
   - subject;
   - owner;
   - role;
   - representation key;
   - version;
   - provenance;
   - lifecycle;
   - mutation authority.
2. Write new output as candidate.
3. Validate identity, role, shape, source provenance, and freshness.
4. Promote only through accepted authority.
5. Record supersession without deleting history.
6. Report current artifact status without granting mutation authority to consumers.

### Evaluation Gate

HITL can inspect the artifact lifecycle and see:

- candidate creation;
- validation;
- accepted or rejected promotion;
- authority transfer;
- current version;
- prior version retained as history;
- provenance and consumers.

### Irreversible Commitment

Candidate artifacts cannot replace authoritative artifacts by being written. Promotion is authority transfer, not filesystem movement.

### Do Not Do Yet

- Do not migrate every artifact family.
- Do not generate a broad artifact platform.
- Do not let artifact consumers gain mutation authority.
- Do not delete history as cleanup.

## Phase 6: Evidence-Bound Decisions

### Goal

Make one accepted decision drive one effect.

### Smallest Implementation

Pick one narrow decision that already exists implicitly, such as route selection, promotion acceptance, report-only classification, blocked classification, or next-action selection.

Persist it only after evidence validation.

### Executable Outcome

The system can produce a suggestion, validate evidence, persist an accepted decision, and execute the authorized effect.

The behavior is:

```text
evidence
  -> alternatives
  -> suggested choice
  -> validator
  -> accepted decision
  -> authorized effect
```

### Durable Artifacts

- Decision record.
- Evidence consumption relation.
- Validator result.
- Effect record.
- Supersession or retirement record if the decision replaces a prior one.

### Implementation Steps

1. Define one decision class for one protocol.
2. Declare allowed alternatives.
3. Capture proposed choice as suggestion.
4. Validate evidence, subject identity, lifecycle legality, and protocol ownership.
5. Persist accepted decision.
6. Apply exactly one authorized effect:
   - route;
   - state entry;
   - artifact promotion;
   - lifecycle advancement;
   - blocker retention;
   - report-only classification.
7. Report the effect and decision lineage.

### Evaluation Gate

HITL can distinguish:

- raw suggestion;
- accepted decision;
- evidence consumed;
- validator;
- decision authority;
- authorized effect.

### Irreversible Commitment

Suggestions, rationale, parser classifications, generated text, and human comments are not decisions until a protocol validates and persists the choice.

### Do Not Do Yet

- Do not implement every decision class.
- Do not let decision persistence happen without evidence.
- Do not encode decisions as report text.
- Do not give human exception text unscoped authority.

## Phase 7: Current State Agreement

### Goal

Make state an executable control summary that agrees with lifecycle, evidence, decisions, artifacts, authority, relations, and freshness.

### Smallest Implementation

Persist one state entry for `RepositoryWork` only after an authorized interaction effect.

The state should be enough to control one next interaction, such as allowing a report, denying mutation, allowing promotion, or marking a blocker.

### Executable Outcome

The system can load current memory, reconstruct why the current state is valid, and use that state to admit or deny the next operation.

The behavior is:

```text
load lineage
  -> derive current lifecycle/artifact/evidence/decision agreement
  -> validate state entry evidence
  -> report current state
  -> admit or deny next protocol interaction
```

### Durable Artifacts

- State entry record.
- State entry evidence relation.
- Lifecycle synchronization record.
- Current semantic summary.
- Admission decision using current state.

### Implementation Steps

1. Add one state vocabulary for `RepositoryWork`.
2. Persist state only through authorized interaction effect.
3. Attach entry evidence.
4. Validate lifecycle agreement before state becomes current.
5. Add command admission dependency on current state for one operation.
6. Report current state as a summary, not authority creation.

### Evaluation Gate

HITL can inspect current state and see:

- state name;
- subject;
- owner;
- entry interaction;
- entry evidence;
- lifecycle agreement;
- supporting artifact and decision records;
- allowed next interactions;
- report-only or active classification.

### Irreversible Commitment

State is a named control condition grounded in evidence and lifecycle agreement. It is not the whole truth and cannot advance without interaction authority.

### Do Not Do Yet

- Do not rewrite all existing state machines.
- Do not treat cached state as semantic freshness.
- Do not allow reports to advance state.
- Do not let state contradict artifact or lifecycle records.

## Phase 8: Blocker and Recovery

### Goal

Make one blocked or failed condition recoverable only through preserved evidence and protocol authority.

### Smallest Implementation

Create one recoverable blocker path.

The blocker may come from stale source, missing required artifact, validation failure, denied authority, or failed admission. Recovery must either restore a supported target state or retain the blocker.

### Executable Outcome

The system can:

```text
produce blocker evidence
  -> preserve original intent
  -> request recovery
  -> evaluate eligibility
  -> validate repaired input
  -> restore supported target or retain blocker
```

### Durable Artifacts

- Blocker record.
- Blocker evidence relation.
- Preserved original intent.
- Recovery intent.
- Recovery review.
- Target-state decision or retained-blocker decision.
- Recovery report.

### Implementation Steps

1. Define one blocker type.
2. Ensure blocker creation preserves original evidence and intent.
3. Define one recovery protocol.
4. Add eligibility check.
5. Add repaired-input validation.
6. Restore only a target state already supported by evidence and lifecycle rules.
7. Retain blocker when recovery is not supported.
8. Report recovery outcome without erasing original blocker.

### Evaluation Gate

HITL can inspect recovery and see:

- original condition;
- original intent;
- blocker evidence;
- recovery intent;
- eligibility;
- repair input;
- validation;
- target or retained blocker;
- preserved lineage.

### Irreversible Commitment

Recovery is not retry. Recovery cannot invent a target state, erase blocker evidence, or reinterpret unsupported failure as success.

### Do Not Do Yet

- Do not build every recovery flow.
- Do not auto-retry as recovery.
- Do not delete blocker evidence after repair.
- Do not recover without explicit eligibility semantics.

## Phase 9: Certification and Distillation

### Goal

Make completion and current understanding authoritative through executable certification and distillation.

### Smallest Implementation

Certify one completion claim for `RepositoryWork` or a child work artifact, then distill one durable insight into current understanding with lineage.

### Executable Outcome

The system can:

```text
completion claim
  -> evidence collection
  -> independent evaluation
  -> policy validation
  -> certification decision
  -> lifecycle update
  -> report
```

Then:

```text
history/evidence
  -> durable relevance review
  -> validated placement
  -> current understanding update
  -> preserved lineage
```

### Durable Artifacts

- Completion claim.
- Certification evidence bundle.
- Certification decision.
- Lifecycle movement record.
- Certification report.
- Distilled current-understanding artifact.
- Supersession and provenance relations.

### Implementation Steps

1. Add one completion claim shape.
2. Collect evidence already produced by prior phases.
3. Add one independent evaluation path.
4. Validate policy before accepting completion.
5. Persist certification decision.
6. Move lifecycle only after certification acceptance.
7. Add one distillation protocol over certified or historical evidence.
8. Update current understanding as an artifact with lineage, not history rewrite.

### Evaluation Gate

HITL can inspect certified completion and distilled understanding and see:

- claim;
- evidence;
- evaluator;
- policy;
- decision;
- lifecycle movement;
- current artifact update;
- superseded version;
- preserved history.

### Irreversible Commitment

Completion claims do not close work. Distillation updates current understanding without rewriting history.

### Do Not Do Yet

- Do not make report text certification evidence unless bound as evidence.
- Do not auto-close from a success status.
- Do not distill without provenance.
- Do not treat archive movement as erasure.

## Phase 10: Capability Admission

### Goal

Make future capabilities enter through declared constitutional contracts rather than new hidden foundations.

### Smallest Implementation

Declare one existing capability using the constitutional admission form. Do not start with a hypothetical future capability.

Candidate first capability:

```text
RepositoryWork Semantic Execution
```

It should declare owned subjects, accepted intents, protocols, authority, artifacts, decisions, evidence, reports, invariants, lifecycle, recovery, certification, and retirement.

### Executable Outcome

The system can evaluate a capability declaration and return:

```text
accepted
rejected
blocked-for-missing-semantics
report-only
```

The evaluation should not grant runtime authority by itself.

### Durable Artifacts

- Capability declaration.
- Capability conformance report.
- Missing-semantics findings, if any.
- Capability acceptance or rejection decision.
- Retirement or supersession rule.

### Implementation Steps

1. Define capability declaration schema from the constitution's admission questions.
2. Fill it for one existing capability.
3. Evaluate required fields:
   - subjects;
   - intents;
   - protocols;
   - authority;
   - sources and observations;
   - evidence;
   - artifacts;
   - decisions;
   - relation types;
   - states;
   - lifecycle movements;
   - invariants;
   - reports;
   - recovery;
   - replay;
   - retirement.
4. Persist conformance report.
5. Persist acceptance or rejection decision.
6. Use missing semantics as implementation input for later capability migration.

### Evaluation Gate

HITL can review the capability declaration and determine whether it belongs inside the governed system without reading implementation source.

### Irreversible Commitment

Capabilities are ownership and authority boundaries over constitutional primitives. They are not namespaces, projects, folders, screens, or service collections.

### Do Not Do Yet

- Do not create plugin infrastructure.
- Do not admit capabilities that lack recovery semantics.
- Do not add new primitives for a capability.
- Do not let capability conformance reports execute work.

## Cross-Phase Evaluation Requirements

Every implementation milestone derived from this plan must include these four fields:

```text
Executable Outcome
What can now execute that previously could not?

Durable Evidence
What record proves the semantics were exercised?

Evaluation Gate
How can HITL or automation determine it worked?

Irreversible Commitment
What semantic/architectural commitment is now embodied in software?
```

Milestones should not be accepted if they only add models, interfaces, or documentation without executable behavior.

## Implementation Acceptance Baseline

The plan is considered implemented when the system can execute this end-to-end semantic path for `RepositoryWork`:

```text
load RepositoryWork subject
  -> capture intent
  -> capture source
  -> admit protocol
  -> execute interaction
  -> capture observation
  -> validate and bind evidence
  -> produce candidate artifact
  -> validate and promote artifact
  -> persist accepted decision
  -> apply authorized state/lifecycle effect
  -> report current semantic condition
  -> create and recover or retain blocker
  -> certify completion claim
  -> distill history into current understanding
  -> evaluate capability declaration
```

The first pass may use narrow implementations for each step. The important property is not breadth. The important property is that every semantic boundary is executable, evidenced, and resistant to accidental authority.

## Explicit Non-Goals

- Do not implement a complete semantic framework before the first vertical slice.
- Do not refactor the repository around new namespaces as a substitute for semantic behavior.
- Do not migrate every artifact family before one promotion path works.
- Do not migrate every state machine before one state summary is evidence-grounded.
- Do not create speculative plugin, registry, or schema infrastructure.
- Do not treat generated text, parser output, report fields, file writes, command success, or UI display as acceptance authority.

## Expected Evolution

The first implementation should feel small:

```text
one subject
one intent
one source
one protocol
one interaction
one evidence record
one candidate artifact
one decision
one state effect
one blocker/recovery
one certification
one distillation
one capability declaration
```

After that path works, the architecture can expand by repetition:

- Add more subject classes.
- Add a real subject registry.
- Add more protocols.
- Add more artifact roles.
- Add more decision classes.
- Add more recovery protocols.
- Add more certification policies.
- Add more report surfaces.
- Add more capability declarations.

The expansion should be mechanical because the constitutional boundaries have already been proven by executable behavior.
