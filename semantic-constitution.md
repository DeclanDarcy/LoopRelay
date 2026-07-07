# Semantic Constitution

## Recover the Semantic Constitution

This document recovers the semantic constitution that governs the recovered LoopRelay architecture. It is derived from the prior audit chain:

- `state-machine-refactor-audit.md`: implementation and current behavior evidence.
- `secondary-state-machine-audit.md`: canonical machine evidence.
- `third-state-machine-audit.md`: canonical architecture evidence.
- `audit.md`: preservation and future authority evidence.

It does not redesign the system. It does not prescribe implementation, repository layout, framework, language, class names, or process topology. It defines the semantic reality that must remain true when those things change.

The recovered architecture contains a strategic workflow, durable repository memory, prompt-backed and evidence-backed work, artifact promotion, completion certification, and recovery. The smallest semantic model that can express all of it is not a list of current components. It is a set of primitives, contracts, laws, and relationships.

## Deliverable 1: Semantic Ontology

The semantic primitives are:

1. Machine
2. Source
3. Capability
4. Protocol
5. Interaction
6. State
7. Transition
8. Decision
9. Evidence
10. Artifact
11. Projection
12. Authority
13. Identity
14. Lifecycle
15. Invariant
16. Recovery

State machines, promotions, materializations, certifications, ledgers, reports, and handoffs are not additional primitives. They are compositions of these primitives.

### Machine

- Meaning: The bounded semantic universe in which capabilities operate, state advances, artifacts evolve, evidence is preserved, and authority is exercised.
- Purpose: To coordinate work from intent through execution, decision, durable memory, reporting, and recovery.
- Identity: A machine is identified by its governed repository or domain boundary plus its durable memory.
- Lifecycle: Absent, initialized, active, paused, blocked, cancelled, failed, completed, resumed, or retired.
- Authority: Run Control grants or withholds machine-level execution authority for each command-facing interaction.
- Relationships: Contains capabilities, protocols, states, transitions, artifacts, evidence, decisions, lifecycles, invariants, and recovery paths.
- Constraints: The machine does not treat reporting as execution authority, does not advance without authorized transition semantics, and does not hide durable facts in process memory.

### Source

- Meaning: A fact-bearing input that exists outside the immediate interaction and constrains what the machine may validly know or do.
- Purpose: To anchor machine work in project context, roadmap intent, repository state, execution evidence, human intent, or other authoritative input.
- Identity: A source is identified by its stable origin, scope, and captured version or snapshot.
- Lifecycle: Observed, captured, projected, consumed, superseded, unavailable, or retired.
- Authority: Source authority comes from its origin. The machine may consume it, but cannot silently transform it into internal authority without validation.
- Relationships: Sources feed projections, evidence, decisions, artifact updates, and recovery reviews.
- Constraints: A source is not a projection. A stale or unvalidated source cannot authorize transition advancement.

### Capability

- Meaning: A bounded semantic power that owns a purpose, protocols, state subset, artifacts, decisions, evidence, invariants, and handoff rules.
- Purpose: To separate authority by responsibility instead of letting one coordinator own every meaning.
- Identity: A capability is identified by the purpose it owns and the authority it can exercise.
- Lifecycle: Defined, active, paused, delegated, superseded, or retired.
- Authority: A capability has authority only inside its owned protocols and owned subjects.
- Relationships: Capabilities collaborate by handing off artifacts, evidence, decisions, and states.
- Constraints: A capability may consume another capability's artifacts, but cannot mutate or reinterpret their authority unless authority is explicitly transferred.

### Protocol

- Meaning: A governed sequence of interactions with entry rules, participants, evidence requirements, success, failure, exit, and recovery semantics.
- Purpose: To define how a capability safely performs work.
- Identity: A protocol is identified by owner, purpose, entry condition, subject class, and allowed outcomes.
- Lifecycle: Proposed, active, invoked, completed, blocked, superseded, or retired.
- Authority: Protocol authority is created by the owning capability and constrained by machine-level authority and invariants.
- Relationships: Protocols contain interactions and can be projected as state machines.
- Constraints: A protocol is not just a state graph. It includes evidence, artifact, decision, authority, reporting, and recovery requirements.

### Interaction

- Meaning: The fundamental unit of work.
- Purpose: To consume intent and inputs, exercise scoped authority, produce observations, validate outcomes, and record what happened.
- Identity: An interaction instance is identified by protocol, trigger, subject, input snapshot, and correlation lineage.
- Lifecycle: Intended, authorized, prepared, executed, interpreted, validated, recorded, reported, and possibly recovered.
- Authority: An interaction inherits authority from machine control, capability ownership, protocol rules, and the current subject state.
- Relationships: Interactions may produce decisions, transitions, evidence, artifacts, projections, reports, or blockers.
- Constraints: Execution output alone does not complete an interaction. Completion requires interpretation, validation, and recording according to the protocol.

### State

- Meaning: A durable or conceptual position that constrains what interactions may happen next.
- Purpose: To summarize readiness, pause, block, cancellation, failure, completion, or pending branch conditions.
- Identity: A state is identified by machine, owning protocol or capability, subject, and entry evidence.
- Lifecycle: Entered, validated, active or report-only, exited, superseded, or terminal.
- Authority: State constrains interaction eligibility, but state alone is not sufficient authority unless its entry evidence and lifecycle agreement still hold.
- Relationships: States are entered by transitions, checked by invariants, summarized in memory, and interpreted by control and recovery.
- Constraints: State is not the same as artifact lifecycle. They must agree where readiness or completion overlaps.

### Transition

