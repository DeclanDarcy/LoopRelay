# Canonical Semantic Architecture Implementation Roadmap

## Source Authority

This roadmap realizes the semantic constitution available in this checkout as `semantic-constitution.md`. The request named `.semantic/semantic-constitution.md`; that path is not present in this repository, so the root constitution file is treated as the completed semantic constitution and highest architectural authority.

This roadmap is organized by semantic realization, not by current source files, namespaces, projects, or implementation conveniences.

## Executive Summary

The implementation strategy is to make the constitution directly expressible in software from the bottom up. The first stable layer must answer what the system governs, what remains identical through change, who owns change, which lifecycles are legal, and which invariants can stop advancement. Only after that can the system safely admit intent, capture sources, run protocols, validate interactions, bind evidence, accept decisions, mutate artifacts, advance state, report outcomes, certify completion, or recover from failure.

The dependency ordering is therefore:

```text
System boundary
  -> subject, identity, lifecycle, ownership, invariants
  -> governed relations
  -> intent and source capture
  -> protocol and authority admission
  -> interaction execution and evidence binding
  -> artifact, decision, state, lifecycle, and relation effects
  -> reporting, recovery, certification, distillation, and future capabilities
```

The architectural philosophy is conservative: every phase introduces durable semantic structure that should survive into the finished system. The roadmap avoids bridge architectures whose only purpose is to fit the current implementation. Existing state machines, CLIs, projections, prompts, ledgers, and artifact stores become migration surfaces only after the semantic authority they need has been established.

Expected implementation evolution:

- Early phases produce a small semantic kernel that can name subjects, owners, identities, lifecycles, authority scopes, invariants, relations, sources, intents, protocols, interactions, evidence, artifacts, decisions, and state summaries.
- Middle phases make existing LoopRelay behavior pass through that kernel: generated output becomes observation, accepted proof becomes evidence, proposed choices become decisions, candidate files become artifacts, transitions become interaction effects, and current workflow state becomes a validated control summary.
- Later phases make recovery, certification, distillation, and capability admission explicit so the system can evolve without adding new hidden foundations.

## Semantic Dependency Graph

The constitution's primitives and derived constructs depend on one another in this order.

```text
System
  -> bounded semantic domain
  -> subject registry
  -> protocol registry
  -> authority map
  -> invariant set
  -> durable memory rule

Subject
  -> identity
  -> lifecycle
  -> owner or ownerless rule
  -> valid authority scopes
  -> valid artifacts, decisions, evidence, reports, relations, and retirement

Identity
  -> subject continuity
  -> version, instance, snapshot, projection, replay, merge, split, and retirement rules

Lifecycle
  -> allowed subject evolution
  -> entry and exit evidence
  -> synchronization rules with state, artifact, decision, evidence, and authority lifecycles

Authority
  -> subject ownership and protocol ownership
  -> scoped ability to accept, decide, validate, mutate, promote, certify, report, recover, or retire
  -> cannot be inferred from process permission, file access, report display, or generated text

Invariant
  -> semantic constraints over subjects, relations, interactions, lifecycle, artifacts, decisions, evidence, state, and recovery
  -> violation semantics before recovery semantics

Relation
  -> governed fact between subject endpoints
  -> type, owner, authority scope, evidence basis, lifecycle, directionality, cardinality, invalidation, replay, and retirement

Intent
  -> subject-bound reason for action
  -> admission request, not acceptance authority

Source
  -> origin-bearing input with identity, version or snapshot, freshness, trust boundary, and consumer scope
  -> may be captured into artifacts without becoming artifact authority

Protocol
  -> owner, subject class, accepted intents, entry conditions, required inputs, evidence requirements, authority rules, decisions, effects, reporting, failure, and recovery
  -> state machines are projections of protocol behavior, not protocol authority

Interaction
  -> admitted intent over a subject through a protocol
  -> scoped authority, inputs, readiness, observation, interpretation, validation, evidence, outcome, persistence, report, and recoverable failure intent

Observation
  -> raw source fact, output, repository condition, failure context, or human input
  -> no authority until captured, validated, and bound as evidence

Evidence
  -> validated and bound observation
  -> supports decisions, state effects, artifact mutation, certification, blockers, and recovery targets

Decision
  -> accepted choice among allowed alternatives
  -> consumes evidence or governed absence of evidence
  -> produces authority for routing, state effects, lifecycle movement, artifact acceptance, certification, or recovery

Artifact
  -> durable information with owner, role, subject, version, provenance, lifecycle, mutation authority, consumers, supersession, archive, deletion, and history rules
  -> candidate artifacts require promotion or equivalent acceptance before replacing authority

State
  -> named control condition grounded in entry evidence, lifecycle agreement, decisions, artifacts, freshness, and authority
  -> constrains interaction admission and reporting without replacing lifecycle

Derived constructs
  -> machine: system + durable memory + command admission + current state summary
  -> capability: owned subjects + protocols + authority scopes + artifacts + decisions + evidence + invariants + recovery
  -> transition: interaction effect
  -> projection: artifact role with scoped view and freshness rule
  -> report: artifact or outcome that describes authority without creating it
  -> recovery: protocol over preserved evidence and intent
  -> certification: protocol over a completion claim, evidence, policy, and accepted result
  -> distillation: protocol that turns history and evidence into current authoritative understanding
```

The irreducible spine is:

```text
Subject-bound intent
  -> protocol
  -> authorized interaction
  -> observation
  -> validated evidence
  -> accepted decision
  -> relation, state, lifecycle, artifact, report, certification, recovery, or distillation effect
  -> durable lineage
```

## Architectural Dependency Graph

Implementation capabilities depend on the semantic graph as follows.

