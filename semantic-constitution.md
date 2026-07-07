# Semantic Constitution

## Recover the Semantic Constitution

This document recovers the semantic constitution that governs the recovered LoopRelay architecture. It is derived from the prior audit chain:

- `state-machine-refactor-audit.md`: implementation and current behavior evidence.
- `secondary-state-machine-audit.md`: canonical machine evidence.
- `third-state-machine-audit.md`: canonical architecture evidence.
- `audit.md`: preservation and future authority evidence.

It does not redesign the system. It does not prescribe implementation, repository layout, framework, language, class names, storage medium, or process topology. It defines the semantic reality that must remain true when those things change.

The recovered architecture contains strategic intent, durable memory, prompt-backed and evidence-backed work, artifact promotion, completion certification, reporting, and recovery. The smallest stable model is not a list of current components. It is a constitution, an ontology, contracts, semantic models, and derived architectural implications.

## Part I: Constitution

The constitution is the source of the rest of this document. Ontology, contracts, state machines, artifacts, and implementation structures are valid only insofar as they preserve these laws.

### Constitutional Laws

1. A governed system can act only inside its declared boundary.
2. Every governed thing is a subject or is derived from a subject.
3. Every subject has identity, ownership, lifecycle, authority, and relationship rules.
4. Intent precedes interaction, but intent alone does not create authority.
5. Intent distinguishes governed work from passive knowledge.
6. A protocol is the only valid way for intent to become governed work.
7. Relations between subjects are governed semantic facts, not informal references.
8. Only interactions happen. Transitions, reports, promotions, materializations, certifications, and recoveries are interaction effects or protocol purposes.
9. Every active interaction must have intent, subject, authority, inputs, validation, evidence, and outcome.
10. Reporting describes authority; it does not create authority.
11. Permission to run a command, write storage, or display a result is not authority to accept the result.
12. Source information cannot become system authority without capture, validation, and binding.
13. Ingesting a source creates or updates an artifact; it does not erase source origin or source identity.
14. Prompt output, external output, generated text, parser output, and report text are observations until interpreted and validated.
15. Evidence becomes authority only when bound to a decision, interaction effect, blocker, certification, or recovery target.
16. Every decision consumes evidence or an explicitly governed absence of evidence.
17. Every decision must be validated by the protocol that owns its decision class.
18. A decision is not authoritative because it is plausible; it is authoritative because its protocol accepted it.
19. Every artifact has exactly one primary owner at a time.
20. Artifact consumers do not gain mutation authority by consumption.
21. Candidate artifacts cannot replace authoritative artifacts without promotion or an equivalent acceptance protocol.
22. A projection is an artifact role. It is never source authority.
23. Artifact deletion is a lifecycle event, not erasure of semantic history.
24. State and lifecycle are distinct and must agree where readiness, execution, completion, or block status overlaps.
25. A persisted state without matching entry evidence is not safe to advance.
26. A transition is an interaction effect, not a primitive event independent of interaction.
27. Transition completion includes required evidence, decision, artifact, lifecycle, persistence, and report effects.
28. A completion claim is not completion authority until certified.
29. Recovery is a protocol over preserved evidence and intent, not a retry label.
30. Recovery cannot invent a target state, erase original evidence, or reinterpret an unsupported blocker as success.
31. Machine memory records authority exercised by owners; it does not replace that authority.
32. Identity survives version change, snapshotting, projection, observation, evidence, and archival.
33. Version supersession does not rewrite history.
34. Freshness is a semantic condition, not a cache detail.
35. Concurrency is allowed only where protocols define ordering, snapshot validation, conflict handling, and authority precedence.
36. Future capabilities must enter by declaring subjects, protocols, authority, evidence, lifecycle, invariants, artifacts, relationships, and recovery semantics under this constitution.

### Article 1: What Exists

A governed system exists as a bounded semantic domain. Within it exist subjects, intents, sources, protocols, interactions, states, decisions, evidence, artifacts, authority grants, identities, lifecycles, invariants, and governed relationships.

Capability, transition, projection, recovery, state machine, report, and machine memory are not additional foundations. They are compositions of the primitives above.

### Article 2: What Can Happen

Only interactions can happen. An interaction may observe, prepare, execute, interpret, validate, decide, mutate, report, certify, promote, materialize, distill, block, cancel, fail, or recover.

Every interaction begins from intent, concerns a subject, enters through a protocol, exercises scoped authority, consumes inputs, produces observations, validates outcomes, records evidence, updates governed relationships when authorized, and reports its outcome.

### Article 3: What Work Is About

Every governed action concerns a subject. A subject is anything whose continuity matters: a repository, plan, issue, protocol run, artifact, state, decision, evidence record, authority grant, lifecycle entry, report, or recovery review.

Identity answers what remains the same. Lifecycle answers how the subject may evolve. Authority answers who may accept change. Evidence answers why change may be believed. Relationship rules answer how one subject may validly refer to, own, support, produce, consume, represent, supersede, or constrain another.

Identity is retained as a primitive because identity is not just a field on a subject. It is the governed equivalence rule by which versions, snapshots, projections, observations, artifacts, evidence, and replay remain about the same subject.

### Article 4: How Subjects Relate

Subjects do not form a valid system merely by existing. They form a valid system through typed, governed relationships.

A relation may declare ownership, representation, support, production, consumption, derivation, validation, authorization, supersession, containment, projection, blocking, recovery, or reporting. Each relation has subject endpoints, type, owner, evidence basis, authority scope, lifecycle, and invalidation rule.