- Meaning: The state effect of an authorized interaction.
- Purpose: To move, confirm, block, cancel, fail, complete, or recover a state under a governed contract.
- Identity: A transition is identified by owner, source state or state class, trigger, subject, and target semantics.
- Lifecycle: Planned, prepared, started, output-produced, interpreted, validated, materialized or routed, persisted, reported, and recovered if needed.
- Authority: Transition authority belongs to the owning capability and is admitted by Run Control through readiness validation.
- Relationships: Transitions consume state, artifacts, evidence, decisions, projections, and recovery intent; they produce target state, durable history, and reports.
- Constraints: A transition is not complete merely because an external action or prompt returned output. It is complete only after required validation, materialization, evidence, persistence, and reporting.

### Decision

- Meaning: An authoritative choice among allowed alternatives.
- Purpose: To determine branch, route, artifact acceptance, certification, recovery target, or lifecycle change.
- Identity: A decision is identified by subject, choice set, evidence consumed, validator, authority source, and persistence record.
- Lifecycle: Proposed, parsed or classified, validated, persisted, consumed, superseded, retired, replayed, or archived.
- Authority: Decision authority comes from validated evidence under a protocol and from the capability allowed to make that class of decision.
- Relationships: Decisions consume evidence and artifacts, affect transitions and lifecycles, and are recorded by machine memory.
- Constraints: Suggestions, report text, next-action hints, and model rationale are not decisions until validated and persisted by the authorized protocol.

### Evidence

- Meaning: Validated proof that an observation, transition, decision, blocker, certification, or recovery event occurred.
- Purpose: To support authority, replay, validation, reporting, recovery, and lineage.
- Identity: Evidence is identified by observed source, capture event, validation result, binding, hash or freshness marker, and consumer scope.
- Lifecycle: Observed, captured, validated, bound, promoted to authority or retained as blocker, consumed, replayed, archived, superseded, or retired.
- Authority: Evidence becomes authority only after validation and binding to a decision, transition, blocker, or recovery target.
- Relationships: Evidence is produced by interactions and consumed by decisions, invariants, certification, recovery, reporting, and future projections.
- Constraints: Raw observation is not evidence authority. Evidence must not silently mutate the artifact it explains.

### Artifact

- Meaning: Durable information used or produced by the machine.
- Purpose: To carry current understanding, plans, active subjects, prepared work, decisions, evidence, manifests, projections, reports, and archives.
- Identity: An artifact is identified by owner, semantic role, subject, stable path or storage key, version, and lineage.
- Lifecycle: Absent, draft, candidate, validated, promoted, ready, consumed, executing, completed, blocked, superseded, archived, deleted, or retained as history.
- Authority: Artifact authority belongs to its owner and may be transferred through promotion, certification, or explicit handoff.
- Relationships: Artifacts can contain or reference evidence, decisions, projections, state summaries, and lifecycle records.
- Constraints: Artifacts represent current understanding or durable history, not unmediated truth. Their authority depends on validation, provenance, and lifecycle.

### Projection

- Meaning: A purpose-specific view of sources or artifacts prepared for an interaction.
- Purpose: To make required context explicit, bounded, fresh, and replayable before work proceeds.
- Identity: A projection is identified by source set, purpose, protocol, generated version, provenance, and freshness criteria.
- Lifecycle: Needed, generated, validated, consumed, stale, regenerated, or retired.
- Authority: A projection has preparation authority, not source authority. It can enable a transition only while fresh and valid.
- Relationships: Projections feed interactions, decisions, transition input snapshots, reporting, and recovery review.
- Constraints: A projection must never replace the sources it summarizes or conceal freshness loss.

### Authority

- Meaning: The right to decide, validate, mutate, promote, certify, recover, or report within a defined scope.
- Purpose: To prevent decisions, artifact mutation, transition routing, and recovery from becoming ambiguous.
- Identity: Authority is identified by issuer, bearer, scope, subject, validator, time or version, and retirement condition.
- Lifecycle: Created, delegated, exercised, recorded, transferred, constrained, superseded, revoked, or retired.
- Authority: Authority is self-describing only when recorded. Unrecorded authority is not replayable.
- Relationships: Authority governs every primitive and is consumed by interactions, decisions, transitions, artifact mutations, and recovery.
- Constraints: Permission to perform an action is not the same as authority to accept the result.

### Identity

- Meaning: The continuity of a semantic subject across versions, snapshots, projections, observations, and lifecycle changes.
- Purpose: To let the machine know what remains the same while information evolves.
- Identity: Identity is itself defined by stable subject key, owner, lineage root, and allowed equivalence rules.
- Lifecycle: Created, observed, instantiated, versioned, projected, superseded, merged only by authority, retired, or archived.
- Authority: Identity creation and mutation belong to the subject owner or a protocol explicitly authorized to transfer or retire identity.
- Relationships: Identity binds artifacts, evidence, decisions, states, transitions, projections, and lifecycle entries.
- Constraints: Version changes do not change identity. Snapshot identity is not subject identity. Projection identity is derived, never primary.

### Lifecycle

- Meaning: The allowed evolution of a subject.
- Purpose: To define creation, readiness, consumption, completion, block, recovery, supersession, archival, and retirement.
- Identity: A lifecycle is identified by subject, owner, state vocabulary, transition rules, and evidence requirements.
- Lifecycle: Defined, active, synchronized with state, advanced, blocked, completed, superseded, or retired.
- Authority: Lifecycle advancement belongs to the owner or to a protocol with delegated lifecycle authority.
- Relationships: Lifecycles apply to machines, capabilities, protocols, interactions, transitions, artifacts, evidence, decisions, projections, invariants, and recovery reviews.
- Constraints: Lifecycles compose but do not collapse. Workflow state and artifact lifecycle may overlap, but neither erases the other.

### Invariant

