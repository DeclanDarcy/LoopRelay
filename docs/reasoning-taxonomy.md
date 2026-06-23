# Reasoning Taxonomy

This document defines the vocabulary for Reasoning Trajectory Preservation. The taxonomy is explanatory, event-led, and repository-scoped. It does not create new decision authority, operational-context authority, governance authority, or execution authority.

## Core Terms

Reasoning Trajectory is the durable explanation of how project thinking changed over time. It preserves why a hypothesis, alternative, contradiction, assumption, constraint, or strategic direction mattered, and how those changes influenced later work.

Reasoning Event is the immutable unit of reasoning history. An event records what changed in the project's thinking, when it was recorded, which event family and event type classify it, what narrative explains it, what artifacts it references, and what provenance supports it.

Reasoning Thread is a reviewable grouping of related reasoning events across decisions, milestones, handoffs, governance findings, operational-context revisions, and execution outputs. Threads make long-running reasoning easier to navigate, but they are not sessions, decisions, or authority.

Reasoning Relationship is an explanatory link between reasoning events, reasoning threads, or existing domain artifacts. Relationships can express causes, influence, support, challenge, contradiction, replacement, invalidation, resolution, reopening, selection, and derivation.

Reasoning Reference is a typed pointer from reasoning records to existing repository or domain artifacts. References connect explanations to evidence without transferring ownership of the referenced artifact.

Reasoning Graph is a derived navigation index built from events, threads, relationships, and resolvable references. The graph is rebuildable and must not become repository authority.

Reasoning Query is a request to trace or explain reasoning history. Queries select graph traversal and reconstruction strategies, but they do not mutate source artifacts or decide outcomes.

Reasoning Reconstruction is an auditable narrative answer built from events, relationships, threads, and source references. A reconstruction explains how a current position emerged and exposes the evidence used.

## Analytical Categories

Hypothesis, alternative, contradiction, and direction are analytical categories first. They are represented by event families, event types, thread themes, relationships, and reconstructions.

They are not first-class persisted entities in the initial implementation.

### Hypothesis

A hypothesis is a belief under investigation. It is reconstructed from hypothesis-family events such as raised, supported, challenged, invalidated, and retired. The current status is derived from the event trace, not stored in a separate hypothesis record.

### Alternative

An alternative is a path considered. It is reconstructed from alternative-family events such as introduced, compared, rejected, revisited, and selected. Proposal-local alternatives remain part of Decision Lifecycle artifacts; broader alternative history belongs to reasoning events.

### Contradiction

A contradiction is a conflict between beliefs, decisions, assumptions, constraints, evidence, or source artifacts. Governance owns current contradiction detection. Reasoning Trajectory owns contradiction history as events and relationships.

### Direction

Direction is the strategic movement that emerges from repeated reasoning events and cross-domain traces. Direction remains derived until a materialization review proves that a first-class strategic direction object is necessary.

## Event Families Are Vocabulary

Event families classify events. They do not imply that matching persisted aggregate types exist.

The presence of `HypothesisRaised`, `HypothesisSupported`, and `HypothesisInvalidated` events is evidence for reconstruction. It is not authorization to create `.agents/reasoning/hypotheses` or a mutable `Hypothesis` lifecycle.

Adding an event family or event type is a schema compatibility decision. Adding a persisted entity requires the materialization policy in `docs/reasoning-materialization-policy.md`.

## Non-Authoritative By Design

Reasoning records can support, influence, challenge, and explain existing domain artifacts. They may not approve decisions, resolve proposals, supersede decisions, promote operational context, enforce governance findings, create execution directives, or become a private knowledge database.