Untyped references are not semantic relations. A path, identifier, pointer, display link, or textual mention becomes a relation only when its meaning, authority, and evidence are governed.

### Article 5: How Intent Enters

Intent is the semantic direction toward a desired outcome for a subject. Intent may come from a human, command, roadmap, protocol, blocker, certification claim, or recovery request.

Intent is the second constitutional anchor after subject. A passive graph may contain subjects, artifacts, evidence, and relations. A governed system acts only when intent is admitted by protocol.

Intent is prior to interaction but weaker than authority. It can request, explain, or preserve the reason for work. It cannot mutate state, accept output, or close work until a protocol admits it and authority is exercised.

### Article 6: How Protocols Govern

A protocol is the governed form by which intent becomes interaction. It defines owner, subject class, participants, entry conditions, required inputs, evidence rules, allowed decisions, artifact effects, state effects, lifecycle effects, reporting, failure, and recovery.

A state machine is only a projection of protocol behavior over states. It cannot replace the protocol that defines authority, evidence, artifacts, decisions, lifecycle, and recovery.

### Article 7: How Authority Flows

Authority is scoped. It is created, delegated, exercised, recorded, transferred, constrained, superseded, revoked, or retired. It is never implied by convenience, file access, process access, generated text, report display, or previous success.

Authority flows through owners and protocols into interactions, decisions, artifact mutation, state effects, lifecycle advancement, evidence binding, reporting, certification, and recovery. Each use must remain within subject, scope, validator, and lifecycle constraints.

### Article 8: How Knowledge Evolves

Knowledge begins as source or observation. It becomes evidence through capture, validation, and binding. It becomes decision input through protocol acceptance. It becomes current understanding through authorized artifact mutation, promotion, certification, or distillation. It becomes history through supersession, archival, or retention.

When an external source is ingested, its captured representation becomes an artifact, but the source remains the origin-bearing subject. The artifact may preserve source identity, version, freshness, and provenance; it does not become the source authority by storage alone.

Current understanding is always versioned and replayable. History is not overwritten by newer understanding.

### Article 9: How Truth Is Established

The system does not treat raw artifacts, reports, model output, command output, or memory entries as truth. It establishes operational truth through source authority, validated evidence, accepted decisions, artifact lifecycle, invariant satisfaction, and durable memory agreement.

A current artifact may be authoritative for action without being absolute truth. Its authority lasts only within its identity, version, lifecycle, freshness, and owner scope.

### Article 10: How Decisions Emerge

Decisions emerge from interactions that present alternatives under a protocol. They consume evidence, are validated by the owner of the decision class, are persisted for lineage, and are consumed by artifact mutation, state effects, lifecycle advancement, certification, recovery, or reporting.

A persisted decision is an artifact with special authority. It is not authoritative because it is stored. It is authoritative because its protocol accepted it.

### Article 11: How Evidence Evolves

Evidence evolves from observation to captured record to validated proof to bound authority to consumed history. Evidence can authorize decisions, state effects, certification, artifact mutation, lifecycle advancement, and recovery only while bound and valid.

Blocker evidence has special force: it preserves both the reason advancement stopped and the basis on which a recovery protocol may later judge repair.

### Article 12: How Artifacts Evolve

Artifacts are created with owner and role, validated before authority, promoted before replacement, mutated only by authority, consumed without transferring ownership, superseded without erasing history, archived with lineage, and deleted only as a recorded lifecycle event.

Artifacts represent current understanding or history. Promotion and materialization are authority changes, not mere writes.

### Article 13: How Recovery Works

Recovery is a protocol whose subject is a blocked, failed, cancelled, stale, or invalid condition. It begins only from preserved evidence and intent. It reviews eligibility, validates repaired input, and restores only a supported target state or retains the blocker.

Recovery cannot invent missing authority, erase original evidence, reinterpret unsupported blockers as success, or turn retry into repair without protocol authority.

### Article 14: How Lifecycles Interact

Lifecycles compose across subjects. State, artifact, evidence, decision, authority, and recovery lifecycles may advance together, but they remain distinct. A state claiming readiness must be supported by artifact lifecycle, evidence, and freshness. A lifecycle claiming completion must be supported by certification or owner authority where required.

### Article 15: How Identity Persists

Identity persists across versions, snapshots, projections, observations, evidence, decisions, lifecycle changes, archival, and replay. Versions change content. Instances record occurrences. Snapshots freeze sequence points. Projections derive views. Evidence binds observations. None of these may silently replace subject identity.

### Article 16: How Information Moves

Information moves from source and observation into artifact or evidence, then into interpretation, validation, decision, state effect, artifact mutation, relationship update, memory, report, recovery, replay, and archival. Each transformation changes authority and must be governed by the receiving primitive's contract.

### Article 17: How Time Is Understood

The system understands time as ordered lineage. Current means latest valid agreement across state, lifecycle, artifacts, evidence, decisions, and source freshness. Future means permitted but not yet authorized interactions. History means retained sequence of what happened and why. Replay depends on identity, version, source, evidence, protocol, and policy equivalence.

### Article 18: How Future Capabilities Fit

A future capability does not require new semantic foundations. It must declare:

- its owned subjects and identities;
- its protocols;
- its intents and accepted triggers;
- its authority grants and handoffs;
- its artifacts, decisions, evidence, and reports;
- its relationship types and relation evidence;
- its states as control summaries over protocol behavior;
- its lifecycles and invariants;
- its recovery semantics.