- Meaning: A rule that must hold for a subject, relationship, lifecycle, interaction, transition, decision, artifact, evidence record, or recovery path.
- Purpose: To prevent unsafe advancement and preserve semantic continuity.
- Identity: An invariant is identified by scope, assertion, authority, evaluation point, violation semantics, and recovery semantics.
- Lifecycle: Declared, active, evaluated, violated, satisfied, weakened by governance, superseded, or retired.
- Authority: Invariant authority comes from the owning protocol, capability, machine law, or explicit governance decision.
- Relationships: Invariants constrain every primitive and can block, fail, pause, or route recovery.
- Constraints: An invariant failure is not automatically recoverable. Recoverability is part of the invariant's contract.

### Recovery

- Meaning: The evidence-bound process for returning from blocked, failed, cancelled, stale, or invalid conditions to a safe semantic position.
- Purpose: To preserve lineage while allowing work to resume only when a supported repair is proven.
- Identity: Recovery is identified by blocker, transition intent, evidence set, review interaction, target semantics, and outcome record.
- Lifecycle: Entered, evidence captured, intent recorded, review requested, repair validated, target restored, blocker retained, superseded, or retired.
- Authority: Recovery authority belongs to the recovery protocol, not to the failed transition alone.
- Relationships: Recovery consumes blockers, evidence, state, lifecycle, decisions, source snapshots, and repaired artifacts; it produces target state or retained block.
- Constraints: Recovery cannot invent a target, erase original evidence, or treat retry as repair unless the protocol authorizes it.

## Deliverable 2: Semantic Hierarchy

The hierarchy is not a simple vertical chain from state to transition. The recovered architecture shows a containment hierarchy crossed by authority, identity, lifecycle, and invariants.

```text
Machine
  Source boundary
  Machine memory
  Capability
    Protocol
      Interaction
        Observation/input
        Interpretation
        Validation
        Decision
        Transition effect
        Artifact/evidence/projection effect
        Report
        Recovery intent when needed
    State set
    Artifact ownership
    Decision ownership
    Evidence ownership
    Invariant set
  Reporting and recovery interface

Cross-cutting over every level:
  Authority
  Identity
  Lifecycle
  Invariant
  Time and sequence
```

State machines are projections of protocol behavior over states and transitions. They are useful views, not the root ontology. The semantic root is Machine; the operational root is Capability; the unit of work is Interaction; the state effect is Transition.

## Deliverable 3: Universal Semantic Contracts

### Machine Contract

Every machine has a boundary, identity, durable memory, capability registry, current state summary, authority map, invariant set, reporting surface, and recovery surface.

### Source Contract

Every source has an origin, subject, capture method, version or snapshot, freshness rule, trust boundary, and consumer scope.

### Capability Contract

Every capability has purpose, owned protocols, owned state subjects, artifact ownership, decision classes, evidence responsibilities, invariants, handoff contracts, and recovery obligations.

### Protocol Contract

Every protocol has owner, participants, entry conditions, allowed interactions, input requirements, evidence requirements, artifact effects, decision authority, success exits, failure exits, reporting semantics, and recovery semantics.

### Interaction Contract

Every interaction has intent, subject, trigger, authority, inputs, readiness checks, execution form, observations, interpretation rules, validation rules, optional mutation, evidence recording, outcome, report, and recovery intent where failure is recoverable.

### State Contract

Every state has owner, subject, entry evidence, allowed interactions, readiness meaning, report-only or active classification, exit conditions, lifecycle relationship, blocker semantics, and recovery relationship.

### Transition Contract

Every transition has identity, owner, source, trigger, requirements, consumed information, prepared inputs, execution, interpretation, validation, produced information, mutations, persistence, report, target, failure semantics, and recovery semantics.

### Decision Contract

Every decision has subject, alternatives, producer, authority source, evidence consumed, validator, result, persistence, consumers, replay rules, supersession rules, and retirement rules.

### Evidence Contract

Every evidence record has observation source, capture event, validator, binding, authority level, hash or freshness marker, consumers, retention policy, replay semantics, and retirement or archival semantics.

### Artifact Contract

Every artifact has owner, identity, semantic role, representation, validity rules, version, provenance, lifecycle, mutation authority, consumers, reference rules, supersession, archival, and deletion semantics.

### Projection Contract

Every projection has purpose, source set, source versions, owner, generation protocol, freshness rule, validation rule, consumer interactions, invalidation rule, and retention policy.

### Authority Contract

Every authority grant has issuer, bearer, subject, scope, allowed actions, validators, evidence basis, transfer rules, consumption rules, expiry, revocation, and retirement.

### Identity Contract

Every identity has stable subject key, owner, lineage root, version relation, instance relation, snapshot relation, projection relation, equivalence rules, and retirement rule.

### Lifecycle Contract

Every lifecycle has subject, owner, allowed phases, allowed advancements, entry evidence, exit evidence, synchronization rules, blocking rules, completion rules, supersession rules, and archival rules.

### Invariant Contract

Every invariant has assertion, scope, owner, evaluation points, evidence inputs, violation semantics, recovery semantics, severity, lifetime, and governance rule for change.

### Recovery Contract

Every recovery has blocked subject, original intent, evidence set, eligibility rule, reviewer, repaired input rule, validation rule, target state rule, fallback rule, report, and lineage preservation.

## Deliverable 4: Semantic Laws