| Architectural capability | Depends on | Enables |
| --- | --- | --- |
| Semantic domain boundary | System identity and system boundary | Command admission, memory scoping, capability admission |
| Subject registry | Subject, identity, lifecycle, owner rules | Valid interaction subjects, artifact subjects, state subjects, recovery subjects |
| Identity and lineage model | Subject identity, version, instance, snapshot, projection, replay rules | Supersession, replay, archival, recovery target validation |
| Lifecycle grammar | Subject lifecycle, artifact lifecycle, evidence lifecycle, decision lifecycle, authority lifecycle | Readiness, completion, block status, archival, deletion, retirement |
| Authority map | Ownership, protocol authority, scoped grants, validators | Acceptance of decisions, artifact mutation, state effects, certification, recovery |
| Invariant catalog | Subject scope, evaluation points, violation semantics | Safe source capture, readiness, validation, promotion, persistence, recovery |
| Relation registry | Subject endpoints, relation types, evidence basis, invalidation | Provenance, support, consumption, production, projection, blocking, recovery links |
| Intent capture | Intent origin, subject, desired outcome, scope, retention | Protocol admission and replayable work purpose |
| Source capture | Source origin, snapshot/version, freshness, trust boundary | Evidence, prepared artifact views, projection freshness, replay |
| Protocol registry | Subject classes, accepted intents, authority, evidence, effects, exits | Governed interactions, state-machine projections, recovery protocols |
| Interaction envelope | Intent, subject, protocol, authority, inputs, readiness, observation, validation, outcome | Transition replacement, evidence binding, effect atomicity, reporting |
| Evidence ledger | Observation capture, validation, binding, authority level, freshness/hash, consumers | Decisions, blockers, certification, recovery, replayable history |
| Artifact authority model | Artifact owner, role, version, provenance, lifecycle, promotion, materialization | Candidate/current separation, projections, reports, archives, memory |
| Decision authority model | Alternatives, producer, evidence consumed, validator, persistence, replay | Routing, state effects, lifecycle effects, artifact acceptance, certification |
| State control model | Entry evidence, lifecycle agreement, allowed interactions, blocker semantics | Command admission, current status, pause/cancel/fail/complete reporting |
| Reporting surface | State, lifecycle, authority, evidence, artifact, decision summaries | Discoverability without authority leakage |
| Recovery surface | Blocker evidence, original intent, eligibility, target-state rule, fallback | Safe unblock, cancellation resume, stale artifact repair |
| Certification surface | Completion claim, evidence collection, independent evaluation, policy validation | Authoritative completion and closed lifecycle movement |
| Distillation surface | History, evidence, durable relevance, placement authority, lineage | Current understanding without erasing history |
| Capability admission surface | Declared subjects, protocols, authority, evidence, artifacts, decisions, invariants, recovery | Future extensibility under the same constitution |

## Minimal Semantic Kernel

The smallest implementation capable of expressing the constitution is a durable semantic kernel with these responsibilities:

1. System boundary and constitution authority
   - Names the governed semantic domain.
   - Declares memory, command admission, reporting, recovery, and future capability boundaries.

2. Subject identity and ownership
   - Names every governed thing as a subject.
   - Preserves identity through versions, snapshots, projections, observations, evidence, decisions, lifecycle changes, archival, and replay.
   - Records one primary owner or an explicit ownerless rule.

3. Lifecycle and state compatibility
   - Defines allowed subject evolution.
   - Records entry and exit evidence requirements.
   - Separates lifecycle from state while allowing them to agree.

4. Authority and invariant contracts
   - Records scoped authority grants and validators.
   - Declares invariant evaluation points and violation semantics.
   - Makes permission visibly distinct from acceptance authority.

5. Governed relationship facts
   - Represents ownership, representation, provenance, support, production, consumption, governance, authorization, constraint, supersession, projection, blocking, and recovery relations.
   - Rejects untyped references as semantic authority.

6. Intent and source capture
   - Preserves why work was requested.
   - Preserves origin, version, freshness, trust boundary, and consumer scope of source information.

7. Protocol and interaction envelope
   - Admits intent only through a protocol.
   - Records interaction identity, authority, inputs, readiness, observations, validation, outcome, evidence, persistence, report, and recoverable failure intent.

8. Evidence, decision, and artifact records
   - Captures observation as evidence only after validation and binding.
   - Persists decisions as authority-bearing artifacts only after protocol validation.
   - Represents artifacts by owner, role, subject, version, provenance, lifecycle, mutation authority, consumers, and supersession.

9. Durable lineage
   - Preserves the sequence of interactions, evidence, decisions, artifact versions, lifecycle movement, reports, state effects, recovery reviews, supersessions, archival, and deletion events.

The kernel deliberately excludes broad automation, optimized projection generation, UI composition, provider-specific execution strategy, speculative capability plugins, and domain-specific convenience workflows. Those are admissible only after they can declare their subjects, protocols, authority, evidence, lifecycle, artifacts, decisions, invariants, relations, reporting, and recovery semantics under the kernel.

## Vertical Slice Opportunities

These slices can be implemented early because they exercise multiple semantic primitives while remaining architecturally clean.

| Slice | Semantic path exercised | Why it is valuable early |
| --- | --- | --- |
| Source ingestion to governed artifact | Source -> capture -> artifact representation -> provenance relation -> freshness rule -> report | Proves source/artifact separation before any generated view is trusted |
| Candidate artifact promotion | Intent -> protocol -> candidate artifact -> validation -> authority transfer -> lifecycle advancement -> evidence -> state effect | Proves generated output cannot replace authority by being written |
| Decision route from evidence | Observation -> evidence -> alternatives -> validator -> persisted decision -> route/state effect -> report | Proves decision authority is protocol acceptance, not parser plausibility |
| Report-only projection | Current state/lifecycle/artifacts/evidence -> projection artifact -> report | Proves reports and projections describe authority without creating it |
| Blocker preservation and recovery | Failed interaction -> blocker evidence -> recovery intent -> eligibility -> validated repair -> target state or retained blocker | Proves recovery is not retry and cannot invent missing authority |
| Completion certification | Completion claim -> evidence collection -> independent evaluation -> policy validation -> decision -> lifecycle movement | Proves completion is certified, not merely claimed |
| History distillation | Interaction history/evidence -> durable relevance review -> current understanding artifact -> lineage preservation | Proves current understanding can evolve without rewriting history |