If it can do that, it belongs inside the system. If it cannot, it is outside the constitution or it is an implementation shortcut whose semantics have not yet been recovered.

### Article 19: Constitutional Summary

The enduring system turns source-bound intent into authorized interaction. Interactions produce observations, evidence, decisions, relationship updates, state effects, artifact effects, reports, and recovery outcomes. Authority determines who may accept those outcomes. Subject and identity preserve what the outcomes are about. Lifecycle governs how subjects evolve. Relations explain how subjects affect one another. Invariants prevent unsafe evolution. Memory records lineage. Recovery preserves continuity when ordinary advancement fails.

Everything else is implementation.

## Part II: Ontology

The ontology contains fourteen primitives. A primitive remains primitive only if removing it would prevent the remaining concepts from reconstructing its meaning without loss.

### Primitive Set

1. System
2. Subject
3. Intent
4. Source
5. Protocol
6. Interaction
7. State
8. Decision
9. Evidence
10. Artifact
11. Authority
12. Identity
13. Lifecycle
14. Invariant

Capability, transition, projection, recovery, machine, state machine, report, memory, promotion, materialization, certification, and distillation are derived constructs.

Identity and State are retained as primitives after pressure testing, but not because they are independent substances. Identity is primitive as a governed equivalence relation for subjects. State is primitive as an interaction-admission summary that must be named, persisted, validated, and reported even though it is reconstructed from lifecycle, evidence, decisions, artifacts, and authority.

Relations are not a fifteenth primitive. A relation is a governed semantic fact between subjects, expressed through authority, evidence, lifecycle, identity, and artifact records.

### System

A system is a bounded semantic domain in which subjects are governed by protocols, authority, evidence, lifecycle, and invariants.

- Boundary: the domain, repository, organization, workflow, or memory scope inside which the constitution applies.
- Identity: the stable continuity of the governed domain.
- Authority: system-level authority admits or denies command-facing interaction.
- Constraint: a system does not treat reporting, storage access, process access, or generated text as execution authority.

`Machine` is a system instantiated with durable memory, command admission, current state summary, and execution surface. Machine is operationally useful, but System is the primitive.

### Subject

A subject is anything whose continuity, authority, lifecycle, or evidence matters.

- Boundary: the thing being governed, observed, changed, reported, certified, or recovered.
- Identity: every subject has a stable identity relation; identity is how the subject remains itself across representations and history.
- Lifecycle: every mutable subject has allowed evolution.
- Authority: every subject has an owner or an explicit ownerless rule.
- Relationships: every subject can participate only in relation types its owner or protocol permits.
- Constraint: no interaction, decision, artifact mutation, relation, or recovery is valid without a subject.

### Intent

Intent is directed semantic pressure toward an outcome for a subject.

- Boundary: the reason work is being attempted.
- Identity: intent is identified by origin, subject, desired outcome, scope, and preserved lineage.
- Lifecycle: expressed, captured, admitted, satisfied, superseded, cancelled, blocked, or retired.
- Authority: intent can request work but cannot accept results until a protocol admits it.
- Relationships: intent binds origin to subject and protocol admission.
- Constraint: interaction without intent is ungrounded; intent without protocol is not governed action.

Intent is the primitive that turns a semantic repository into a governed actor. Without intent, sources, artifacts, evidence, decisions, and relations can describe a domain, but they cannot explain why work should begin.

### Source

A source is a fact-bearing input outside the immediate interaction that constrains what the system may validly know or do.

- Boundary: project context, roadmap intent, repository state, execution output, human instruction, policy, or external record.
- Identity: stable origin, scope, and captured version or snapshot.
- Lifecycle: observed, captured, consumed, superseded, unavailable, or retired.
- Authority: source authority comes from origin and becomes system authority only through validation and binding.
- Relationships: ingestion relates source identity to an artifact representation without merging them.
- Constraint: a prepared view over a source is not the source; an ingested artifact preserves source provenance but does not become source authority.

### Protocol

A protocol is a governed rule system that admits intent into interactions.

- Boundary: subject class, owner, entry conditions, participants, evidence requirements, decisions, effects, reporting, failure, and recovery.
- Identity: owner, purpose, subject class, entry condition, and allowed outcomes.
- Lifecycle: proposed, active, invoked, running, completed, paused, blocked, failed, cancelled, superseded, or retired.
- Authority: protocol authority is created by ownership and constrained by system authority and invariants.
- Constraint: a protocol is more than a state graph; it includes authority, evidence, artifacts, decisions, lifecycle, reporting, and recovery.

### Interaction

An interaction is the atomic governed occurrence in which intent, subject, authority, inputs, and protocol rules produce observation and outcome.

- Boundary: one governed unit of work.
- Identity: protocol, intent, subject, trigger, input snapshot, authority scope, and correlation lineage.
- Lifecycle: intended, authorized, prepared, executed, interpreted, validated, recorded, reported, and possibly recovered.
- Authority: inherited from system admission, protocol ownership, subject ownership, and current lifecycle/state.
- Constraint: execution output alone does not complete an interaction; completion requires validation, evidence, persistence, and reporting according to protocol.

### State

State is a named control condition of a subject, grounded in evidence and lifecycle agreement.

- Boundary: the current control meaning that constrains what interactions may happen next.
- Identity: system, owner, subject, state name, entry evidence, and lifecycle relation.
- Lifecycle: entered, validated, active or report-only, exited, superseded, terminal, or recovered.
- Authority: state constrains interaction eligibility but does not itself authorize work unless entry evidence and lifecycle still hold.
- Constraint: state is not the whole truth. It is a control summary over evidence, decisions, lifecycle, and artifacts.