1. A machine can act only inside its declared boundary.
2. Every active interaction must be authorized before execution.
3. Every state advance must occur through an authorized transition.
4. Reporting describes authority; it does not create authority.
5. Permission to run a command or mutate storage is not acceptance authority for the result.
6. Source information cannot become machine authority without capture, validation, and binding.
7. A projection is never source authority.
8. Prompt output, external output, or generated text is observation until interpreted and validated.
9. Evidence becomes authority only when bound to a transition, decision, blocker, certification, or recovery target.
10. Every decision consumes evidence or an explicitly authorized absence of evidence.
11. Every decision must be validated by the protocol that owns its decision class.
12. A decision is not authoritative because it is plausible; it is authoritative because its protocol accepted it.
13. Every artifact has exactly one primary owner at a time.
14. Artifact consumers do not gain mutation authority by consumption.
15. Candidate artifacts cannot replace authoritative artifacts without promotion or an equivalent acceptance protocol.
16. Materialization includes extraction, validation, writing, provenance, and lifecycle advancement.
17. Artifact deletion is a lifecycle event, not erasure of semantic history.
18. State and lifecycle are distinct and must agree where readiness, execution, completion, or block status overlaps.
19. A persisted state without matching entry evidence is not safe to advance.
20. Report-only states do not mutate on ordinary run.
21. Transition completion includes required evidence, decision, artifact, lifecycle, persistence, and report effects.
22. A completion claim is not completion authority until certified.
23. Recovery requires original evidence and intent.
24. Recovery cannot invent a target state.
25. Unsupported recovery remains blocked, failed, or report-only.
26. Cancellation is not failure when a recoverable dispatch state is preserved.
27. Machine memory records authority exercised by capabilities; it does not replace that authority.
28. Identity survives version change, snapshotting, and projection.
29. Version supersession does not rewrite history.
30. Freshness is a semantic condition, not a cache detail.
31. Concurrency is allowed only where protocols define ordering, snapshot validation, and conflict handling.
32. Future capabilities must enter by declaring protocols, owned subjects, authority, evidence, lifecycle, invariants, and recovery semantics under this constitution.

## Deliverable 5: Authority Model

Authority is a lifecycle-bearing semantic subject.

### Authority Lifecycle

```text
Created -> scoped -> delegated or retained -> exercised -> recorded -> consumed -> transferred, superseded, revoked, or retired
```

### Authority Creation

- Machine authority is created by the machine boundary and command intent, then admitted by Run Control.
- Capability authority is created by capability ownership and protocol definitions.
- Transition authority is created by protocol rules over a current source state.
- Decision authority is created when a decision protocol validates evidence and choice.
- Artifact authority is created through ownership, promotion, materialization, certification, or explicit handoff.
- Evidence authority is created by validation and binding.
- Recovery authority is created when blocker evidence and transition intent enter a supported recovery protocol.
- Human exception authority is created only when explicit human intent or approval is captured with scope and lineage.

### Authority Transfer

- Capability handoff transfers consumption or mutation authority between owners.
- Promotion transfers authority from candidate output to an authoritative artifact.
- Certification transfers a completion claim into closure, continuation, reopening, evidence gathering, or block authority.
- Recovery transfers a blocked subject to a validated target only when the recovery protocol accepts the repair.
- Policy or governance may transfer or revoke authority, but only with durable evidence.

### Authority Validation

Authority is validated by source freshness, protocol entry rules, state readiness, artifact lifecycle, evidence binding, parser or policy acceptance, invariants, and recovery eligibility.

### Authority Consumption

Authority is consumed by interactions, decisions, transitions, artifact mutation, lifecycle advancement, reporting, and recovery. Consumption must be recorded when it affects durable state, artifacts, decisions, evidence, or lifecycle.

### Authority Retirement

Authority retires when the interaction completes, the subject is superseded, the lifecycle exits the authority scope, the decision is consumed, policy changes, evidence becomes stale, or recovery closes or remains blocked.

### Authority Classes

Decision authority chooses among allowed alternatives. Artifact authority mutates or promotes durable information. Transition authority advances or confirms state. Recovery authority restores safe movement after blockage. Lifecycle authority advances subject phase. Evidence authority allows proof to influence decisions or recovery. Reporting authority describes the current semantic condition without advancing it.

## Deliverable 6: Identity Model

Identity is the continuity rule for semantic subjects.

### What Has Identity

Machines, sources, capabilities, protocols, interaction instances, states, transitions, decisions, evidence records, artifacts, projections, authority grants, lifecycle entries, invariants, recovery reviews, reports, and human exception records all have identity.

### Identity, Version, Instance, Snapshot, Projection, Observation, Evidence

- Identity: the stable subject continuity.
- Version: a change in the subject's content or lifecycle while identity persists.
- Instance: one occurrence of an interaction, transition, decision, evidence capture, or recovery review.
- Snapshot: a captured state of a subject at a sequence point.
- Projection: a derived view over one or more source snapshots for a purpose.
- Observation: raw captured information before validation and binding.
- Evidence: an observation that has been validated and bound.

### Persistence

Identity persists through version changes, lifecycle movement, projection, archival, and replay. Identity may be retired, but not silently reused for a different subject.

### Evolution

Content, lifecycle phase, freshness, version, consumers, projections, and authority scope can change. Stable identity, lineage root, historical evidence, and prior decision records cannot change except through governed correction that preserves both old and new evidence.

### Relationship Rules

- A snapshot has identity as a snapshot, not as the subject itself.
- A projection has derived identity tied to source identities and versions.
- Evidence identity includes both the observation and its binding.
- Decision identity includes the choice set and evidence basis.
- Artifact identity survives representation changes only if lineage and owner preserve equivalence.

## Deliverable 7: Interaction Model

The most fundamental unit of work is the Interaction.

A Transition is the state effect of an interaction. A protocol step is an interaction within a protocol. An observation is input to an interaction. Therefore, interaction is the smallest unit that can express report-only work, prompt-backed work, artifact promotion, materialization, decision routing, certification, and recovery.

### Universal Interaction Stages

