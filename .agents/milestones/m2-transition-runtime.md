# Milestone 2: Canonical Transition Runtime

Objective: implement one workflow-agnostic lifecycle for executing a prompt-driven transition.

## Work

- [ ] Add runtime services under `src/LoopRelay.Orchestration.Primitives/Runtime`:
  - [ ] `TransitionRuntime`
  - [ ] `ITransitionDefinitionResolver`
  - [ ] `IProductResolver`
  - [ ] `IGateEvaluator`
  - [ ] `IPromptContextBuilder`
  - [ ] `IPromptRenderer`
  - [ ] `IPromptExecutor`
  - [ ] `IOutputInterpreter`
  - [ ] `IProductValidator`
  - [ ] `IEffectExecutor`
  - [ ] `ITransitionRunStore`
  - [ ] `ITransitionEvidenceStore`
- [ ] Implement the lifecycle:
  - [ ] Resolve transition definition.
  - [ ] Resolve required inputs.
  - [ ] Evaluate input gate.
  - [ ] Construct prompt context.
  - [ ] Render prompt.
  - [ ] Persist transition start.
  - [ ] Execute prompt.
  - [ ] Capture raw output.
  - [ ] Interpret output.
  - [ ] Validate declared outputs.
  - [ ] Apply effects.
  - [ ] Persist completion.
  - [ ] Resolve eligible successors.
- [ ] Add durable transition states:
  - [ ] Not started.
  - [ ] Started.
  - [ ] Prompt completed.
  - [ ] Output interpreted.
  - [ ] Output validated.
  - [ ] Effects partially applied.
  - [ ] Effects applied.
  - [ ] Completed.
  - [ ] Blocked.
  - [ ] Failed.
  - [ ] Cancelled.
- [ ] Implement execution postures without workflow-specific runtime types:
  - [ ] One-shot agent prompt.
  - [ ] Persistent session.
  - [ ] Warm session.
  - [ ] Scoped artifact operation.
  - [ ] Decision session.
  - [ ] Read-only prompt.
- [ ] Extract reusable pieces from `RoadmapPromptTransitionRunner`:
  - [ ] Input snapshot hashing.
  - [ ] Transition journal events.
  - [ ] Raw prompt output capture.
  - [ ] Failure persistence pattern.
- [ ] Keep output validation after prompt execution and before completion.
- [ ] Ensure a successful prompt response cannot complete a transition unless required products validate.
- [ ] Add effect execution with deterministic ordering and durable partial-failure evidence.
- [ ] Add representative adapter coverage for one roadmap transition, preferably completion-context bootstrap, without making the Roadmap CLI use the runtime in production.

## Acceptance

- [ ] Transition runtime tests cover missing inputs, stale inputs, invalid inputs, malformed prompt output, missing output, invalid output, partial effect failure, cancellation, and persistence failure.
- [ ] The representative transition executes fully through the runtime in tests.
- [ ] No workflow migration has begun.
