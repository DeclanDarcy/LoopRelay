# Milestone 8: EvalRoadmap Implementation

Objective: implement EvalRoadmap as a first-class workflow that converges into the same Plan entry contract as TraditionalRoadmap.

## Work

- [ ] Add `EvalRoadmapWorkflowDefinition`.
- [ ] Add evaluation artifact path constants, preferably in a new `EvaluationArtifactPaths` class:
  - [ ] Input directory `.agents/evals`.
  - [ ] Selected evaluation.
  - [ ] Dependency inventory.
  - [ ] Hypothesis inventory.
  - [ ] Architectural catalog.
  - [ ] Dependency graph.
  - [ ] Next epic roadmap.
  - [ ] Evaluation evidence directory.
- [ ] Add prompt assets under `src/LoopRelay.Core/Prompts/Evaluation`:
  - [ ] Interpret Next Evaluation.
  - [ ] Generate Dependency Inventory.
  - [ ] Generate Hypothesis Inventory.
  - [ ] Generate Architectural Catalog.
  - [ ] Generate Dependency DAG.
  - [ ] Generate Next Epic Roadmap.
  - [ ] Generate Prepared Epic.
  - [ ] Generate Milestone Specifications, reusing existing milestone generation where possible.
- [ ] Define stages:
  - [ ] Evaluation Foundation.
  - [ ] Dependency Analysis.
  - [ ] Hypothesis Development.
  - [ ] Architectural Organization.
  - [ ] Roadmap Formation.
  - [ ] Epic Preparation.
  - [ ] Milestone Specification.
  - [ ] Workflow Completion.
- [ ] Define transitions:
  - [ ] Interpret Next Evaluation.
  - [ ] Generate Dependency Inventory.
  - [ ] Generate Hypothesis Inventory.
  - [ ] Generate Architectural Catalog.
  - [ ] Generate Dependency DAG.
  - [ ] Generate Next Epic Roadmap.
  - [ ] Generate Prepared Epic.
  - [ ] Generate Milestone Specifications.
  - [ ] Verify Plan Entry Contract.
- [ ] Express dependencies declaratively.
- [ ] Keep execution serial while ensuring the definition supports multiple eligible successors later.
- [ ] Implement output gates for every evaluation knowledge product.
- [ ] Ensure EvalRoadmap produces the exact same `PreparedEpic` and `MilestoneSpecificationSet` products as TraditionalRoadmap.
- [ ] Ensure Plan does not branch on which roadmap workflow produced the products.
- [ ] Add resolution support:
  - [ ] Default invocation selects EvalRoadmap when `.agents/evals/*.md` exists.
  - [ ] `--eval` forces EvalRoadmap chain.
  - [ ] `looprelay eval` runs EvalRoadmap only.

## Detail Requirements

### EvalRoadmap Contract

Workflow identity: `EvalRoadmap`.

Purpose: transform evaluation intent into a prepared implementation roadmap.

Consumes:

- Evaluation Intent
- Project Context
- Repository Context

Produces:

- Prepared Epic
- Milestone Specification Set

Entry gate validates evaluation intent, repository readiness, project context, and storage authority.

Exit gate validates Prepared Epic, Milestone Specification Set, and Plan entry contract.

### Eval Stage Purposes

Evaluation Foundation establishes evaluation scope.

Dependency Analysis discovers and validates evaluation dependencies.

Hypothesis Development generates falsifiable architectural hypotheses.

Architectural Organization transforms hypotheses into architectural structure.

Roadmap Formation produces an implementation roadmap.

Epic Preparation produces the prepared implementation epic.

Milestone Specification generates implementation-ready milestone specifications.

Workflow Completion verifies the Plan entry contract.

### Evaluation Products

Semantic products:

- Evaluation Intent
- Dependency Inventory
- Hypothesis Inventory
- Architectural Catalog
- Dependency Graph
- Epic Roadmap
- Prepared Epic
- Milestone Specification Set

Each product should record identity, producer, authority, freshness, validation, dependencies, and lifecycle.

### Dependency Ordering

Current ordering:

```text
Evaluation Intent
  -> Dependency Inventory
  -> Hypothesis Inventory
  -> Architectural Catalog
  -> Dependency Graph
  -> Epic Roadmap
  -> Prepared Epic
  -> Milestone Specifications
```

Dependencies determine transition eligibility, not execution strategy. Execution remains serial now; future concurrent branches, join transitions, and deterministic merges should not require runtime redesign.

### Eval Workflow Resolution

Default detection selects EvalRoadmap when `.agents/evals/*.md` exists. `--eval` always selects EvalRoadmap in chained mode. `looprelay eval` runs only EvalRoadmap.

Eligibility states include eligible, blocked, waiting, cancelled, failed, completed, and ambiguous. Every EvalRoadmap decision must be explainable.

### Downstream Convergence

Both roadmap workflows terminate with:

```text
Prepared Epic
  -> Milestone Specification Set
  -> Plan Entry Gate
```

Plan, Execute, and future workflows must remain unaware of which roadmap workflow produced those products.

### Legacy Independence

EvalRoadmap must not be a mode inside TraditionalRoadmap. It should have no inherited orchestration, no copied state machine, no special-case runtime, and no conditional orchestration after workflow selection.

Shared pieces are runtime, controller, products, and contracts. Independent pieces are stages, transitions, prompts, products, and dependencies.

### Eval Validation Cases

Validation should cover fresh evaluation, resume, dependency generation, hypothesis generation, architecture generation, DAG generation, roadmap generation, epic generation, milestone generation, workflow completion, and Plan eligibility.

## Acceptance

- [ ] EvalRoadmap runs and resumes through the canonical runtime.
- [ ] EvalRoadmap product validation blocks downstream progression on missing or invalid outputs.
- [ ] Plan entry is identical for EvalRoadmap and TraditionalRoadmap.
- [ ] No Plan or Execute code branches on roadmap producer identity.