1. Intent: why work is being attempted.
2. Subject: what the work concerns.
3. Authority: who may perform or accept the work.
4. Inputs: sources, artifacts, evidence, decisions, projections, or state.
5. Readiness: preconditions, freshness, lifecycle, and invariants.
6. Execution: prompt run, external interpretation, review, materialization, report, or other work form.
7. Observation: output, failure, blocker, cancellation, or reportable condition.
8. Interpretation: parsing, classification, route selection, or semantic reading when output matters.
9. Validation: policy, artifact rules, lifecycle, freshness, invariants, and recovery checks.
10. Decision: optional authoritative choice.
11. Mutation: optional state, artifact, lifecycle, or memory change.
12. Evidence: durable proof of what happened.
13. Outcome: target state, same state, report, block, failure, cancellation, completion, or recovery target.

Intent, subject, authority, inputs, readiness, execution, observation, validation, evidence, and outcome are universal. Interpretation, decision, and mutation are conditional but governed when present.

## Deliverable 8: Protocol Model

A protocol is a governed interaction sequence owned by a capability.

### Protocol Purpose

Protocols make work repeatable, reviewable, recoverable, and safe to evolve. They define how intent becomes validated outcome.

### Protocol Ownership

Each protocol has one primary capability owner. Participants may supply sources, consume artifacts, validate evidence, or receive handoff authority, but ownership remains explicit.

### Protocol Lifecycle

```text
Defined -> active -> invoked -> running -> completed, paused, blocked, failed, cancelled, superseded, or retired
```

### Protocol Participants

Participants may include machine control, source providers, capability owners, artifact owners, validators, recovery reviewers, reporting consumers, and humans.

### Protocol Entry and Exit

Entry requires source state, authority, subject identity, readiness, and required input availability. Exit produces one of: success, pause, block, failure, cancellation, completion, recovery target, or report-only outcome.

### Protocol Success, Failure, and Recovery

Success means the protocol's required evidence, decisions, artifacts, lifecycle changes, state effects, and reports are complete. Failure or blockage must preserve evidence and recovery intent when recovery is semantically supported.

### Protocols Versus State Machines

A state machine is a projection of a protocol onto states and transitions. It answers "what state can follow what state?" A protocol answers the larger question: "who may do what, with which information, under which evidence, validation, artifact, lifecycle, reporting, and recovery rules?"

## Deliverable 9: State Model

State is a control summary, not the whole truth.

### Purpose

State constrains what may happen next and communicates whether the machine is ready, paused, blocked, cancelled, failed, completed, or awaiting recovery.

### Identity and Ownership

State identity is bound to machine identity, subject identity, owner, and entry evidence. The owning capability controls normal entry and exit. Run Control controls command-level interpretation. Recovery controls restoration from blocked or cancelled conditions.

### Validity

A state is valid only when its entry evidence, artifact lifecycle, decision lineage, and freshness requirements still hold. A state may be persisted but unsafe to advance if supporting lifecycle or source freshness no longer matches.

### Entry and Exit

State entry requires a transition or initialization protocol. State exit requires an authorized transition, recovery, or terminal/report-only classification.

### Readiness, Pause, Completion, Failure, Recovery

- Ready states permit active interactions if freshness and lifecycle agree.
- Pause states intentionally stop automatic advancement.
- Blocked states require evidence and recovery intent.
- Completed states report terminal or subject-level completion according to scope.
- Failed states report unsafe or unrecoverable conditions until repaired.
- Recovery states are not normal work states; they are evidence review positions.

### Fundamental or Derived

State is derived from transition history, evidence, decisions, and lifecycle, but once persisted it is a first-class control artifact. It is not ontologically root; it is authoritative only with its supporting proof.

## Deliverable 10: Decision Model

Decisions are authority-bearing artifacts when persisted.

### Creation

A decision is created by an interaction that presents alternatives under a protocol. It may be generated by a model, parser, policy, route table, human, validator, or recovery review, but creation alone is not authority.

### Authority and Evidence

Decision authority requires evidence or an explicitly governed absence of evidence. The decision class determines which capability can validate it.

### Validation

Validation checks that the choice is in the allowed set, the evidence is bound and fresh enough, the subject identity is correct, the lifecycle permits the choice, and the protocol owner accepts the result.

### Persistence and Consumption

Persisted decisions become lineage. They are consumed by transitions, artifact promotion, certification, recovery, reporting, or future selection. Machine memory records them; it does not author them.

### Replay, Retirement, Supersession

Decisions may be replayed when evidence and source snapshots still match. They retire when consumed, superseded by newer evidence, invalidated by lifecycle, or archived with completed history. Supersession preserves prior decision identity and evidence.

### Are Decisions Artifacts?

Yes. A persisted decision is an artifact with special authority. The artifact form stores the decision, but authority comes from validated protocol acceptance.

## Deliverable 11: Evidence Model

Evidence is validated, bound observation.

### Observation and Capture

Observation is raw output, source fact, failure context, human input, or repository condition. Capture records the observation with subject, time sequence, source, and context.

### Validation and Promotion

Evidence is validated against parser rules, policy, artifact rules, lifecycle, freshness, identity, and invariant requirements. It is promoted to authority only when bound to a transition, decision, certification, blocker, or recovery target.

### Consumption and Retention

Evidence is consumed by decisions, transitions, certification, artifact promotion, recovery, reporting, and projection generation. Retention preserves replayability, even after the evidence is no longer current authority.

### Observation, Authority, History

- Evidence remains observation when uncaptured, unvalidated, stale, or unbound.
- Evidence becomes authority when the owning protocol validates and binds it.
- Evidence becomes history when consumed, superseded, archived, or retained for replay rather than current decision authority.

### Replay and Retirement