## High-Risk Foundations

The following foundations should appear early because incorrect implementation would create long-lived architectural debt.

| Foundation | Risk if delayed or implemented incorrectly | Roadmap response |
| --- | --- | --- |
| Subject identity | Versions, snapshots, projections, and evidence can silently point at different things | Establish subject identity in Phase 1 before protocol or artifact authority |
| Authority vs permission | File access, command success, report display, or generated text can become implicit acceptance | Establish scoped authority and validators before interaction execution |
| Source vs artifact vs projection | Ingested or prepared views can become false source authority | Establish source capture and provenance before artifact promotion |
| Relation governance | Paths, links, and textual mentions can become informal semantic facts | Establish relation contracts before derived projections and recovery links |
| Evidence binding | Raw observations can drive decisions without validation | Establish evidence lifecycle before decision effects |
| Protocol before state machine | State transitions can become authority without evidence, ownership, recovery, or lifecycle rules | Establish protocol admission before state effect modeling |
| Artifact ownership and promotion | Candidate outputs can overwrite current authority without accepted transfer | Establish artifact authority before materialization and projection expansion |
| State and lifecycle separation | Current workflow status can contradict artifact, evidence, or decision lifecycle | Establish state/lifecycle agreement before command admission depends on state |
| Recovery target validation | Retry can erase failure context or invent a safe target | Establish recovery only after blocker evidence and target-state semantics exist |
| Reporting boundary | UI or report fields can become hidden execution authority | Establish report-only semantics before broad projection surfaces |
| Supersession and archival | New versions can rewrite history or make replay impossible | Establish durable lineage before certification and distillation |

## Complete Multi-Phase Roadmap

### Phase Overview

| Phase | Title | Purpose | Major semantic concepts realized | Major implementation capabilities realized | Dependencies | Completion criteria |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Constitutional Subject Kernel | Make governed things identifiable, owned, lifecycle-bearing, constrained subjects | System, subject, identity, lifecycle, authority, invariant, relation contract | Boundary, subject registry, ownership, identity, lifecycle grammar, invariant declarations, relation vocabulary | Constitution | HITL can identify every governed thing by subject, owner, identity, lifecycle, authority scope, and relation rules |
| 2 | Intent, Source, and Provenance Admission | Make work purpose and source authority explicit before action | Intent, source, source/artifact distinction, provenance, freshness | Intent capture, source capture, source snapshots, ingestion records, provenance relations | Phase 1 | HITL can trace every admitted input to origin, subject, freshness, trust boundary, and captured representation |
| 3 | Protocol and Authority Admission | Make protocols the only path from intent to governed work | Protocol, authority grants, admission, readiness, invariant checks | Protocol registry, authority validation, admission outcome, report-only rejection | Phases 1-2 | HITL can show that no interaction starts without protocol admission and scoped authority |
| 4 | Governed Interaction and Evidence Lineage | Make interactions, observations, evidence, and durable lineage first-class | Interaction, observation, evidence, validation, binding, memory lineage | Interaction envelope, evidence ledger, validation/binding rules, append-only history | Phases 1-3 | HITL can replay why an interaction outcome is believed or why it blocked |
| 5 | Artifact Authority and Projection Lifecycle | Make artifact roles, materialization, promotion, and projection freshness explicit | Artifact, promotion, materialization, projection, supersession, archival | Artifact authority model, candidate/current separation, materialization evidence, projection invalidation | Phases 1-4 | HITL can prove no candidate artifact replaced authority without validation, promotion, evidence, and lineage |
| 6 | Decision Authority and Effect Routing | Make accepted choices the source of route, lifecycle, artifact, relation, and state effects | Decision, alternatives, validators, effect authority, routing | Decision persistence, decision class ownership, effect authorization, route evidence | Phases 1-5 | HITL can distinguish suggestion, parser output, human input, and accepted decision authority |
| 7 | State, Lifecycle, and Current Memory Agreement | Make current control state a validated summary, not an independent truth | State, lifecycle synchronization, current, history, replay | State summaries, entry evidence, command admission checks, lifecycle agreement, current memory | Phases 1-6 | HITL can reconstruct each state from evidence, decisions, lifecycle, artifacts, and authority |
| 8 | Blocker and Recovery Protocols | Make failed, blocked, cancelled, stale, or invalid conditions recoverable only by protocol | Blocker evidence, recovery intent, eligibility, target state, retained blocker | Recovery protocols, cancellation resume semantics, stale repair semantics, blocker reports | Phases 1-7 | HITL can show recovery preserved original evidence and restored only a supported target |
| 9 | Certification and Distillation | Make completion and current understanding authoritative without rewriting history | Completion claim, certification, distillation, archive, current understanding | Certification protocol, independent evaluation, policy decision, distillation lineage | Phases 1-8 | HITL can show completion was certified and distilled knowledge preserved source lineage |
| 10 | Capability Admission and Semantic Extensibility | Make future capabilities enter through declared constitutional contracts | Capability, future protocols, capability-owned subjects, capability recovery | Capability admission surface, capability conformance review, extensibility governance | Phases 1-9 | HITL can accept or reject a new capability by the constitution without adding new foundations |

### Phase 1: Constitutional Subject Kernel

#### Goal

Establish the smallest durable semantic foundation: the system boundary, governed subjects, identity continuity, ownership, lifecycle grammar, authority scopes, invariant declarations, and the relation contract.