State is reconstructable in principle from lifecycle, evidence, decisions, artifacts, and authority. It remains primitive because command admission, reporting, replay, and recovery require a named control condition with its own identity, evidence, and lifecycle.

### Decision

A decision is an accepted choice among allowed alternatives for a subject.

- Boundary: branch, route, artifact acceptance, certification, recovery target, lifecycle change, or policy result.
- Identity: subject, choice set, selected alternative, evidence consumed, validator, authority source, and persistence record.
- Lifecycle: proposed, parsed or classified, validated, persisted, consumed, superseded, replayed, archived, or retired.
- Authority: decision authority comes from validated evidence under the protocol that owns the decision class.
- Constraint: suggestions, rationale, report text, and next-action hints are not decisions until validated and persisted by the authorized protocol.

### Evidence

Evidence is validated and bound observation.

- Boundary: proof that an observation, effect, decision, blocker, certification, or recovery event occurred.
- Identity: observed source, capture event, validation result, binding, freshness or hash marker, and consumer scope.
- Lifecycle: observed, captured, validated, bound, promoted to authority or retained as blocker, consumed, replayed, archived, superseded, or retired.
- Authority: evidence becomes authority only after validation and binding.
- Constraint: raw observation is not evidence authority, and evidence must not silently mutate the artifact it explains.

### Artifact

An artifact is durable information with owner, role, subject, version, and lineage.

- Boundary: current understanding, plan, prepared view, candidate output, decision record, evidence record, manifest, report, archive, or memory entry.
- Identity: owner, semantic role, subject, representation key, version, and lineage.
- Lifecycle: absent, draft, candidate, validated, promoted, ready, consumed, executing, completed, blocked, superseded, archived, deleted, or retained as history.
- Authority: artifact authority belongs to its owner and may be transferred only through promotion, certification, or explicit handoff.
- Constraint: artifacts represent understanding or history, not unmediated truth.

### Authority

Authority is the scoped right to accept, decide, validate, mutate, promote, certify, report, recover, or retire a subject.

- Boundary: issuer, bearer, subject, scope, allowed actions, validators, evidence basis, and retirement condition.
- Identity: the grant or ownership relation that makes authority replayable.
- Lifecycle: created, delegated, exercised, recorded, transferred, constrained, superseded, revoked, or retired.
- Authority: authority is self-describing only when recorded.
- Constraint: permission to perform an action is not authority to accept the result.

### Identity

Identity is the continuity relation that lets a subject remain itself across change.

- Boundary: stable subject key, owner, lineage root, version relation, instance relation, snapshot relation, projection relation, and equivalence rules.
- Identity: identity defines its own continuity and may itself be a governed subject.
- Lifecycle: created, observed, instantiated, versioned, projected, superseded, merged only by authority, retired, or archived.
- Authority: identity creation and mutation belong to the subject owner or an explicitly authorized transfer protocol.
- Constraint: version change, snapshotting, projection, observation, and evidence do not silently replace subject identity.

Identity is a property of every subject, but it is primitive because equivalence, merge, split, projection, replay, and retirement rules require governance independent of any single representation.

### Lifecycle

Lifecycle is the allowed evolution of a subject.

- Boundary: phases, advancements, entry evidence, exit evidence, synchronization rules, blocking rules, completion rules, supersession rules, and archival rules.
- Identity: subject, owner, vocabulary, advancement rules, and evidence requirements.
- Lifecycle: defined, active, synchronized with state, advanced, blocked, completed, superseded, archived, or retired.
- Authority: lifecycle advancement belongs to the owner or a protocol with delegated lifecycle authority.
- Constraint: lifecycles compose but do not collapse. Workflow state and artifact lifecycle may overlap, but neither erases the other.

### Invariant

An invariant is a governed condition that must hold for a subject, relationship, lifecycle, interaction, decision, artifact, evidence record, state, or recovery path.

- Boundary: assertion, scope, owner, evaluation points, evidence inputs, violation semantics, recovery semantics, severity, lifetime, and governance rule for change.
- Identity: scope, assertion, authority, and evaluation rule.
- Lifecycle: declared, active, evaluated, violated, satisfied, weakened by governance, superseded, or retired.
- Authority: invariant authority comes from system law, protocol ownership, artifact definition, lifecycle rule, or explicit governance decision.
- Constraint: an invariant failure is not automatically recoverable. Recoverability is part of the invariant's contract.

### Derived Constructs

Derived constructs are stable and important, but they are not primitives.

| Derived construct | Reconstructed from |
|---|---|
| Machine | System + durable artifact memory + command admission protocol + current state summary |
| Capability | Subject ownership + authority boundary + protocol collection + artifact and decision responsibilities |
| Transition | Interaction effect on state, lifecycle, artifacts, evidence, decisions, or memory |
| Projection | Artifact whose role is a prepared, scoped, freshness-bound view over sources or artifacts |
| Recovery | Protocol whose subject is a blocked, failed, cancelled, stale, or invalid condition |
| State machine | Protocol projected onto states and transition effects |
| Report | Artifact or interaction outcome that describes authority without creating it |
| Memory | Artifact collection plus evidence lineage and current summaries |
| Promotion | Interaction effect that transfers authority from candidate artifact to authoritative artifact |
| Materialization | Interaction effect that writes artifacts with provenance and lifecycle advancement |
| Certification | Protocol that evaluates a completion claim against evidence and policy |
| Distillation | Protocol that turns history and evidence into current authoritative understanding |