Replay uses evidence identity, source snapshots, validation result, and binding. Evidence may retire as current authority, but its historical record remains unless a governed deletion protocol preserves a deletion record.

## Deliverable 12: Artifact Model

Artifacts are durable representations of understanding, authority, evidence, or history.

### Artifact Lifecycle

```text
Created -> owned -> validated -> promoted or retained -> mutated or consumed -> referenced -> superseded, archived, deleted, or retained as history
```

### Creation and Ownership

Creation requires an owner and purpose. Ownership determines mutation authority, lifecycle advancement, validation rules, and downstream consumers.

### Validation and Promotion

Validation checks structure, subject identity, provenance, freshness, policy, and invariants. Promotion changes authority, not merely location or file content.

### Mutation and Consumption

Mutation must be authorized and recorded. Consumption does not grant ownership. Consumers may derive projections or decisions only within their own protocols.

### Reference, Supersession, Archival, Deletion

References preserve subject identity and version. Supersession creates a new current version while preserving old lineage. Archival retains history. Deletion is a lifecycle outcome that requires evidence and must not erase decision lineage.

### Truth or Current Understanding

Artifacts represent current understanding or durable historical record. Truth is established through validated evidence, accepted decisions, source authority, and lifecycle agreement. A current artifact can be authoritative without being absolute truth.

## Deliverable 13: Invariant Model

Invariants are semantic constraints with violation semantics.

### Origin and Ownership

Invariants originate from machine law, capability ownership, protocol contracts, artifact definitions, evidence rules, decision policies, lifecycle rules, recovery rules, and governance decisions. Each invariant has an owner.

### Evaluation

Invariants are evaluated at source capture, readiness, projection generation, interaction start, interpretation, validation, materialization, promotion, decision persistence, lifecycle advancement, state persistence, reporting, and recovery.

### Violation and Recovery

Violation may block, fail, pause, reject, supersede, request more evidence, or enter recovery. Recovery is allowed only when the invariant's contract defines a supported repair.

### Categories

- Machine invariants constrain boundary, control, memory, and command behavior.
- Capability invariants constrain ownership and handoff.
- Protocol invariants constrain entry, sequence, success, failure, and recovery.
- Interaction invariants constrain readiness, execution, validation, and recording.
- Artifact invariants constrain identity, structure, ownership, promotion, and lifecycle.
- Evidence invariants constrain capture, validation, binding, authority, replay, and retention.
- Decision invariants constrain alternatives, evidence, validation, persistence, and consumption.
- State invariants constrain entry evidence, readiness, report-only behavior, and exit.
- Transition invariants constrain source, target, mutation, persistence, and recovery.

### Absolute and Contextual Invariants

Absolute invariants:

- No authority without scope.
- No decision authority without validation.
- No evidence authority without binding.
- No artifact mutation without owner authority.
- No recovery without preserved evidence and intent.
- No state advance without authorized transition semantics.
- No reporting field can become execution authority by display alone.

Contextual invariants:

- Which sources must be fresh.
- Which artifact shapes are valid.
- Which decisions are allowed.
- Which recovery intents are supported.
- Which pauses are terminal.
- Which human exceptions are accepted.
- Which lifecycle phases can synchronize.

## Deliverable 14: Lifecycle Model

Everything that can change under the machine has a lifecycle: machines, sources, capabilities, protocols, interactions, states, transitions, decisions, evidence, artifacts, projections, authority grants, identities, invariants, recovery reviews, and reports.

### Composition

Lifecycles compose by subject relationship. A transition lifecycle can advance a state lifecycle, artifact lifecycle, evidence lifecycle, decision lifecycle, and authority lifecycle in one interaction. Composition does not merge ownership.

### Synchronization

Lifecycles synchronize at readiness, promotion, execution, completion, block, recovery, supersession, and archival points. The machine must prove synchronization when using one lifecycle as authority for another.

### Constraint

A lifecycle can constrain another lifecycle. For example:

- Artifact readiness can constrain state advancement.
- Evidence validation can constrain decision authority.
- Decision consumption can constrain transition target.
- Recovery eligibility can constrain blocked state exit.
- Source freshness can constrain projection validity.

### Lifecycle Grammar

The shared grammar is:

```text
Absent or proposed
  -> captured or created
  -> prepared
  -> validated
  -> active, ready, or authoritative
  -> consumed, executing, or reported
  -> completed, blocked, cancelled, failed, superseded, archived, deleted, or retired
```

Each subject uses a subset of this grammar.

## Deliverable 15: Temporal Model

The machine understands time primarily as sequence, not as wall-clock time.

### Current

Current means the latest validated state, artifact lifecycle, evidence authority, decision lineage, and source freshness that agree for a subject.

### History

History is append-only lineage of interactions, transitions, decisions, evidence, artifact versions, lifecycle movements, reports, and recovery reviews. Supersession adds to history; it does not rewrite it.

### Future

Future is represented by allowed transitions, pending protocols, next reportable actions, and possible recovery targets. Future is descriptive until authority is created and exercised.

### Replay

Replay is valid only when identities, versions, source snapshots, evidence bindings, protocol rules, and policy versions still match or when differences are explicitly accounted for.

### Snapshot and Version

A snapshot captures a subject at a sequence point. A version records subject evolution. A projection is derived from snapshots and versions.

### Ordering and Concurrency

Ordering is established by interaction sequence, transition history, evidence capture, and persistence. Concurrency is permitted only where protocols define snapshot discipline, conflict detection, and authority precedence. Stale snapshots must be revalidated before mutation or recovery.

### Cancellation

Cancellation is a temporal interruption. It must preserve enough dispatch state, evidence, and intent to resume safely or report that safe resume is unavailable.

## Deliverable 16: Information Model

