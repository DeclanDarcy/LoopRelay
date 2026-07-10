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

## Detail Requirements

### Freeze Scope

M2 freezes transition runtime behavior for downstream workflow migration. Later milestones consume this lifecycle rather than redefining prompt context construction, rendering, execution, output interpretation, product validation, effect execution, evidence, persistence, recovery, or successor eligibility.

### Runtime Boundary

M2 is about executing one transition, not workflows. It should not migrate workflows, implement chaining, unify CLI, implement automatic workflow selection, implement stage resolution, redesign prompts, or change repository semantics.

### Input Resolution Fields

Input resolution should locate required products and determine:

- identity
- authority
- freshness
- usability
- validation state
- compatibility representation
- evidence
- causal or hash identity

Failure modes should include missing, blocked, invalid, ambiguous, stale, and unsupported.

### Input Gate Checks

The input gate should check required products, authority, freshness, lifecycle, compatibility, and dependencies. It must return a structured gate result, not a boolean.

### Prompt Context Versus Prompt Rendering

Prompt context construction should resolve projections, project context, products, metadata, and compatibility inputs. It should not render the prompt.

Prompt rendering should load prompt content, inject resolved inputs, inject projections, inject metadata, and produce an immutable rendered prompt. It should not persist state, validate outputs, apply effects, or advance lifecycle.

### Prompt Execution Metadata

Prompt execution returns raw result and metadata only. Metadata should include:

- execution duration
- cancellation state
- runtime diagnostics
- session metadata
- prompt identity
- execution posture

Supported postures include one-shot, persistent session, warm session, scoped operation, read-only, decision session, and elevated or permission-aware execution where applicable.

### Output Interpretation Categories

The interpreter should classify output as:

- valid
- malformed
- incomplete
- unexpected
- blocked

It should produce a structured transition result before any effects are applied.

### Output Gate Checks

The output gate should verify that products exist, validate, are authoritative, are fresh, are complete, and satisfy dependencies. Prompt success must not equal transition success.

### Effect Execution Requirements

Effects execute after output gate satisfaction. They must be ordered, explicit, observable, recoverable where required, and able to persist partial failure evidence.

Effect categories include persistence, lifecycle, evidence, decision recording, publication, Git, archive, compatibility, telemetry if present, and recovery bookkeeping.

### Transition Persistence Metadata

Transition persistence should record:

- transition identity
- workflow
- stage
- products
- evidence
- gate results
- execution metadata
- recovery metadata

States should distinguish not started, started, prompt completed, output interpreted, output validated, effects partially applied, effects applied, completed, blocked, failed, and cancelled.

### Successor Resolution Boundary

The transition runtime returns eligible successor candidates. It does not choose which successor runs next; workflow orchestration chooses later.

### Recovery Model

Recovery should classify interrupted transitions as restart, resume, repair, rerun, or unsupported. Recovery state must be workflow-agnostic and based on transition evidence.

### Runtime Evidence Contents

Runtime evidence should be sufficient to explain:

- transition identity
- workflow
- stage
- inputs
- products
- prompt identity and rendered prompt evidence
- execution metadata
- validation results
- effects
- persistence
- recovery state
- successor eligibility

### Runtime Validation

M2 tests should stress missing inputs, invalid inputs, blocked gates, malformed prompt output, partial output, invalid products, effect failures, cancellation, persistence failure, and unsupported recovery.

## Acceptance

- [ ] Transition runtime tests cover missing inputs, stale inputs, invalid inputs, malformed prompt output, missing output, invalid output, partial effect failure, cancellation, and persistence failure.
- [ ] The representative transition executes fully through the runtime in tests.
- [ ] No workflow migration has begun.