## Part III: Universal Contracts

Contracts make the primitives instantiable without tying them to current implementation.

### System Contract

Every system has boundary, identity, durable memory rule, subject registry, protocol registry, relationship registry, authority map, invariant set, reporting surface, and recovery surface.

### Subject Contract

Every subject has identity, owner or ownerless rule, lifecycle, authority scope, evidence requirements, valid artifacts, valid decisions, reporting semantics, and retirement rule.

### Relation Contract

Every semantic relation has source subject, target subject or subject set, relation type, owner, authority scope, evidence basis, lifecycle, directionality, cardinality, invalidation rule, replay rule, and retirement rule.

### Intent Contract

Every intent has origin, subject, desired outcome, scope, capture rule, admission rule, authority relation, satisfaction rule, supersession rule, and retention rule.

### Source Contract

Every source has origin, subject, capture method, version or snapshot, freshness rule, trust boundary, authority level, and consumer scope.

### Protocol Contract

Every protocol has owner, subject class, accepted intents, participants, entry conditions, allowed interactions, input requirements, evidence requirements, relation effects, artifact effects, decision authority, state effects, lifecycle effects, success exits, failure exits, reporting semantics, and recovery semantics.

### Interaction Contract

Every interaction has intent, subject, protocol, trigger, authority, inputs, readiness checks, execution form, observations, interpretation rules, validation rules, optional decision, optional relation update, optional mutation, evidence recording, outcome, report, and recovery intent where failure is recoverable.

### State Contract

Every state has owner, subject, entry evidence, allowed interactions, readiness meaning, report-only or active classification, exit conditions, lifecycle relationship, blocker semantics, and recovery relationship.

### Decision Contract

Every decision has subject, alternatives, producer, authority source, evidence consumed, validator, result, persistence, consumers, replay rules, supersession rules, and retirement rules.

### Evidence Contract

Every evidence record has observation source, capture event, validator, binding, authority level, freshness or hash marker, consumers, retention policy, replay semantics, and retirement or archival semantics.

### Artifact Contract

Every artifact has owner, identity, semantic role, representation, validity rules, version, provenance, lifecycle, mutation authority, consumers, reference rules, supersession, archival, and deletion semantics.

### Authority Contract

Every authority grant has issuer, bearer, subject, scope, allowed actions, validators, evidence basis, transfer rules, consumption rules, expiry, revocation, and retirement.

### Identity Contract

Every identity has stable subject key, owner, lineage root, version relation, instance relation, snapshot relation, projection relation, equivalence rules, merge rule, and retirement rule.

### Lifecycle Contract

Every lifecycle has subject, owner, allowed phases, allowed advancements, entry evidence, exit evidence, synchronization rules, blocking rules, completion rules, supersession rules, archival rules, and retirement rules.

### Invariant Contract

Every invariant has assertion, scope, owner, evaluation points, evidence inputs, violation semantics, recovery semantics, severity, lifetime, and governance rule for change.

### Derived Construct Contracts

Derived constructs must declare which primitive contracts reconstruct them.

- A capability declares owned subjects, protocols, authority scopes, artifacts, decisions, evidence responsibilities, handoffs, lifecycle obligations, invariants, and recovery protocols.
- A transition declares the interaction that produced it, the affected subject, the prior state or lifecycle condition, the validated effect, the evidence, the persisted record, and the report.
- A projection declares artifact role, source set, source versions, owner, generation protocol, freshness rule, validation rule, consumers, invalidation rule, and retention policy.
- A recovery declares blocked subject, original intent, evidence set, eligibility rule, reviewer, repaired input rule, validation rule, target state rule, fallback rule, report, and lineage preservation.

## Part IV: Semantic Models

### Authority Model

Authority is a lifecycle-bearing subject.

```text
Created -> scoped -> delegated or retained -> exercised -> recorded -> consumed -> transferred, superseded, revoked, or retired
```

Authority is created by boundary, ownership, protocol definition, evidence validation, artifact promotion, certification, explicit handoff, or captured human exception. It is validated by source freshness, protocol entry rules, state readiness, artifact lifecycle, evidence binding, parser or policy acceptance, invariants, and recovery eligibility.

Authority classes include decision authority, artifact authority, state-effect authority, recovery authority, lifecycle authority, evidence authority, reporting authority, and human exception authority. Reporting authority describes current semantic condition without advancing it.

### Identity Model

Identity is the continuity rule for subjects. It is a property of every subject, but it is governed as a primitive because continuity, equivalence, replay, merge, split, retirement, and projection cannot be recovered from representation alone.

- Identity: stable subject continuity.
- Version: content or lifecycle change while identity persists.
- Instance: one occurrence of an interaction, decision, evidence capture, report, or recovery review.
- Snapshot: captured subject condition at a sequence point.
- Projection: derived artifact view over one or more source snapshots for a purpose.
- Observation: raw captured information before validation and binding.
- Evidence: observation that has been validated and bound.

Identity persists through version changes, lifecycle movement, projection, archival, and replay. It may be retired, but not silently reused for a different subject.

### Relationship Model

Relations are governed semantic facts between subjects. They are how the ontology becomes a system rather than a catalog.

Every relation has:

- source subject;
- target subject or target subject set;
- relation type;
- owner;
- evidence basis;
- authority scope;
- lifecycle;
- directionality and cardinality;
- invalidation rule;
- replay and retirement rules.

Core relation types:

- Ownership: a subject owns authority over another subject, protocol, artifact, decision, lifecycle, invariant, or relation.
- Representation: an artifact represents a subject without becoming the subject.
- Provenance: an artifact, evidence record, decision, or report derives from a source, observation, interaction, or prior artifact.
- Support: evidence supports a decision, state effect, certification, blocker, or recovery target.
- Production: an interaction produces observation, evidence, decision, artifact, report, or effect.
- Consumption: an interaction, decision, protocol, or artifact consumes source, evidence, decision, artifact, intent, or authority.
- Governance: a protocol governs interactions, decisions, artifact effects, state effects, lifecycle movement, reporting, and recovery.
- Authorization: authority permits a scoped action or acceptance over a subject.
- Constraint: an invariant constrains a subject, relation, interaction, lifecycle, artifact, decision, evidence record, or state.
- Supersession: one version, artifact, decision, evidence record, state, or lifecycle entry replaces another as current without deleting history.
- Projection: an artifact gives a scoped view over sources or artifacts for a purpose and freshness rule.
- Blocking: evidence prevents advancement of a subject until a protocol resolves or retains the block.
- Recovery: a protocol relates preserved blocker evidence and intent to a validated target or retained blocker.

References are not relations until governed. A filename, identifier, path, pointer, hyperlink, prompt mention, or report field has no semantic force unless a protocol gives it relation type, evidence, authority, lifecycle, and invalidation rules.

### Source and Artifact Model

Source and artifact are distinct even when the same bytes are involved.

- Source is origin-bearing. Its authority comes from where the information came from and the freshness or trust rules attached to that origin.
- Artifact is system-bearing. Its authority comes from ownership, validation, lifecycle, provenance, and accepted role inside the governed system.
- Ingestion captures a source into an artifact representation. The artifact records source identity, source version, capture context, freshness, and provenance.
- Ingestion does not merge source and artifact authority. The artifact can become authoritative for system action only through validation, binding, promotion, or protocol acceptance.
- A system artifact may later serve as a source for another interaction, but it does so through its artifact identity, lifecycle, and provenance rather than by losing its artifact role.

### Interaction Model

The universal interaction sequence is:

```text
Intent
  -> subject
  -> protocol admission
  -> authority
  -> inputs
  -> readiness
  -> execution or review
  -> observation
  -> interpretation
  -> validation
  -> decision when needed
  -> effect when authorized
  -> relationship update when authorized
  -> evidence
  -> persistence
  -> report
```

Intent, subject, protocol admission, authority, inputs, readiness, observation, validation, evidence, outcome, and report are universal. Interpretation, decision, mutation, relationship update, state effect, artifact effect, lifecycle effect, and recovery are conditional but governed when present.

### Protocol Model

Protocols make work repeatable, reviewable, recoverable, and safe to evolve. They define how intent becomes validated outcome.

Each protocol has one primary owner. Participants may supply sources, consume artifacts, validate evidence, or receive handoff authority, but ownership remains explicit.

```text
Defined -> active -> invoked -> running -> completed, paused, blocked, failed, cancelled, superseded, or retired
```

Protocol entry requires intent, subject identity, source state, relation validity, authority, readiness, and required inputs. Exit produces success, pause, block, failure, cancellation, completion, recovery target, or report-only outcome.

### State Model

State is a control summary, not the whole truth.

State constrains what may happen next and communicates whether a subject is ready, active, paused, blocked, cancelled, failed, completed, or awaiting recovery. A state is valid only when its entry evidence, artifact lifecycle, decision lineage, and freshness requirements still hold.

State entry requires initialization or an authorized interaction effect. State exit requires an authorized interaction effect, recovery protocol, or terminal/report-only classification.

State can be reconstructed from lifecycle, evidence, decisions, relationships, artifacts, and authority, but reconstruction alone is not enough for command admission. The system needs a named control condition so it can report, block, resume, replay, and recover without re-deriving the full semantic graph on every interaction.

### Decision Model

Decisions are authority-bearing artifacts when persisted.

A decision is created by an interaction that presents alternatives under a protocol. It may be generated by a model, parser, policy, route table, human, validator, or recovery review, but generation alone is not authority.

Decision authority requires evidence or an explicitly governed absence of evidence. Validation checks that the choice is allowed, the evidence is bound and fresh enough, the subject identity is correct, the lifecycle permits the choice, and the protocol owner accepts the result.

### Evidence Model

Evidence is validated, bound observation.

```text
Observation -> capture -> validation -> binding -> authority or blocker -> consumption -> history
```

Observation is raw output, source fact, failure context, human input, or repository condition. Capture records the observation with subject, sequence, source, and context. Validation checks parser rules, policy, artifact rules, lifecycle, freshness, identity, and invariants. Binding makes the evidence relevant to a decision, state effect, certification, blocker, or recovery target.

### Artifact Model

Artifacts are durable representations of understanding, authority, evidence, or history.

```text
Created -> owned -> validated -> promoted or retained -> mutated or consumed -> referenced -> superseded, archived, deleted, or retained as history
```

Creation requires an owner and role. Ownership determines mutation authority, lifecycle advancement, validation rules, and downstream consumers. Promotion changes authority, not merely location or file content. Deletion is a lifecycle outcome that requires evidence and must not erase decision lineage.

### Lifecycle Model

