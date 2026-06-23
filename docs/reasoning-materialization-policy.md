# Reasoning Materialization Policy

Reasoning Trajectory starts with events, threads, relationships, references, graph navigation, queries, and reconstructions. Specialized hypothesis, alternative, contradiction, and direction records are intentionally deferred.

## Derived If Reconstructable

Before introducing a new persisted reasoning artifact, ask:

```text
Can this be reconstructed from events, threads, relationships, and existing domain artifacts?
```

If the answer is yes, the concept remains derived.

This rule protects Command Center from creating a second source of truth beside Decisions, Operational Context, Governance, and Execution.

## Materialization Gate

A materialization review is required before adding any top-level persisted entity beyond:

- reasoning events
- reasoning threads
- reasoning relationships
- explicitly requested reconstruction or certification reports

The review must answer:

- What question cannot be answered from existing reasoning records and source artifacts?
- What repeated workflow requires a mutable domain object?
- Why is a derived projection insufficient?
- What authority could the new artifact accidentally imply?
- How will the artifact remain explanatory instead of authoritative?
- How will repository recovery work if the artifact is deleted?
- Can the artifact be rebuilt from events?
- Are event families or event types starting to behave like an unapproved lifecycle state machine?
- Should persisted thread identity remain first-class, become a derived graph cluster, or be limited to persisted reports?

## Allowed Outcomes

The materialization review may conclude:

- Remain Derived
- Add Derived Cache
- Add Read Model Report
- Promote To First-Class Entity
- Reject Concept

Promotion to a first-class entity requires a separate implementation slice. It must not be bundled into event substrate, graph, query, or reconstruction work.

## Thread Review

Threads are persisted early as a pragmatic grouping mechanism. That does not make them permanently first-class. A later materialization review may keep persisted thread identity, demote threads to derived graph clusters, or restrict thread persistence to reports if thread records begin to imply authority.

## Event Families Do Not Create Entities

Event families and event types are classification vocabulary. They are not lifecycle transition tables.

For example, `HypothesisRaised`, `HypothesisSupported`, and `HypothesisInvalidated` can reconstruct a hypothesis trace. They do not authorize a `.agents/reasoning/hypotheses` directory or mutable hypothesis state machine.

If event taxonomy starts simulating an unapproved lifecycle, the correct response is to simplify the taxonomy or run the materialization review.