#### Semantic Objectives

- System becomes the bounded semantic domain.
- Subject becomes the unit of governance.
- Identity becomes the continuity rule for every subject.
- Lifecycle becomes the allowed evolution of a subject.
- Authority becomes scoped acceptance power, not operational permission.
- Invariants become named semantic constraints with violation semantics.
- Relations become governed facts rather than informal references.

#### Architectural Objectives

- A governed domain can name what is inside and outside the system.
- Every governed thing can be identified, owned, constrained, evolved, related, reported, and retired.
- The system can distinguish subject identity from artifact representation.
- The system can record authority scopes before any interaction tries to exercise them.

#### Scope

Implemented:

- System boundary and domain identity.
- Subject contract and subject registry.
- Identity continuity rules for versions, instances, snapshots, projections, observations, evidence, decisions, archival, and replay.
- Lifecycle grammar shared by mutable subjects.
- Authority grant shape and ownership relation.
- Invariant declaration shape with evaluation points and violation semantics.
- Relation contract and core relation type vocabulary.

Explicitly not implemented:

- Execution protocols.
- Decision routing.
- Artifact promotion.
- Recovery.
- Certification.
- Broad reporting views.
- Domain-specific capability automation.

#### Core Deliverables

- A constitutional subject model that can represent repositories, roadmaps, plans, issues, protocols, interactions, artifacts, states, decisions, evidence records, authority grants, lifecycle entries, reports, and recovery reviews as subjects.
- A stable identity and lineage model that prevents version, snapshot, projection, and artifact representation from replacing subject identity.
- A relation model that can later represent ownership, representation, provenance, support, production, consumption, governance, authorization, constraint, supersession, projection, blocking, and recovery.
- An authority and invariant foundation that can reject implicit authority before workflows rely on it.

#### Dependency Justification

Every later concept depends on subject identity, lifecycle, ownership, authority, and invariant scope. Without this phase, protocols would lack subject classes, evidence would lack binding targets, decisions would lack owners and validators, artifacts would lack mutation authority, and recovery would lack a stable blocked subject.

#### Risks

- Making identity too representation-specific would cause projections, snapshots, and artifacts to become hidden subject replacements.
- Making authority too operational would collapse permission and acceptance.
- Treating relations as mere references would leave provenance, support, recovery, and projection semantics informal.
- Overfitting lifecycle vocabulary to current state names would preserve accidental workflow structure.

#### Completion Criteria

HITL can select any governed thing and answer:

- What subject is this?
- What identity persists through version, snapshot, projection, evidence, and archival?
- Who owns it or what ownerless rule applies?
- Which lifecycle grammar governs it?
- Which authority scopes may affect it?
- Which invariants constrain it?
- Which relation types may connect it to other subjects?

### Phase 2: Intent, Source, and Provenance Admission

#### Goal

Make the reason for work and the authority of source information explicit before any protocol can act.

#### Semantic Objectives

- Intent becomes captured, subject-bound direction.
- Source becomes origin-bearing input with identity, snapshot or version, freshness, trust boundary, and consumer scope.
- Ingestion becomes an authority-preserving relation from source to artifact representation.
- Provenance becomes a governed relation, not a path convention.
- Source, artifact, and projection boundaries become enforceable.

#### Architectural Objectives

- The system can distinguish passive knowledge from governed work.
- Human instruction, repository state, prior artifacts, command output, policy, and external records can enter as sources or observations with correct origin.
- Captured source representations can be stored without becoming source authority by storage alone.
- Freshness and trust boundaries are visible before protocol admission.

#### Scope

Implemented:

- Intent capture and retention rules.
- Source identity, snapshot/version, freshness, trust boundary, authority level, and consumer scope.
- Source ingestion into artifact representation with preserved source identity and provenance.
- Provenance and representation relations between source subjects, captured artifacts, and future consumers.

Explicitly not implemented:

- Decision acceptance from source facts.
- Artifact promotion beyond source capture.
- Protocol-specific validation.
- Projection generation as a broad capability.
- Recovery from stale sources.

#### Core Deliverables

- Intent records that preserve origin, subject, desired outcome, scope, admission status, satisfaction, supersession, cancellation, block, and retirement.
- Source records that preserve origin and freshness independently from any stored artifact.
- Ingestion artifacts that carry source identity, source version, capture context, freshness, provenance, owner, role, and lifecycle.
- Provenance relations that make every captured source representation traceable without merging source and artifact authority.

#### Dependency Justification

Intent and source capture require subjects, identities, owners, lifecycles, authority scopes, invariants, and relations from Phase 1. Protocols cannot safely admit work until they can inspect intent and source freshness.

#### Risks

- Treating a prepared view as the source would make projection freshness impossible to reason about.
- Treating a captured artifact as authoritative merely because it exists would bypass validation and binding.
- Losing human instruction scope would create broad human exception authority.
- Capturing source data without freshness would make replay and recovery unsafe.

#### Completion Criteria

HITL can trace any admitted input or work request to:

- origin;
- subject;
- desired outcome or fact-bearing scope;
- captured version or snapshot;
- freshness and trust rule;
- artifact representation, if one exists;
- provenance relation;
- authority level and consumer scope.

### Phase 3: Protocol and Authority Admission

#### Goal

Make protocol admission the only valid path from captured intent to governed work.

#### Semantic Objectives

- Protocol becomes the governed form that admits intent into interactions.
- Authority grants become validated against owner, subject, scope, lifecycle, source freshness, relation validity, and invariants.
- Readiness becomes a semantic admission result, not a convenience check.
- Report-only outcomes become explicit non-mutating protocol exits.

#### Architectural Objectives

- The system can decide whether a requested interaction is admissible before execution or review begins.
- Each protocol can declare owner, subject class, accepted intents, participants, entry conditions, inputs, evidence requirements, allowed decisions, effects, reporting, failure, and recovery.
- State machines can be treated as projections of protocols rather than authority owners.
- Command-facing behavior can deny work without confusing denial with failure.