Everything that can change under the constitution has a lifecycle: systems, subjects, intents, sources, protocols, interactions, states, decisions, evidence, artifacts, relationships, authority grants, identities, invariants, reports, and recovery reviews.

The shared lifecycle grammar is:

```text
Absent or proposed
  -> captured or created
  -> prepared
  -> validated
  -> active, ready, or authoritative
  -> consumed, executing, or reported
  -> completed, blocked, cancelled, failed, superseded, archived, deleted, or retired
```

Each subject uses a subset of this grammar. Lifecycles compose by subject relationship but do not merge ownership.

### Invariant Model

Invariants are semantic constraints with violation semantics. They are evaluated at source capture, readiness, artifact preparation, interaction start, interpretation, validation, materialization, promotion, decision persistence, lifecycle advancement, state persistence, reporting, and recovery.

Violation may block, fail, pause, reject, supersede, request more evidence, or enter recovery. Recovery is allowed only when the invariant contract defines a supported repair.

Absolute invariants:

- No subjectless interaction.
- No intentless interaction.
- No authority without scope.
- No decision authority without validation.
- No evidence authority without binding.
- No artifact mutation without owner authority.
- No semantic relation without type, subject endpoints, authority, and evidence basis.
- No state effect without interaction authority.
- No recovery without preserved evidence and intent.
- No reporting field can become execution authority by display alone.

Contextual invariants:

- Which sources must be fresh.
- Which artifact shapes are valid.
- Which decisions are allowed.
- Which recovery intents are supported.
- Which pauses are terminal.
- Which human exceptions are accepted.
- Which lifecycle phases can synchronize.

### Temporal Model

The system understands time primarily as sequence, not wall-clock time.

- Current means latest valid agreement across state, lifecycle, artifacts, evidence, decisions, and source freshness for a subject.
- History is append-only lineage of interactions, decisions, evidence, artifact versions, lifecycle movements, reports, state effects, and recovery reviews.
- Future is represented by allowed interactions, pending protocols, next reportable actions, and possible recovery targets.
- Replay is valid only when identities, versions, source snapshots, evidence bindings, protocol rules, and policy versions still match or differences are explicitly accounted for.
- Cancellation is a temporal interruption. It must preserve enough dispatch state, evidence, and intent to resume safely or report that safe resume is unavailable.

### Information Model

Information moves through semantic transformations:

```text
Source or observation
  -> capture
  -> artifact view or evidence
  -> interpretation
  -> validation
  -> decision
  -> state effect, artifact mutation, relationship update, lifecycle movement, or report
  -> memory
  -> replay, projection, certification, recovery, distillation, or archival
```

Transformations:

- Observation becomes evidence through capture, validation, and binding.
- Source becomes an artifact view through scoped preparation and freshness tracking.
- Evidence becomes decision input through protocol acceptance.
- Decision becomes effect authority through validation.
- Interaction effect becomes state, lifecycle, relation, artifact, history, and report movement.
- Artifact becomes source for future views when its identity, version, and lifecycle are valid.
- History becomes knowledge when distilled into current authoritative artifacts.
- Knowledge becomes report when projected without creating mutation authority.
- Blocker evidence becomes recovery input when intent and eligibility are preserved.

### Semantic Boundaries

The strongest boundaries that must never blur are:

1. System vs machine: the semantic domain is not the operational implementation.
2. Subject vs representation: the governed thing is not identical to one artifact about it.
3. Intent vs authority: wanting work is not permission to accept its result.
4. Reference vs relation: an identifier or link is not a governed semantic relation.
5. Source vs artifact: ingestion preserves source origin but creates an artifact representation.
6. Source vs projection: prepared views do not replace source authority.
7. Observation vs evidence: raw output is not authority.
8. Evidence vs decision: proof constrains choice but is not the choice.
9. Decision vs suggestion: accepted, validated choice is authority; rationale is not.
10. Authority vs permission: ability to act is not acceptance of result.
11. Reporting vs execution: reports describe; they do not advance.
12. State vs lifecycle: workflow position and subject evolution must agree but remain distinct.
13. Protocol vs state machine: state graph is only one projection of governed work.
14. Candidate vs promoted artifact: generated content is not authoritative until accepted.
15. Artifact vs evidence: durable information is not proof unless bound as proof.
16. Completion claim vs certification: claim starts certification; it does not close work.
17. Memory vs authority owner: memory records; owners and protocols decide.
18. Blocker production vs recovery: many interactions can block; only recovery protocols can unblock.
19. Human exception vs model preference: exceptions require explicit human authority and scope.
20. Current understanding vs history: current artifacts can be superseded; history remains lineage.

### Universal Semantic Patterns

#### Control Pattern

```text
Load current memory -> classify intent and state -> authorize active work or report -> dispatch protocol
```

#### Interaction Pattern

```text
Intent -> subject -> protocol -> readiness -> execution -> observation -> interpretation -> validation -> decision or effect -> relation update -> evidence -> persistence -> report
```

#### Promotion Pattern

```text
Candidate artifact -> classify -> validate -> transfer authority -> advance artifact lifecycle -> record evidence -> persist state effect
```

#### Materialization Pattern

```text
Output -> extract -> validate identities and paths -> write artifacts -> write manifests and provenance -> advance lifecycle -> validate invariants -> record evidence
```

#### Routing Pattern

```text
Evidence or output -> parse/classify -> validate allowed route -> record decision -> enter target state or blocker
```

#### Certification Pattern

