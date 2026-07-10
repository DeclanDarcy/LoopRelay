# Milestone 8: EvalRoadmap Implementation

Objective: implement EvalRoadmap as a first-class workflow that converges into the same Plan entry contract as TraditionalRoadmap.

## Work

- [ ] Add `EvalRoadmapWorkflowDefinition`.
- [ ] Add evaluation artifact path constants, preferably in a new `EvaluationArtifactPaths` class:
  - [ ] Input directory `.agents/evals`.
  - [ ] Selected evaluation.
  - [ ] Dependency inventory: `.agents/eval-dependency-inventory.md`.
  - [ ] Hypothesis inventory: `.agents/eval-hypothesis-inventory.md`.
  - [ ] Architectural catalog: `.agents/eval-architectural-catalog.md`.
  - [ ] Eval DAG: `.agents/eval-dag.md`.
  - [ ] Next epic roadmap: `.agents/next-epic-roadmap.md`.
  - [ ] Prepared epic: `.agents/epic.md`.
  - [ ] Milestone specification set: `.agents/specs/*.md`.
  - [ ] Evaluation evidence directory.
- [ ] Register and use eval prompt assets from `src/LoopRelay.Core/Prompts/Eval`:
  - [ ] `CreateEvalDependencyInventory.prompt` creates `.agents/eval-dependency-inventory.md` from `.agents/evals/`.
  - [ ] `CreateEvalHypothesisInventory.prompt` creates `.agents/eval-hypothesis-inventory.md` from `.agents/eval-dependency-inventory.md`.
  - [ ] `CreateArchitecturalCatalog.prompt` creates `.agents/eval-architectural-catalog.md` from the dependency and hypothesis inventories.
  - [ ] `CreateEvalDag.prompt` creates `.agents/eval-dag.md` from the architectural catalog, dependency inventory, and hypothesis inventory.
  - [ ] `CreateNextEpicRoadmap.prompt` creates `.agents/next-epic-roadmap.md` from the eval DAG and supporting eval artifacts.
  - [ ] `CreateNextEpicImplementationSpec.prompt` creates `.agents/epic.md` from the next-epic roadmap and supporting eval artifacts.
  - [ ] `UpdateDependencyInventory.prompt` refreshes `.agents/eval-dependency-inventory.md` against actual repository state when status assessment is required.
  - [ ] `UpdateHypothesisInventory.prompt` refreshes `.agents/eval-hypothesis-inventory.md` against actual repository state when status assessment is required.
  - [ ] `UpdateRoadmap.prompt` refreshes `.agents/next-epic-roadmap.md` against actual repository state when status assessment is required.
- [ ] Reuse the existing `GenerateMilestoneDeepDivesForEpic` transition after active epic generation:
  - [ ] Input: `.agents/epic.md`.
  - [ ] Output: `.agents/specs/*.md`.
  - [ ] Required prompt context section: `Active Epic`, loaded from `.agents/epic.md`.
  - [ ] Prompt blocks rather than generating specs when `Active Epic` is missing, empty, malformed, or ambiguous.
- [ ] Define stages:
  - [ ] Evaluation Foundation.
  - [ ] Dependency Inventory.
  - [ ] Hypothesis Inventory.
  - [ ] Architectural Catalog.
  - [ ] Eval DAG.
  - [ ] Next Epic Roadmap.
  - [ ] Active Epic Preparation.
  - [ ] Milestone Specification.
  - [ ] Workflow Completion.
- [ ] Define transitions:
  - [ ] Select Evaluation Intent.
  - [ ] Create Eval Dependency Inventory.
  - [ ] Create Eval Hypothesis Inventory.
  - [ ] Create Eval Architectural Catalog.
  - [ ] Create Eval DAG.
  - [ ] Create Next Epic Roadmap.
  - [ ] Create Next Epic Active Epic.
  - [ ] Refresh Eval Dependency Inventory Status.
  - [ ] Refresh Eval Hypothesis Inventory Status.
  - [ ] Refresh Next Epic Roadmap Status.
  - [ ] Generate Milestone Deep Dives For Epic.
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

Dependency Inventory discovers and validates implementation-first evaluation dependencies.

Hypothesis Inventory generates falsifiable implementation-first hypotheses.

Architectural Catalog organizes dependency and hypothesis items into dependency-safe, falsifiability-safe horizontal groups.

Eval DAG creates the prerequisite, evidence, validation, falsifiability, and machine-gate graph.

