# Milestone 2: Canonical Transition Runtime

Objective: implement one workflow-agnostic lifecycle for executing a prompt-driven transition.

## Work

- [x] Add runtime services under `src/LoopRelay.Orchestration.Primitives/Runtime`:
  - [x] `TransitionRuntime`
  - [x] `ITransitionDefinitionResolver`
  - [x] `IProductResolver`
  - [x] `IGateEvaluator`
  - [x] `IPromptContextBuilder`
  - [x] `IPromptRenderer`
  - [x] `IPromptExecutor`
  - [x] `IOutputInterpreter`
  - [x] `IProductValidator`
  - [x] `IEffectExecutor`
  - [x] `ITransitionRunStore`
  - [x] `ITransitionEvidenceStore`
- [x] Implement the lifecycle:
  - [x] Resolve transition definition.
  - [x] Resolve required inputs.
  - [x] Evaluate input gate.
  - [x] Construct prompt context.
  - [x] Render prompt.
  - [x] Persist transition start.
  - [x] Execute prompt.
  - [x] Capture raw output.
  - [x] Interpret output.
  - [x] Validate declared outputs.
  - [x] Apply effects.
  - [x] Persist completion.
  - [x] Resolve eligible successors.
- [x] Add durable transition states:
  - [x] Not started.
  - [x] Started.
  - [x] Prompt completed.
  - [x] Output interpreted.
  - [x] Output validated.
  - [x] Effects partially applied.
  - [x] Effects applied.
  - [x] Completed.
  - [x] Blocked.
  - [x] Failed.
  - [x] Cancelled.
- [x] Implement execution postures without workflow-specific runtime types:
  - [x] One-shot agent prompt.
  - [x] Persistent session.
  - [x] Warm session.
  - [x] Scoped artifact operation.
  - [x] Decision session.
  - [x] Read-only prompt.
- [x] Extract reusable pieces from `RoadmapPromptTransitionRunner`:
  - [x] Input snapshot hashing.
  - [x] Transition journal events.
  - [x] Raw prompt output capture.
  - [x] Failure persistence pattern.
- [x] Keep output validation after prompt execution and before completion.
- [x] Ensure a successful prompt response cannot complete a transition unless required products validate.
- [x] Add effect execution with deterministic ordering and durable partial-failure evidence.
    - [x] Add representative runtime harness coverage for one roadmap transition, preferably completion-context bootstrap, without migrating workflows yet.
- [x] Persist canonical effect records from the transition runtime with the actual transition run id.
- [x] Persist canonical gate evaluation records for transition input and output gates.
- [x] Persist recoverable canonical blocker records for blocked input gates and prompt-context blocks.
- [x] Persist canonical recovery markers for blocked, failed, cancelled, and partially applied transition outcomes.

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
- storage representation
- evidence
- causal or hash identity

Failure modes should include missing, blocked, invalid, ambiguous, stale, and unsupported.

### Input Gate Checks

The input gate should check required products, authority, freshness, lifecycle, storage representation, and dependencies. It must return a structured gate result, not a boolean.

### Prompt Context Versus Prompt Rendering

Prompt context construction should resolve projections, project context, products, metadata, and migration inputs. It should not render the prompt.

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

Effect categories include persistence, lifecycle, evidence, decision recording, publication, Git, archive, migration bookkeeping, telemetry if present, and recovery bookkeeping.

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

- [x] Transition runtime tests cover missing inputs, stale inputs, invalid inputs, malformed prompt output, missing output, invalid output, partial effect failure, cancellation, and persistence failure.
- [x] The representative transition executes fully through the runtime in tests.
- [x] No workflow migration has begun.