#### Scope

Implemented:

- Protocol contract registry.
- Protocol owner and subject-class model.
- Accepted intent mapping.
- Entry conditions and readiness checks.
- Authority grant evaluation.
- Protocol admission outcomes: admitted, report-only, denied, blocked, failed, cancelled, or unsupported.
- Invariant checks at admission.

Explicitly not implemented:

- Full interaction execution.
- Evidence binding beyond admission diagnostics.
- Decision persistence.
- Artifact mutation.
- State effects.
- Recovery protocols beyond declaring recovery semantics.

#### Core Deliverables

- Protocol definitions that own how intent becomes interaction.
- Authority admission records that show which grant was exercised or why authority was absent.
- Readiness reports that describe admission status without granting execution authority.
- A protocol projection rule that prevents state graphs from replacing protocol authority.

#### Dependency Justification

Protocols depend on subject classes, intent capture, source freshness, authority scopes, relation validity, lifecycle rules, and invariants. They must exist before interactions, evidence, decisions, artifacts, and state effects can be considered authoritative.

#### Risks

- Letting commands call implementation behavior directly would recreate ungoverned action paths.
- Encoding readiness as current state alone would erase evidence and lifecycle requirements.
- Allowing protocol ownership to be inferred from caller identity would weaken authority.
- Treating denial as failure would make reporting and recovery inaccurate.

#### Completion Criteria

HITL can inspect any attempted work and determine:

- which intent requested it;
- which subject it concerns;
- which protocol admitted or rejected it;
- which authority scope was exercised or missing;
- which sources and relations were checked;
- which invariants were evaluated;
- whether the result was active work, report-only, denied, blocked, failed, cancelled, or unsupported.

### Phase 4: Governed Interaction and Evidence Lineage

#### Goal

Make each governed occurrence explicit from admission through observation, validation, evidence binding, persistence, and report.

#### Semantic Objectives

- Interaction becomes the atomic occurrence of governed work.
- Observation remains raw until captured, validated, and bound.
- Evidence becomes validated, bound observation with authority level and consumer scope.
- Durable memory becomes lineage of authority exercised by owners, not an authority owner.

#### Architectural Objectives

- Every interaction can be replayed from intent, subject, protocol, authority, inputs, observations, validation, evidence, outcome, and report.
- Raw model output, command output, parser output, and report text remain observations until evidence binding.
- Blocker evidence can preserve the reason advancement stopped.
- The system can distinguish interaction failure, validation rejection, blocker creation, and report-only completion.

#### Scope

Implemented:

- Interaction identity and envelope.
- Input snapshot capture.
- Observation capture.
- Interpretation and validation rule hooks declared by protocol.
- Evidence record contract with validator, binding, freshness/hash marker, authority level, consumers, retention, replay, and archival semantics.
- Durable lineage for interactions, evidence, reports, and blocker records.

Explicitly not implemented:

- Broad artifact promotion.
- Decision effect routing.
- State effect persistence except as recorded outcomes without admission dependency.
- Recovery eligibility review.
- Completion certification.

#### Core Deliverables

- Interaction records that make transition-like behavior reconstructable as protocol-governed effects.
- Evidence records that can support decisions, state effects, artifact mutation, blockers, certification, and recovery.
- A memory lineage model that records what happened and why without becoming the owner of acceptance.
- Failure and blocker records that preserve enough evidence for later recovery without reinterpreting failure as success.

#### Dependency Justification

Interactions require admitted protocol authority. Evidence requires observation, subject identity, validators, bindings, authority scopes, and lifecycle. This phase must precede decision authority, artifact promotion, state advancement, certification, and recovery because all of those consume evidence.

#### Risks

- Recording output without binding would create evidence-shaped logs with no authority.
- Letting memory replace owners would make persisted records authoritative by existence.
- Failing to preserve input snapshots would break replay.
- Failing to distinguish blockers from failures would make recovery unsound.

#### Completion Criteria

HITL can inspect any interaction and answer:

- what intent and subject caused it;
- which protocol and authority admitted it;
- what inputs and source snapshots it consumed;
- what observations were produced;
- which observations became evidence;
- what validation and binding made the evidence usable;
- what outcome was recorded;
- what report described the outcome;
- what evidence remains if recovery is needed.

### Phase 5: Artifact Authority and Projection Lifecycle

#### Goal

Make artifacts durable semantic objects with owners, roles, versions, provenance, validation, promotion, materialization, projection freshness, supersession, archival, and deletion semantics.

#### Semantic Objectives

- Artifact becomes a first-class subject role for durable information.
- Candidate, current, projection, evidence, manifest, decision, report, archive, and memory roles become explicit specializations.
- Promotion becomes authority transfer, not file movement.
- Materialization becomes an interaction effect with provenance and lifecycle advancement.
- Projection becomes an artifact role, never source authority.

#### Architectural Objectives

- Generated or extracted output cannot replace authoritative artifacts without validation and promotion.
- Artifact consumers cannot mutate by consumption.
- Projection freshness and invalidation can be evaluated against source versions and lifecycle.
- Supersession, archival, and deletion preserve lineage rather than erasing semantic history.

#### Scope

Implemented:

- Artifact identity, owner, role, subject, representation, validity rules, version, provenance, lifecycle, mutation authority, consumers, reference rules, supersession, archival, and deletion semantics.
- Candidate/current distinction.
- Materialization effects with identity and path validation.
- Promotion effects with authority transfer and evidence.
- Projection artifact contract with source set, source versions, owner, generation protocol, freshness rule, validation rule, consumers, invalidation, and retention.

Explicitly not implemented:

