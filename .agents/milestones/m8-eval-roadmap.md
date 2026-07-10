# Milestone 8: EvalRoadmap Implementation

Objective: implement EvalRoadmap as a first-class workflow that converges into the same Plan entry contract as TraditionalRoadmap.

## Work

- [x] Add `EvalRoadmapWorkflowDefinition`.
- [x] Add evaluation artifact path constants, preferably in a new `EvaluationArtifactPaths` class:
  - [x] Input directory `.agents/evals`.
  - [x] Selected evaluation.
  - [x] Dependency inventory: `.agents/eval-dependency-inventory.md`.
  - [x] Hypothesis inventory: `.agents/eval-hypothesis-inventory.md`.
  - [x] Architectural catalog: `.agents/eval-architectural-catalog.md`.
  - [x] Eval DAG: `.agents/eval-dag.md`.
  - [x] Next epic roadmap: `.agents/next-epic-roadmap.md`.
  - [x] Prepared epic: `.agents/epic.md`.
  - [x] Milestone specification set: `.agents/specs/*.md`.
  - [x] Evaluation evidence directory.
- [x] Register and use eval prompt assets from `src/LoopRelay.Core/Prompts/Eval`:
  - [x] `CreateEvalDependencyInventory.prompt` creates `.agents/eval-dependency-inventory.md` from `.agents/evals/`.
  - [x] `CreateEvalHypothesisInventory.prompt` creates `.agents/eval-hypothesis-inventory.md` from `.agents/eval-dependency-inventory.md`.
  - [x] `CreateArchitecturalCatalog.prompt` creates `.agents/eval-architectural-catalog.md` from the dependency and hypothesis inventories.
  - [x] `CreateEvalDag.prompt` creates `.agents/eval-dag.md` from the architectural catalog, dependency inventory, and hypothesis inventory.
  - [x] `CreateNextEpicRoadmap.prompt` creates `.agents/next-epic-roadmap.md` from the eval DAG and supporting eval artifacts.
  - [x] `CreateNextEpicImplementationSpec.prompt` creates `.agents/epic.md` from the next-epic roadmap and supporting eval artifacts.
  - [x] `UpdateDependencyInventory.prompt` refreshes `.agents/eval-dependency-inventory.md` against actual repository state when status assessment is required.
  - [x] `UpdateHypothesisInventory.prompt` refreshes `.agents/eval-hypothesis-inventory.md` against actual repository state when status assessment is required.
  - [x] `UpdateRoadmap.prompt` refreshes `.agents/next-epic-roadmap.md` against actual repository state when status assessment is required.
  - [x] Generated Eval prompt templates render through the unified runtime prompt renderer with source-hash evidence.
- [x] Reuse the existing `GenerateMilestoneDeepDivesForEpic` transition after active epic generation:
  - [x] Input: `.agents/epic.md`.
  - [x] Output: `.agents/specs/*.md`.
  - [x] Required prompt context section: `Active Epic`, loaded from `.agents/epic.md`.
  - [x] Prompt blocks rather than generating specs when `Active Epic` is missing, empty, malformed, or ambiguous.
- [x] Define stages:
  - [x] Evaluation Foundation.
  - [x] Dependency Inventory.
  - [x] Hypothesis Inventory.
  - [x] Architectural Catalog.
  - [x] Eval DAG.
  - [x] Next Epic Roadmap.
  - [x] Active Epic Preparation.
  - [x] Milestone Specification.
  - [x] Workflow Completion.
- [x] Define transitions:
  - [x] Select Evaluation Intent.
  - [x] Create Eval Dependency Inventory.
  - [x] Create Eval Hypothesis Inventory.
  - [x] Create Eval Architectural Catalog.
  - [x] Create Eval DAG.
  - [x] Create Next Epic Roadmap.
  - [x] Create Next Epic Active Epic.
  - [x] Refresh Eval Dependency Inventory Status.
  - [x] Refresh Eval Hypothesis Inventory Status.
  - [x] Refresh Next Epic Roadmap Status.
  - [x] Generate Milestone Deep Dives For Epic.
  - [x] Verify Plan Entry Contract.
- [x] Express dependencies declaratively.
- [x] Keep execution serial while ensuring the definition supports multiple eligible successors later.
- [x] Implement output gates for every evaluation knowledge product.
- [x] Ensure EvalRoadmap produces the exact same `PreparedEpic` and `MilestoneSpecificationSet` products as TraditionalRoadmap.
- [x] Ensure Plan does not branch on which roadmap workflow produced the products.
- [x] Add resolution support:
  - [x] Default invocation selects EvalRoadmap when `.agents/evals/*.md` exists.
  - [x] `--eval` forces EvalRoadmap chain.
  - [x] `looprelay eval` runs EvalRoadmap only.
  - [x] `SelectEvaluationIntent` runs through the canonical transition runtime from observed `.agents/evals/*.md` intent and resumes at Dependency Inventory.
  - [x] Existing eval dependency, hypothesis, architecture catalog, DAG, and next-roadmap artifacts resume at the correct EvalRoadmap stage without mutating the repository.
  - [x] Downstream eval artifacts do not skip canonical stages when required prior eval products are missing.

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

- [x] EvalRoadmap runs and resumes through the canonical runtime.
- [x] EvalRoadmap uses the `src/LoopRelay.Core/Prompts/Eval` prompt assets for the eval analysis and status-refresh transitions.
- [x] EvalRoadmap product validation blocks downstream progression on missing or invalid outputs.
- [x] Plan entry is identical for EvalRoadmap and TraditionalRoadmap.
- [x] No Plan or Execute code branches on roadmap producer identity.
