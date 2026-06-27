# Phase 8 - Repository Knowledge and Information Intelligence

Goal: make Repository Knowledge first-class. Repository Knowledge is the broader durable body of repository information: Repository Understanding, history, evidence, knowledge graph, lineage, and queries. Repository Understanding remains the current, living view inside that knowledge body.

## Implementation

- [ ] Establish Repository Knowledge as the named information layer:
  - Repository Understanding: current living view
  - Repository History: time-ordered evolution
  - Repository Evidence: supporting artifacts and facts
  - Knowledge Graph: relationships among durable information
  - Information Lineage: provenance from intent through understanding
  - Information Queries: governed exploration over authoritative facts
- [ ] Preserve the Phase 7 boundary: Repository Understanding is already canonical. This phase enriches the surrounding knowledge layer rather than reintroducing understanding as a new feature.
- [ ] Promote durable information objects:
  - Intent
  - Plan
  - Handoff
  - Decision
  - Operational Context
  - Repository Understanding
  - Evidence
  - History Entry
  - Knowledge Relationship
- [ ] Treat Operational Context as the implementation artifact behind Repository Understanding.
- [ ] Extend Reasoning Graph into a repository Knowledge Graph connecting intent, plans, runs, handoffs, decisions, understanding, artifacts, evidence, and history.
- [ ] Add repository history as a continuous, queryable, repository-centric timeline:
  - planning history
  - execution history
  - decision history
  - understanding history
  - transfer history
  - knowledge evolution
- [ ] Add end-to-end lineage:
  - intent to plan
  - plan to run
  - run to handoff
  - handoff to decision
  - decision to understanding
  - understanding to future decisions
- [ ] Add information queries backed by authoritative information:
  - why a decision was made
  - how understanding changed
  - what evidence supports a claim
  - when a claim became true
  - which assumptions remain
  - which goals remain incomplete
- [ ] Add information authority tests:
  - Human owns intent and ratification.
  - Planning runtime generates proposals and revisions but does not approve them.
  - Decisions domain validates decisions.
  - Continuity owns Repository Understanding.
  - Reasoning owns Knowledge Graph semantics.
- [ ] Add UI knowledge, history, evidence, lineage, and understanding-evolution explorers.
- [ ] Add contracts for Repository Understanding, Knowledge Graph, Repository History, Information Lineage, Information Query, and Evolution projections.

## Certification

- [ ] Repository Understanding is the primary representation of repository knowledge.
- [ ] Repository Knowledge is the durable information layer that contains understanding, history, evidence, graph relationships, lineage, and queries.
- [ ] Knowledge relationships are evidence-backed and queryable.
- [ ] Information lineage is complete across planning, execution, decisions, and understanding.
- [ ] Repository intelligence explains current state without speculative conclusions.
