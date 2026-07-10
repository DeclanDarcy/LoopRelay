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

## Acceptance

- [ ] EvalRoadmap runs and resumes through the canonical runtime.
- [ ] EvalRoadmap product validation blocks downstream progression on missing or invalid outputs.
- [ ] Plan entry is identical for EvalRoadmap and TraditionalRoadmap.
- [ ] No Plan or Execute code branches on roadmap producer identity.