Information moves through semantic transformations:

```text
Source or observation
  -> capture
  -> projection or evidence
  -> interpretation
  -> validation
  -> decision
  -> transition, artifact mutation, lifecycle movement, or report
  -> memory
  -> replay, projection, certification, recovery, or archival
```

### Transformations

- Observation becomes evidence through capture, validation, and binding.
- Source becomes projection through scoped preparation and freshness tracking.
- Evidence becomes decision input through protocol acceptance.
- Decision becomes transition authority through validation.
- Transition becomes state, lifecycle, artifact, history, and report effects.
- Artifact becomes source for future projections when its identity, version, and lifecycle are valid.
- History becomes knowledge when distilled into current authoritative artifacts.
- Knowledge becomes report when projected without creating mutation authority.
- Blocker evidence becomes recovery input when intent and eligibility are preserved.

### Memory

Machine memory is not a private parallel truth. It is the durable record of state, artifacts, decisions, evidence, lifecycle, projections, manifests, blockers, reports, and lineage from which the machine can resume and recover.

## Deliverable 17: Semantic Boundaries

The strongest boundaries that must never blur are:

1. Source vs projection: prepared views do not replace source authority.
2. Observation vs evidence: raw output is not authority.
3. Evidence vs decision: proof constrains choice but is not the choice.
4. Decision vs suggestion: accepted, validated choice is authority; rationale is not.
5. Authority vs permission: ability to act is not acceptance of result.
6. Reporting vs execution: reports describe; they do not advance.
7. State vs lifecycle: workflow position and subject evolution must agree but remain distinct.
8. Protocol vs state machine: state graph is only one projection of governed work.
9. Candidate vs promoted artifact: generated content is not authoritative until accepted.
10. Artifact vs evidence: durable information is not proof unless bound as proof.
11. Completion claim vs certification: claim starts certification; it does not close work.
12. Memory vs authority owner: memory records; capability protocols decide.
13. Blocker production vs recovery: many interactions can block; only recovery protocols can unblock.
14. Human exception vs model preference: exceptions require explicit human authority and scope.
15. Current understanding vs history: current artifacts can be superseded; history remains lineage.

## Deliverable 18: Universal Semantic Patterns

### Control Pattern

```text
Load current memory -> classify command/state -> authorize active work or report -> dispatch protocol
```

This appears in initialization, resume, status, cancellation, failed report, completed report, and unblock dispatch.

### Interaction Pattern

```text
Intent -> readiness -> preparation -> execution -> observation -> interpretation -> validation -> decision or mutation -> evidence -> persistence -> report
```

This is the universal work pattern. Some interactions skip interpretation, decision, or mutation, but none skip authority, validation, evidence, outcome, and report.

### Promotion Pattern

```text
Candidate -> classify -> validate -> transfer authority -> mutate artifact lifecycle -> record evidence -> persist state
```

This explains active epic promotion and any future authoritative artifact acceptance.

### Materialization Pattern

```text
Output -> extract -> validate identities and paths -> write artifacts -> write manifests/provenance -> advance lifecycle -> validate invariants -> record evidence
```

This explains split families, milestone specs, archives, and future bundle-producing protocols.

### Routing Pattern

```text
Evidence or output -> parse/classify -> validate allowed route -> record decision -> enter target state or blocker
```

This explains selection, audit, execution disposition, completion certification, and recovery review.

### Certification Pattern

```text
Claim -> collect evidence -> independent evaluation -> policy validation -> route -> update subject lifecycle and memory
```

This explains completion certification and any future independent acceptance process.

### Recovery Pattern

```text
Block/failure/cancellation -> preserve evidence and intent -> review eligibility -> validate repair -> restore supported target or retain blocker
```

This explains evidence-blocked recovery, invalid certification repair, malformed execution evidence repair, and future recoverable blockers.

### Distillation Pattern

```text
History/evidence -> evaluate durable relevance -> validate placement -> update current understanding -> preserve lineage
```

This explains completion-context updates, operational-context evolution, preservation insight extraction, and future knowledge maintenance.

## Deliverable 19: Semantic Compression

The recovered architecture compresses into the semantic model as follows:

| Recovered architecture | Semantic model |
|---|---|
| 9 capabilities | 1 capability primitive plus owned protocol contracts |
| 7 state machines plus Machine Memory | Protocol primitive plus state/transition projections and memory artifacts |
| 21 conceptual states | 1 state contract with ready, active, paused, blocked, cancelled, failed, completed, and report-only roles |
| 32 canonical transitions | 1 interaction contract plus transition effect contract |
| 6 transition archetypes | 6 universal patterns under one interaction model |
| 14 execution stages | 13 interaction stages, most optional but governed |
| 13 artifact families | 1 artifact primitive with current, candidate, projection, evidence, manifest, decision, and archive roles |
| 4 decision classes | 1 decision primitive with strategic, operational, certification, and recovery scopes |
| 9 invariant categories | 1 invariant primitive with absolute and contextual scopes |
| 10 lifecycles | 1 lifecycle grammar specialized by subject |
| Preservation platform concepts | Existing authority, evidence, artifact, decision, recovery, and distillation primitives |

Estimated compression:

- Capability compression: 9 observed capabilities -> 1 capability contract, about 9:1.
- Transition compression: 32 transitions -> 1 transition contract plus 6 patterns, about 4.6:1.
- State compression: 21 states -> 1 state contract plus 8 roles, about 2.3:1.
- Artifact compression: 13 families -> 1 artifact contract plus 7 roles, about 1.9:1.
- Lifecycle compression: 10 lifecycles -> 1 lifecycle grammar, about 10:1.
- Overall semantic compression: roughly 92 recovered architectural units -> 16 semantic primitives, about 5.7:1.