- Domain-specific projection ecosystems beyond the artifact role contract.
- Decision classes as authority owners.
- State admission based on current artifacts.
- Certification protocols.
- Distillation protocols.

#### Core Deliverables

- Artifact records that make file-backed, generated, report, manifest, projection, decision, evidence, archive, and memory artifacts semantically comparable without merging their roles.
- Promotion records that prove candidate artifacts became authoritative through accepted authority transfer.
- Materialization records that prove written artifacts match declared subject identities, roles, paths, owners, provenance, and lifecycles.
- Projection records that prove a prepared view is scoped, freshness-bound, and non-authoritative as a source.

#### Dependency Justification

Artifact authority depends on subject identity, ownership, lifecycle, relations, source provenance, protocol admission, interactions, and evidence. It must precede decision effect routing and state agreement because decisions and state summaries consume authoritative artifacts.

#### Risks

- Preserving current file paths as authority would let storage layout replace semantic ownership.
- Treating generation success as promotion would bypass validation.
- Letting projections become source truth would corrupt freshness and replay.
- Allowing deletion as filesystem erasure would break semantic history.

#### Completion Criteria

HITL can inspect any artifact and determine:

- owner;
- subject;
- semantic role;
- representation key;
- version;
- provenance;
- lifecycle;
- mutation authority;
- consumers;
- whether it is candidate, current, projection, report, evidence, decision, manifest, archive, or memory;
- how it was materialized, promoted, superseded, archived, deleted, or retained.

### Phase 6: Decision Authority and Effect Routing

#### Goal

Make accepted choices the only source of route, artifact, relation, lifecycle, certification, recovery, and state-effect authority where a choice is required.

#### Semantic Objectives

- Decision becomes an accepted choice among allowed alternatives for a subject.
- Decision authority comes from protocol validation over evidence.
- Suggestions, rationale, parser classifications, model text, human comments, and reports remain non-authoritative until accepted by the owning protocol.
- Effects become authorized only through validated decisions or protocols that explicitly allow direct effects.

#### Architectural Objectives

- The system can distinguish generated recommendation from accepted decision.
- Strategic, operational, certification, and recovery decision scopes can share one decision contract without becoming one decision class.
- Routing, state effects, lifecycle advancement, artifact acceptance, relation updates, and blockers can cite the decision that authorized them.
- Human exceptions can be captured as scoped authority with evidence, not implicit override.

#### Scope

Implemented:

- Decision contract with subject, alternatives, producer, authority source, evidence consumed, validator, result, persistence, consumers, replay, supersession, and retirement.
- Decision class ownership and validator model.
- Decision persistence as authority-bearing artifact.
- Effect routing rules from accepted decisions to allowed relation, lifecycle, artifact, state, report, blocker, certification, or recovery outcomes.

Explicitly not implemented:

- Full state command admission based on current state.
- Recovery eligibility review beyond decision class declaration.
- Completion certification execution.
- Distillation into current understanding.

#### Core Deliverables

- Decision records that bind alternatives, evidence, validator, selected result, authority, and consumers.
- Effect authorization records that distinguish decision-authorized effects from direct protocol effects.
- Routing semantics that show why a subject advanced, blocked, failed, paused, cancelled, completed, or remained report-only.
- Human exception records with explicit origin, scope, evidence, expiry or retirement, and affected subject.

#### Dependency Justification

Decisions require protocols, interactions, evidence, authority, artifacts, lifecycle rules, and invariants. They must precede state agreement and recovery because state effects and recovery targets often depend on accepted route decisions.

#### Risks

- Treating plausible output as a decision would bypass owner validation.
- Persisting decisions without evidence would create stored suggestions with false authority.
- Combining all decision scopes too early would obscure ownership and validators.
- Letting human exception text act globally would overgrant authority.

#### Completion Criteria

HITL can inspect any route, artifact acceptance, lifecycle movement, blocker, certification result, recovery target, or state effect and identify:

- whether a decision was required;
- the allowed alternatives;
- the evidence consumed;
- the validator;
- the accepted result;
- the authority source;
- the persisted decision record;
- the consumers and replay rules.

### Phase 7: State, Lifecycle, and Current Memory Agreement

#### Goal

Make current state a validated control summary that agrees with lifecycle, evidence, decisions, artifacts, authority, relations, and freshness.

#### Semantic Objectives

- State becomes a named control condition of a subject.
- State remains distinct from lifecycle while agreeing where readiness, execution, completion, block, failure, cancellation, pause, or recovery status overlaps.
- Current becomes latest valid agreement across state, lifecycle, artifacts, evidence, decisions, and source freshness.
- History remains append-only lineage.

#### Architectural Objectives

- Command admission can depend on state without re-deriving the entire semantic graph each time.
- State entry and exit can cite authorized interaction effects and entry evidence.
- State reports can show readiness, active work, blocked status, cancellation, failure, completion, and recovery eligibility without creating authority.
- Replay can reconstruct why current state is safe or unsafe.

#### Scope

Implemented:

- State contract with owner, subject, entry evidence, allowed interactions, readiness meaning, active or report-only classification, exit conditions, lifecycle relationship, blocker semantics, and recovery relationship.
- State entry and exit effects from authorized interactions.
- Current-memory agreement checks across state, lifecycle, artifacts, evidence, decisions, authority, relations, and freshness.
- Command admission use of state as control summary.
- Report-only state classification.

Explicitly not implemented:

- Recovery protocols that restore target states.
- Certification closure.
- Distillation into current understanding.
- New capability admission.

#### Core Deliverables

- State records that are named, persisted, validated, reportable, replayable, and recoverable.
- Lifecycle synchronization records that prevent state from contradicting artifact, evidence, decision, or authority lifecycle.
- Current-memory summaries that are reconstructable from durable lineage but efficient enough for command admission and reporting.
- Readiness and blocked reports that describe current semantic condition without advancing it.

#### Dependency Justification