Next Epic Roadmap selects the earliest meaningful unresolved graph frontier and decomposes one bounded epic slice into evidence-backed milestones.

Active Epic Preparation transforms the selected roadmap into the canonical active epic at `.agents/epic.md`.

Milestone Specification reuses `GenerateMilestoneDeepDivesForEpic` to transform `.agents/epic.md` into the canonical `.agents/specs/*.md` milestone specification set.

Workflow Completion verifies the Plan entry contract.

### Eval Prompt Asset Contract

EvalRoadmap should use the prompt assets already present under `src/LoopRelay.Core/Prompts/Eval`. These prompts own the evaluation-analysis artifact sequence:

| Transition | Prompt asset | Primary output |
|---|---|---|
| Create Eval Dependency Inventory | `CreateEvalDependencyInventory.prompt` | `.agents/eval-dependency-inventory.md` |
| Create Eval Hypothesis Inventory | `CreateEvalHypothesisInventory.prompt` | `.agents/eval-hypothesis-inventory.md` |
| Create Eval Architectural Catalog | `CreateArchitecturalCatalog.prompt` | `.agents/eval-architectural-catalog.md` |
| Create Eval DAG | `CreateEvalDag.prompt` | `.agents/eval-dag.md` |
| Create Next Epic Roadmap | `CreateNextEpicRoadmap.prompt` | `.agents/next-epic-roadmap.md` |
| Create Next Epic Active Epic | `CreateNextEpicImplementationSpec.prompt` | `.agents/epic.md` |
| Refresh Eval Dependency Inventory Status | `UpdateDependencyInventory.prompt` | `.agents/eval-dependency-inventory.md` |
| Refresh Eval Hypothesis Inventory Status | `UpdateHypothesisInventory.prompt` | `.agents/eval-hypothesis-inventory.md` |
| Refresh Next Epic Roadmap Status | `UpdateRoadmap.prompt` | `.agents/next-epic-roadmap.md` |

The prompt-generated eval analysis artifacts are intermediate products. They do not satisfy Plan entry by artifact existence alone. EvalRoadmap must validate them, record product evidence, write the canonical active epic to `.agents/epic.md`, and then use `GenerateMilestoneDeepDivesForEpic` to produce the same canonical `MilestoneSpecificationSet` product that TraditionalRoadmap produces.

`CreateNextEpicImplementationSpec.prompt` must not default to `.agents/specs/epic.md`. The universal EvalRoadmap writes `.agents/epic.md`; `GenerateMilestoneDeepDivesForEpic` consumes `.agents/epic.md` and writes `.agents/specs/*.md`. The universal Plan entry gate consumes the canonical `PreparedEpic` product and the canonical `MilestoneSpecificationSet` product, not an old Plan preflight artifact.

### Evaluation Products

Semantic products:

- Evaluation Intent
- Dependency Inventory
- Hypothesis Inventory
- Architectural Catalog
- Eval DAG
- Next Epic Roadmap
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
  -> Eval DAG
  -> Next Epic Roadmap
  -> Prepared Epic
  -> Milestone Specifications
```

Dependencies determine transition eligibility, not execution strategy. Execution remains serial now; future concurrent branches, join transitions, and deterministic merges should not require runtime redesign.

Repository-status refresh transitions may run against existing eval artifacts before downstream validation when repository assessment is explicitly required or when product freshness rules mark those artifacts stale.

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

### Workflow Independence

EvalRoadmap must not be a mode inside TraditionalRoadmap. It should have no inherited orchestration, no copied state machine, no special-case runtime, and no conditional orchestration after workflow selection.

Shared pieces are runtime, controller, products, and contracts. Independent pieces are stages, transitions, prompts, products, and dependencies.

### Eval Validation Cases

Validation should cover fresh evaluation, resume, dependency inventory generation, hypothesis inventory generation, architectural catalog generation, DAG generation, next-epic roadmap generation, active epic generation, repository-status refresh prompts, milestone spec generation through `GenerateMilestoneDeepDivesForEpic`, workflow completion, and Plan eligibility.

## Acceptance

- [ ] EvalRoadmap runs and resumes through the canonical runtime.
- [ ] EvalRoadmap uses the `src/LoopRelay.Core/Prompts/Eval` prompt assets for the eval analysis and status-refresh transitions.
- [ ] EvalRoadmap product validation blocks downstream progression on missing or invalid outputs.
- [ ] Plan entry is identical for EvalRoadmap and TraditionalRoadmap.
- [ ] No Plan or Execute code branches on roadmap producer identity.