The main compression is not fewer names. It is the recognition that authority, identity, lifecycle, evidence, and interaction contracts explain the recovered capabilities without changing the semantic foundation for future capabilities.

## Deliverable 20: Semantic Constitution

### Article 1: What Exists

The machine exists as a bounded semantic universe. Within it exist sources, capabilities, protocols, interactions, states, transitions, decisions, evidence, artifacts, projections, authority grants, identities, lifecycles, invariants, reports, and recovery paths.

Nothing else is required as a semantic primitive. New implementation structures are valid only when they instantiate these primitives or compose them without changing their laws.

### Article 2: What Can Happen

Only interactions can happen. An interaction may observe, prepare, execute, interpret, validate, decide, transition, mutate, report, certify, promote, materialize, distill, block, cancel, fail, or recover. Every interaction must have authority, subject, inputs, validation, evidence, and outcome.

### Article 3: Who Owns What

Every subject has an owner. Capabilities own protocols. Protocols own interaction rules. Artifact owners own mutation authority. Decision owners own choice validation. Evidence producers own capture until binding. Recovery owns unblocking authority. Machine memory owns durable recordkeeping, not branch authority.

Ownership may be transferred only by an authorized protocol and must be recorded.

### Article 4: How Authority Flows

Authority flows from machine boundary and command intent into Run Control, then into capability protocols, then into interactions, decisions, transitions, artifact mutation, lifecycle advancement, evidence binding, reporting, and recovery.

Authority is scoped. It is created, exercised, consumed, transferred, superseded, revoked, or retired. No authority is implied by convenience, file access, process access, generated text, report display, or previous success.

### Article 5: How Knowledge Evolves

Knowledge begins as source or observation. It becomes evidence through capture, validation, and binding. It becomes decision input through protocol acceptance. It becomes current understanding through authorized artifact mutation, projection, certification, or distillation. It becomes history through supersession, archival, or retention.

Current understanding is always versioned and replayable. History is not overwritten by newer understanding.

### Article 6: How Truth Is Established

The machine does not treat raw artifacts as truth. It establishes operational truth through source authority, validated evidence, accepted decisions, artifact lifecycle, invariant satisfaction, and durable memory agreement.

A current artifact may be authoritative for action without being absolute truth. Its authority lasts only within its identity, version, lifecycle, freshness, and owner scope.

### Article 7: How Decisions Emerge

Decisions emerge from interactions that present alternatives under a protocol. They consume evidence, are validated by the owner of the decision class, are persisted for lineage, and are consumed by transitions, artifact mutation, certification, recovery, or reporting.

A decision is an artifact when persisted, but it is not authoritative because it is stored. It is authoritative because its protocol accepted it.

### Article 8: How Evidence Evolves

Evidence evolves from observation to captured record to validated proof to bound authority to consumed history. Evidence can authorize decisions, transitions, certification, and recovery only while bound and valid.

Blocker evidence has special force: it preserves both the reason advancement stopped and the basis on which recovery may later judge repair.

### Article 9: How Artifacts Evolve

Artifacts are created with owner and purpose, validated before authority, promoted before replacement, mutated only by authority, consumed without transferring ownership, superseded without erasing history, archived with lineage, and deleted only as a recorded lifecycle event.

Artifacts represent current understanding or history. Promotion and materialization are authority changes, not mere writes.

### Article 10: How Recovery Works

Recovery begins only from preserved evidence and intent. It reviews eligibility, validates repaired input, and restores only a supported target state or retains the blocker. Recovery cannot invent missing authority, erase original evidence, reinterpret unsupported blockers as success, or turn retry into repair without protocol authority.

### Article 11: How Lifecycles Interact

Lifecycles compose across subjects. State, artifact, evidence, decision, authority, projection, and recovery lifecycles may advance together, but they remain distinct. A state claiming readiness must be supported by artifact lifecycle, evidence, and freshness. A lifecycle claiming completion must be supported by certification or owner authority where required.

### Article 12: How Identity Persists

Identity persists across versions, snapshots, projections, observations, evidence, decisions, lifecycle changes, archival, and replay. Versions change content. Instances record occurrences. Snapshots freeze sequence points. Projections derive views. Evidence binds observations. None of these may silently replace subject identity.

### Article 13: How Information Moves

Information moves from source and observation into projection and evidence, then into interpretation, validation, decision, transition, artifact mutation, memory, report, recovery, replay, and archival. Each transformation changes authority and must be governed by the receiving primitive's contract.

### Article 14: How Time Is Understood

The machine understands time as ordered lineage. Current means latest valid agreement across state, lifecycle, artifacts, evidence, decisions, and source freshness. Future means permitted but not yet authorized interactions. History means retained sequence of what happened and why. Replay depends on identity, version, source, evidence, protocol, and policy equivalence.

### Article 15: How Future Capabilities Fit

A future capability does not require new semantic foundations. It must declare:

- its purpose and owner;
- its protocols;
- its subjects and identities;
- its states and transition effects;
- its artifacts, projections, decisions, and evidence;
- its authority grants and handoffs;
- its lifecycles and invariants;
- its reporting and recovery semantics.

If it can do that, it belongs inside the machine. If it cannot, it is outside the constitution or it is an implementation shortcut that has not yet recovered its semantics.

### Article 16: Constitutional Summary

The enduring system is a machine that turns source-bound intent into authorized interaction. Interactions produce observations, evidence, decisions, transitions, artifacts, reports, and recovery outcomes. Authority determines who may accept those outcomes. Identity preserves what the outcomes are about. Lifecycle governs how subjects evolve. Invariants prevent unsafe evolution. Memory records the lineage. Recovery preserves continuity when ordinary advancement fails.

Everything else is implementation.
