# Milestone 4: Workflow Chaining and Unified Gates

Objective: compose workflows through the same product/gate mechanism used by transitions.

## Work

- [ ] Add chain definitions:
  - [ ] `TraditionalRoadmapChain`: `TraditionalRoadmap -> Plan -> Execute`
  - [ ] `EvalRoadmapChain`: `EvalRoadmap -> Plan -> Execute`
- [ ] Add workflow boundary services:
  - [ ] `WorkflowEntryGateEvaluator`
  - [ ] `WorkflowExitGateEvaluator`
  - [ ] `ProductTransferEvaluator`
  - [ ] `WorkflowBoundaryEvidenceWriter`
- [ ] Add `WorkflowController` and `WorkflowChainRunner`.
- [ ] Ensure the controller owns workflow selection, stage selection, transition selection among eligible transitions, workflow completion checks, downstream eligibility, bounded stop conditions, and terminal outcome mapping.
- [ ] Ensure the controller does not render prompts, execute prompts, validate products, apply effects, or write transition persistence directly. Those remain runtime responsibilities.
- [ ] Add stopping conditions:
  - [ ] Chain completed.
  - [ ] Bounded workflow completed.
  - [ ] Blocked.
  - [ ] Waiting.
  - [ ] Cancelled.
  - [ ] Failed.
  - [ ] Stalled.
  - [ ] Ambiguous.
  - [ ] No eligible transition.
- [ ] Add explainability for why chaining occurred or stopped.
- [ ] Use fake workflow definitions and compatibility adapters for tests.
- [ ] Do not migrate production workflows yet.

## Acceptance

- [ ] Chain progression tests prove workflow boundaries use validated products, not files.
- [ ] Bounded commands stop after one workflow.
- [ ] Default, forced eval, and forced traditional modes select the correct chain.
- [ ] No production workflow has been migrated yet.