```text
Claim -> collect evidence -> independent evaluation -> policy validation -> route -> update subject lifecycle and memory
```

#### Recovery Pattern

```text
Block/failure/cancellation -> preserve evidence and intent -> review eligibility -> validate repair -> restore supported target or retain blocker
```

#### Distillation Pattern

```text
History/evidence -> evaluate durable relevance -> validate placement -> update current understanding -> preserve lineage
```

## Part V: Derived Architectural Implications

### Compression Test

The constitution deliberately compresses the recovered architecture.

| Recovered architecture | Semantic reconstruction |
|---|---|
| Machine | System instantiated with memory, command admission, and current state summary |
| 9 capabilities | Capability as ownership and authority boundary over subjects, protocols, artifacts, decisions, and evidence |
| 7 state machines plus machine memory | Protocols projected onto states and effects, with memory as artifact lineage |
| 21 conceptual states | State primitive with ready, active, paused, blocked, cancelled, failed, completed, and report-only roles |
| 32 canonical transitions | Interaction effects over state, lifecycle, artifacts, decisions, evidence, and memory |
| 6 transition archetypes | Universal semantic patterns under the interaction model |
| 14 execution stages | Interaction stages, most optional but governed |
| 13 artifact families | Artifact primitive with roles such as current, candidate, projection, evidence, manifest, decision, report, and archive |
| 4 decision classes | Decision primitive with strategic, operational, certification, and recovery scopes |
| Cross-artifact references | Governed relations with subject endpoints, type, evidence, lifecycle, and authority |
| Recovery flows | Recovery as a protocol over preserved evidence and intent |
| Preservation platform concepts | Authority, evidence, artifact, decision, lifecycle, recovery, and distillation semantics |

Estimated compression:

- Primitive compression: 16 prior primitives -> 14 primitives, with five prior foundations made derived and two missing foundations added.
- Capability compression: 9 observed capabilities -> 1 derived capability contract, about 9:1.
- Transition compression: 32 transitions -> interaction effects plus universal patterns.
- State compression: 21 states -> 1 state contract plus recurring roles.
- Artifact compression: 13 families -> 1 artifact contract plus role specializations.
- Relation compression: informal ownership, support, provenance, production, consumption, projection, and recovery links -> 1 governed relationship model.
- Lifecycle compression: many lifecycle vocabularies -> 1 lifecycle grammar specialized by subject.

The main compression is not fewer names. It is the recognition that subject, intent, authority, identity, lifecycle, evidence, protocol, and interaction explain the recovered architecture without changing the semantic foundation for future capabilities.

### Applying the Constitution to LoopRelay

LoopRelay is an operational machine under the broader System primitive.

- Its roadmap, repository, active epic, issues, generated plans, completion claims, and archived records are subjects.
- Its commands and human requests express intent.
- Its execution evidence, repository state, documents, and human instruction are sources or observations depending on interaction context.
- Its state machines are protocol projections.
- Its capabilities are authority and ownership boundaries over protocols.
- Its transitions are interaction effects.
- Its projections are artifacts with scoped preparation roles.
- Its references between plans, issues, artifacts, evidence, and decisions are governed relations only when typed, evidenced, and authorized.
- Its recovery paths are protocols over preserved blocker evidence and intent.
- Its memory is durable artifact and evidence lineage, not an independent authority owner.

### Future Capability Admission

A future capability is admissible when it can answer:

1. What subjects does it govern?
2. What intents does it admit?
3. Which protocols turn those intents into interactions?
4. Who owns the subjects, protocols, artifacts, decisions, and evidence?
5. What authority is created, consumed, transferred, or retired?
6. What observations become evidence, and under which validation rules?
7. What artifacts does it create, mutate, promote, archive, or delete?
8. Which states summarize protocol behavior, and what evidence makes them safe to advance?
9. What lifecycle movements can it cause?
10. What invariants can block, fail, or route recovery?
11. What reports describe outcomes without creating execution authority?
12. What recovery protocols preserve lineage when ordinary advancement fails?

If the capability can answer these questions, it can enter without changing the constitution. If it cannot, it is either outside the governed system or its semantics have not yet been recovered.

### Constitutional Validation Framework

Use this constitution to validate every future capability, protocol, architectural proposal, or implementation shortcut.

A proposal is constitutionally valid when it can answer:

1. What subjects does it introduce or affect?
2. What intent causes it to act?
3. Which protocol admits that intent?
4. What authority accepts the result?
5. What sources, observations, artifacts, and evidence does it consume or produce?
6. Which relationships does it create, update, supersede, or invalidate?
7. What decisions are made, and who validates them?
8. What lifecycle movement or state effect occurs?
9. What invariants can stop or redirect it?
10. What report describes the outcome without creating authority?
11. What evidence and intent remain if recovery is needed?
12. What history must remain replayable after supersession or archival?

Failure to answer these questions is diagnostic. Either the proposal is architecturally inconsistent, or it has revealed a genuine constitutional gap.

### Final Form

The constitution is intentionally smaller than the architecture it explains.

```text
Subject-bound intent
  -> protocol
  -> authorized interaction
  -> observation
  -> validated evidence
  -> accepted decision
  -> relation, state, lifecycle, artifact, report, or recovery effect
  -> durable lineage
```

Authority determines who may accept effects. Identity preserves what the effects are about. Relations explain how subjects affect one another. Lifecycle governs how subjects evolve. Invariants prevent unsafe evolution. Evidence makes history replayable. Protocols make future work admissible.

Everything else is implementation.