State requires decisions, evidence, artifacts, lifecycle, authority, and protocol effects. It must come after decision and artifact authority because state entry without accepted evidence and artifacts would create false readiness.

#### Risks

- Reifying state as the whole truth would erase lifecycle, artifacts, evidence, and decisions.
- Allowing persisted state without entry evidence would make advancement unsafe.
- Treating reports as state changes would violate reporting boundaries.
- Computing current from cache freshness alone would confuse cache mechanics with semantic freshness.

#### Completion Criteria

HITL can inspect any current state and show:

- subject and owner;
- state name and classification;
- entry evidence;
- authorized interaction effect;
- lifecycle relationship;
- artifact and decision support;
- freshness requirements;
- allowed next interactions;
- report-only or active status;
- blocker and recovery relationship;
- replay path from durable lineage.

### Phase 8: Blocker and Recovery Protocols

#### Goal

Make blocked, failed, cancelled, stale, or invalid conditions recoverable only through protocols that preserve original evidence and validate supported target states.

#### Semantic Objectives

- Blocker evidence becomes a first-class support relation preventing advancement.
- Recovery becomes a protocol over preserved evidence and intent.
- Recovery eligibility, repaired input, target state, fallback, report, and lineage preservation become explicit.
- Retry becomes distinct from recovery.

#### Architectural Objectives

- Failure, cancellation, block, stale source, invalid artifact, and unsafe state can preserve enough evidence for later review.
- Recovery can restore only supported targets, retain blockers, or report unrecoverable conditions.
- Cancellation can preserve dispatch state and intent when safe resume is possible.
- Recovery reports can explain why recovery did or did not advance without erasing original failure.

#### Scope

Implemented:

- Blocker relation and blocker evidence model.
- Recovery protocol contract with blocked subject, original intent, evidence set, eligibility, reviewer, repaired input, validation, target state, fallback, report, and lineage preservation.
- Recovery authority and decision classes.
- Recovery target validation against state, lifecycle, artifact, evidence, authority, and invariant conditions.
- Cancellation and stale-condition preservation rules.

Explicitly not implemented:

- Completion certification.
- Distillation of recovery history into current understanding.
- New capability admission beyond recovery declarations.

#### Core Deliverables

- Blocker records that preserve why advancement stopped and what evidence supports the stop.
- Recovery reviews that can accept repaired input, restore a supported state, retain a blocker, or report unrecoverable status.
- Recovery lineage that preserves original evidence, recovery intent, reviewer, eligibility, target decision, and final outcome.
- Cancellation semantics that do not pretend an interrupted action completed.

#### Dependency Justification

Recovery requires state, lifecycle, artifacts, evidence, decisions, authority, invariants, protocols, and original intent. It cannot safely precede current-state agreement because target restoration depends on known valid state semantics.

#### Risks

- Treating recovery as retry would allow missing evidence to be ignored.
- Letting recovery choose arbitrary targets would invent state authority.
- Erasing original blockers would make later replay false.
- Reinterpreting unsupported blockers as success would break certification and current memory.

#### Completion Criteria

HITL can inspect any recovery and show:

- original blocked, failed, cancelled, stale, or invalid condition;
- preserved intent;
- preserved evidence;
- eligibility rule;
- reviewer and authority;
- repaired input, if any;
- target state rule;
- validation result;
- retained blocker or restored supported target;
- report;
- lineage that keeps original evidence intact.

### Phase 9: Certification and Distillation

#### Goal

Make completion and current understanding authoritative through certification and distillation while preserving history.

#### Semantic Objectives

- Completion claim becomes an input to certification, not completion authority.
- Certification becomes a protocol over claim, evidence, independent evaluation, policy validation, route decision, lifecycle update, and memory.
- Distillation becomes a protocol that turns durable history and evidence into current authoritative understanding.
- Current understanding remains versioned and replayable; history is not rewritten.

#### Architectural Objectives

- Work can close only when certification accepts the completion claim or an owner authority explicitly supports closure.
- Completion reports can distinguish claim, evaluation, certification decision, lifecycle movement, and archived history.
- Durable history can be distilled into current artifacts without losing source identity, evidence basis, decisions, or supersession.
- Archives preserve lineage and do not become semantic erasure.

#### Scope

Implemented:

- Completion claim subject and lifecycle.
- Certification protocol with evidence collection, independent evaluation, policy validation, decision, route, lifecycle, memory, and report effects.
- Certification evidence and decision records.
- Distillation protocol with durable relevance evaluation, placement validation, current understanding artifact update, supersession, and lineage preservation.
- Archive and history retention semantics aligned with artifact and lifecycle models.

Explicitly not implemented:

- Speculative analytics.
- Automated correctness scoring beyond declared certification policies.
- Capability-specific shortcuts around certification.
- New semantic foundations for future capabilities.

#### Core Deliverables

- Certification records that prove why a completion claim became accepted, rejected, blocked, or routed to recovery.
- Lifecycle movement that distinguishes completed, blocked, failed, cancelled, superseded, archived, and retired.
- Distilled current-understanding artifacts that cite history, evidence, decisions, sources, and superseded versions.
- Archive records that retain semantic history after supersession or closure.

#### Dependency Justification

Certification consumes evidence, decisions, artifacts, state, lifecycle, authority, reports, and recovery results. Distillation consumes durable history and produces authoritative current understanding. Both require all prior phases to avoid closing or rewriting work unsafely.

#### Risks

- Treating completion claims as closure would bypass evidence and policy.
- Letting distillation summarize without lineage would rewrite history.
- Allowing archive movement without lifecycle authority would erase semantic accountability.
- Certifying based on report text rather than evidence would confuse reporting and authority.

#### Completion Criteria

HITL can inspect any completed or distilled subject and show:

- original completion claim or distillation intent;
- evidence set;
- independent evaluation or durable relevance review;
- policy validation;
- accepted decision;
- lifecycle movement;
- current artifact or archive effect;
- report;
- preserved history and replay path.

### Phase 10: Capability Admission and Semantic Extensibility

#### Goal

Make every future capability enter by declaring its constitutional semantics instead of adding new hidden foundations.

#### Semantic Objectives

- Capability becomes a derived construct reconstructed from owned subjects, protocols, authority scopes, artifacts, decisions, evidence responsibilities, lifecycle obligations, invariants, handoffs, and recovery protocols.
- Future capability admission uses the constitution's validation framework.
- Capability boundaries become explicit ownership and authority boundaries rather than implementation modules.

#### Architectural Objectives

- New capabilities can be accepted without changing semantic foundations.
- Capability proposals can be rejected when they cannot identify subjects, intent, protocols, authority, evidence, artifacts, relations, lifecycle, invariants, reporting, recovery, and replay.
- Existing capabilities can be migrated into declared semantic ownership boundaries.
- Extension work can remain future-proof without speculative infrastructure.

#### Scope

Implemented:

- Capability declaration contract.
- Capability admission review using the constitutional validation questions.
- Capability-owned protocol, artifact, evidence, decision, lifecycle, invariant, reporting, and recovery maps.
- Capability conformance reporting.
- Capability retirement and supersession semantics.

Explicitly not implemented:

- New primitives.
- Capability-specific bypasses of authority, evidence, recovery, certification, or lifecycle.
- Framework or library mandates.
- Speculative plugin surfaces that are not backed by declared subjects and protocols.

#### Core Deliverables

- A future-capability admission surface that asks the constitution's twelve validation questions before architecture changes are accepted.
- Capability records that identify owned subjects, identities, intents, protocols, authority grants, artifacts, decisions, evidence, relation types, states, lifecycles, invariants, reporting, recovery, replay, and retirement.
- Conformance reports that describe whether a capability is constitutional without creating authority to execute it.
- Retirement rules for capability versions and superseded capability boundaries.

#### Dependency Justification

Capability admission is intentionally last because a capability is reconstructed from all primitive contracts and derived contracts. It cannot be meaningful until subjects, protocols, authority, evidence, artifacts, decisions, lifecycle, state, recovery, certification, and lineage exist.

#### Risks

- Treating capability boundaries as project or namespace boundaries would preserve accidental implementation structure.
- Accepting capabilities without recovery semantics would leave future blockers unrecoverable.
- Allowing capability-specific shortcuts would fracture the constitution.
- Overbuilding speculative extension mechanisms would create architecture not demanded by semantic authority.

#### Completion Criteria

HITL can evaluate any proposed future capability and answer:

- What subjects does it govern?
- What intents does it admit?
- Which protocols turn those intents into interactions?
- Who owns subjects, protocols, artifacts, decisions, evidence, lifecycles, invariants, and reports?
- What authority is created, consumed, transferred, superseded, revoked, or retired?
- What observations become evidence and under which validation rules?
- What artifacts does it create, mutate, promote, archive, delete, or retain?
- Which states summarize protocol behavior and what evidence makes them safe?
- What lifecycle movements can it cause?
- What invariants can block, fail, pause, reject, or route recovery?
- What reports describe outcomes without creating authority?
- What recovery protocols preserve lineage when ordinary advancement fails?

## Overall Architectural Evolution Narrative

The architecture evolves from implicit workflow behavior to explicit semantic governance.

At the beginning, the most important change is not a new execution path. It is the recovery of subjecthood. Repositories, plans, issues, generated files, decisions, evidence, states, protocols, reports, authority grants, and recovery reviews become governed subjects with identity, lifecycle, owner, authority, invariants, and relation rules. This gives the system a stable language for saying what exists before it says what can happen.

Once subjecthood exists, intent and source capture give the system two missing anchors: why work should begin and what outside information constrains it. This prevents passive knowledge from becoming action and prevents captured files from becoming source authority merely because they were stored.

Protocols then become the admission layer. A request is no longer valid because a command can run or because current code has a branch for it. It is valid only when a protocol admits subject-bound intent under scoped authority with fresh enough sources, valid relations, lifecycle readiness, and satisfied invariants.

Interactions then replace transitions as the primitive occurrence. Execution, review, parsing, validation, reporting, materialization, promotion, certification, and recovery are all interaction purposes or effects. Observations remain observations until captured, validated, and bound as evidence. Durable memory records this lineage but does not own authority.

Artifacts, decisions, and state become authority-bearing only after evidence and protocols can support them. Generated content becomes candidate artifact, not current truth. Parser output becomes suggestion, not decision. State becomes a validated control summary, not the whole workflow truth. Current memory becomes latest valid agreement across state, lifecycle, artifacts, evidence, decisions, authority, relations, and source freshness.

Recovery is introduced only after blockers, state, lifecycle, decisions, artifacts, and evidence are trustworthy. This prevents retry from masquerading as repair. Certification and distillation come after recovery because completion and current understanding need the full lineage of evidence, decisions, state, artifact lifecycle, and blockers.

Finally, capability admission turns the constitution into an extensibility rule. Future capabilities do not add new foundations. They declare their subjects, protocols, authority, evidence, artifacts, decisions, relations, lifecycles, invariants, reporting, recovery, replay, and retirement. If they can do that, they fit the system. If they cannot, the architecture has exposed either an invalid shortcut or a genuine constitutional gap.

The finished system should read like the constitution made operational:

```text
Subject-bound intent
  -> protocol admission
  -> authorized interaction
  -> observation
  -> validated evidence
  -> accepted decision when needed
  -> authorized effect on artifact, state, lifecycle, relation, report, recovery, certification, or distillation
  -> durable lineage
```

That is the stable architectural target. Existing implementation structures can migrate toward it incrementally, but the semantic order should not change.
