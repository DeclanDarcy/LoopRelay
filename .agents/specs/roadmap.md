# Canonical Orchestration Architecture Roadmap

## 1. Purpose

Establish a single, canonical orchestration architecture for LoopRelay that replaces the independently evolved orchestration models currently used by:

* Traditional Roadmap creation
* Eval-driven Roadmap creation
* Plan and task decomposition
* Main implementation execution

The architecture must support two forward workflow chains:

```text
TraditionalRoadmap
        ↓
Plan
        ↓
Execute
```

```text
EvalRoadmap
        ↓
Plan
        ↓
Execute
```

The architecture must expose these chains through one CLI while preserving bounded single-workflow invocation.

The roadmap covers architecture formation, foundational runtime implementation, workflow migration, unified CLI integration, and validation.

The future loop from certified execution closure back into roadmap formation is explicitly out of scope.

---

# 2. Architectural Outcome

At completion, LoopRelay should have:

1. One CLI entry point.
2. Four explicit workflow identities.
3. One canonical orchestration runtime.
4. One canonical transition lifecycle.
5. Uniform input and output gates between:

   * transitions within a workflow;
   * workflows within a chain.
6. Automatic storage verification before orchestration decisions.
7. Automatic workflow and stage resolution for chained invocation.
8. Declarative workflow topology and transition dependencies.
9. Prompt-oriented transitions with concise workflow-specific definitions.
10. Durable, explainable state sufficient for safe resume, recovery, and stage detection.
11. A design that executes serially now but does not structurally prohibit future parallel Eval transitions.

---

# 3. Explicit Workflow Identities

The canonical workflow identities are:

```text
TraditionalRoadmap
EvalRoadmap
Plan
Execute
```

These identities are first-class architectural concepts.

They must not remain informal labels inferred from implementation classes or artifact combinations.

Each workflow must declare:

* identity;
* purpose;
* entry gate;
* stages;
* transitions;
* workflow exit gate;
* downstream workflow identity, when chained;
* required outputs;
* completion conditions;
* blocker semantics;
* recovery semantics.

---

# 4. Unified CLI Contract

## 4.1 Primary chained invocation

```text
looprelay
```

Behavior:

1. Verify storage automatically.
2. Detect roadmap mode:

   * if one or more `.agents/evals/*.md` files exist, select `EvalRoadmap`;
   * otherwise select `TraditionalRoadmap`.
3. Resolve the current workflow and stage.
4. Run or resume the selected roadmap workflow.
5. Verify the roadmap workflow exit gate.
6. Continue automatically into `Plan`.
7. Verify the Plan workflow exit gate.
8. Continue automatically into `Execute`.
9. Stop at certified execution closure, a blocker, cancellation, failure, or another terminal condition.

## 4.2 Explicit chained modes

```text
looprelay --eval
```

Runs or resumes:

```text
EvalRoadmap → Plan → Execute
```

```text
looprelay --traditional
```

Runs or resumes:

```text
TraditionalRoadmap → Plan → Execute
```

Explicit mode flags override repository-based roadmap selection.

## 4.3 Bounded single-workflow commands

```text
looprelay eval
looprelay traditional
looprelay plan
looprelay execute
```

Each command runs or resumes only the named workflow.

A single-workflow command must stop after that workflow reaches its exit gate or another terminal outcome.

It must not auto-chain.

## 4.4 Compatibility and operational commands

Existing status, recovery, and storage capabilities must either:

* be preserved under the unified CLI; or
* remain available through a clearly bounded compatibility surface until migrated.

Their exact public syntax should be finalized during the CLI integration milestone, after the orchestration model is stable.

---

# 5. Foundational Architectural Principles

## 5.1 Workflows are explicit

Workflow identity must not be reconstructed indirectly from class names, command binaries, or incidental artifacts.

## 5.2 Every transition runs a prompt

The primary workflow model is:

```text
Input Gate
    ↓
Prompt Transition
    ↓
Output Gate
```

Deterministic runtime operations may surround the prompt transition, but the domain advancement represented by a transition is prompt-driven.

## 5.3 Inter-workflow and intra-workflow boundaries are structurally equivalent

The runtime must treat:

```text
Transition A → Transition B
```

and:

```text
Workflow A → Workflow B
```

through the same underlying contract:

```text
Required output declared
        ↓
Output produced
        ↓
Output validated
        ↓
Downstream input gate satisfied
        ↓
Downstream unit becomes eligible
```

Workflow chaining must not be implemented through bespoke CLI-to-CLI glue.

## 5.4 Repository-scoped ownership

All workflow outputs are owned by the repository in which LoopRelay is operating.

The repository is the scope for:

* workflow state;
* stage state;
* transition evidence;
* outputs;
* blockers;
* recovery information;
* storage verification;
* lifecycle;
* workflow-chain progression.

No global external orchestrator state may be required to interpret repository progress.

## 5.5 Behavioral authority over historical implementation shape

The current Roadmap CLI is the closest architectural precedent, especially for:

* explicit states;
* transitions;
* prompt contracts;
* input snapshots;
* output validation;
* persistence;
* blockers;
* recovery;
* evidence.

However, existing Roadmap implementation details are not architectural authority.

The new architecture should preserve required behavior while replacing incidental verbosity, mixed responsibilities, duplicated persistence sequences, and clunky prompt-rendering paths with more coherent mechanisms.

## 5.6 No silent inference of success

The runtime must distinguish:

* prompt completed;
* output produced;
* output validated;
* transition completed;
* stage completed;
* workflow completed;
* workflow-chain completed.

None of these may be inferred solely from file existence.

## 5.7 Automatic verification, not automatic repair

Storage verification runs automatically before orchestration decisions.

Automatic verification may:

* establish authority;
* qualify confidence in observable state;
* block unsafe progression;
* expose conflicts or corruption.

It must not silently:

* import;
* export;
* synchronize;
* overwrite;
* repair;
* discard conflicting state.

## 5.8 Serial execution now, dependency topology later

The initial implementation may execute one transition at a time.

The architecture must nevertheless permit workflow definitions to express:

* transition dependencies;
* multiple eligible successors;
* independent branches;
* joins;
* deterministic consolidation.

Actual concurrent execution is outside the current scope.

## 5.9 Confidence is not part of current control flow

Telemetry-informed confidence and confidence-driven orchestration are future concerns.

The current architecture must not:

* assign confidence scores;
* use telemetry to select workflows;
* use confidence to bypass gates;
* introduce speculative confidence abstractions that affect current behavior.

It should avoid making future addition impossible.

---

# 6. Canonical Conceptual Model

## 6.1 Workflow Chain

A selected ordered composition of workflows.

Current chains:

```text
TraditionalRoadmap → Plan → Execute
EvalRoadmap → Plan → Execute
```

## 6.2 Workflow

A repository-scoped prompt-transition graph that transforms validated workflow inputs into validated workflow outputs.

## 6.3 Stage

A meaningful phase within a workflow containing one or more transitions and a stage completion gate.

A stage should represent a coherent domain phase, not incidental implementation activity such as rendering, loading, or persisting.

## 6.4 Transition

A prompt-driven unit of advancement with:

* identity;
* declared dependencies;
* input contract;
* prompt contract;
* output contract;
* parser or interpretation contract;
* validation contract;
* effects;
* completion evidence;
* failure and blocker semantics.

## 6.5 Gate

A deterministic evaluation that decides whether an input, output, stage, workflow, or chain boundary has been satisfied.

Gate results must be explainable and evidence-bearing.

## 6.6 Product

A semantic output of a transition or workflow.

Products may be serialized as files, SQLite records, Git state, evidence, or structured metadata, but their architectural identity must not be reduced to one storage representation.

Examples include:

* prepared epic;
* milestone specification set;
* executable plan;
* operational context;
* execution milestone set;
* handoff;
* certified completion.

## 6.7 Workflow State

Durable repository-owned evidence sufficient to determine:

* workflow identity;
* current stage;
* last attempted transition;
* completed transitions;
* produced products;
* blockers;
* recovery eligibility;
* downstream workflow eligibility.

## 6.8 Transition Result

A structured outcome distinct from raw prompt output.

It must represent at least:

* success;
* blocked;
* failed;
* cancelled;
* invalid output;
* waiting for human input;
* no eligible successor.

## 6.9 Effects

Repository mutations resulting from a validated transition result.

Effects may include:

* artifact persistence;
* lifecycle update;
* evidence capture;
* decision recording;
* publication;
* Git operations;
* archive effects;
* recovery bookkeeping.

Effects must not be intermingled invisibly with prompt rendering or interpretation.

---

# 7. Roadmap Structure

The implementation is divided into ten milestones.

```text
M0  Architecture Baseline and Behavioral Freeze
M1  Canonical Contracts and Vocabulary
M2  Canonical Transition Runtime
M3  Workflow and Stage Resolution
M4  Workflow Chaining and Unified Gates
M5  Traditional Roadmap Migration
M6  Plan Workflow Migration
M7  Execute Workflow Migration
M8  Eval Roadmap Implementation
M9  Unified CLI, Compatibility Retirement, and Certification
```

Each milestone must produce a usable architectural increment and preserve existing behavior within its declared scope.

---

# M0 — Architecture Baseline and Behavioral Freeze

## Objective

Create a trustworthy behavioral baseline before replacing orchestration structures.

## Scope

Capture and lock down the observable behavior that the new architecture must preserve.

## Required work

### M0.1 Public CLI behavior baseline

Record current:

* commands;
* flags;
* exit codes;
* cancellation behavior;
* status behavior;
* recovery behavior;
* storage commands;
* Git and `.agents` publication behavior.

### M0.2 Workflow behavior baseline

Create executable characterization coverage for:

* Roadmap startup;
* Roadmap resume;
* Roadmap blocker reporting;
* Roadmap milestone-spec pause;
* Plan clean-start preflight;
* Plan pipeline completion;
* Execution first run;
* Execution from handoff;
* Execution with live decisions;
* stall handling;
* completion review;
* completion certification;
* cancellation salvage.

### M0.3 Persistence authority baseline

Characterize:

* filesystem-backed behavior;
* SQLite-backed behavior;
* automatic store selection;
* stale export;
* conflict;
* corrupt database;
* missing database;
* imported and canonical database states.

### M0.4 Known-risk containment

Document and isolate known completion and archive hazards that could invalidate refactor verification, including:

* partial archive materialization;
* rerun after live artifact archival;
* archive index collision;
* partially completed completion-context update.

The milestone does not need to redesign all of these behaviors, but it must prevent them from being silently reinterpreted during later migration.

## Exit gate

M0 is complete when:

* every behavior considered contractually important has executable characterization coverage or explicit evidence-based documentation;
* current outcomes can be compared against the new runtime;
* known pre-existing defects are distinguished from refactor regressions;
* no orchestration migration has begun.

---

# M1 — Canonical Contracts and Vocabulary

## Objective

Define the implementation-neutral contracts that every workflow, stage, transition, gate, and product must satisfy.

## Scope

Architectural contracts only. No workflow migration.

## Required work

### M1.1 Explicit workflow identities

Establish canonical identities:

* `TraditionalRoadmap`
* `EvalRoadmap`
* `Plan`
* `Execute`

### M1.2 Workflow definition contract

Define the information required to describe a workflow:

* identity;
* entry products;
* stages;
* transition dependency topology;
* exit products;
* downstream workflow;
* workflow completion gate;
* blocker behavior;
* recovery behavior.

### M1.3 Stage definition contract

Define:

* stage identity;
* member transitions;
* entry gate;
* completion gate;
* dependency relationships;
* allowable terminal outcomes.

### M1.4 Transition definition contract

Define:

* transition identity;
* required input products;
* prompt identity;
* prompt inputs;
* execution posture;
* output products;
* parser;
* validators;
* effects;
* next-transition eligibility;
* evidence requirements;
* blocker and failure semantics.

### M1.5 Product contract

Define semantic products independently from their storage representation.

At minimum, product identity must support:

* ownership;
* producer;
* consumer;
* version or causal identity;
* validation state;
* freshness;
* lifecycle;
* evidence location;
* storage representations.

### M1.6 Gate contract

Define deterministic gate results for:

* satisfied;
* unsatisfied;
* blocked;
* ambiguous;
* invalid;
* waiting for human input.

A gate result must identify:

* evaluated requirements;
* supporting evidence;
* missing requirements;
* conflicting evidence;
* authority used.

### M1.7 Runtime outcome vocabulary

Create a shared semantic vocabulary for:

* completed;
* paused;
* blocked;
* failed;
* cancelled;
* stalled;
* waiting;
* ambiguous.

Workflow-specific CLI exit mappings may remain adapters over this vocabulary.

## Exit gate

M1 is complete when:

* all four workflows can be described through the contracts without relying on workflow-specific runtime types;
* workflow and transition definitions do not embed persistence or CLI implementation details;
* semantic products are distinct from files;
* the contracts do not prohibit dependency-based future concurrency.

---

# M2 — Canonical Transition Runtime

## Objective

Implement one rigorous transition lifecycle that eliminates duplicated orchestration mechanics.

## Scope

The runtime for executing one prompt transition.

## Canonical transition lifecycle

```text
Resolve Definition
        ↓
Resolve Required Inputs
        ↓
Evaluate Input Gate
        ↓
Construct Prompt Context
        ↓
Render Prompt
        ↓
Persist Transition Start
        ↓
Execute Prompt
        ↓
Capture Raw Output
        ↓
Parse or Interpret Output
        ↓
Validate Declared Outputs
        ↓
Apply Effects
        ↓
Persist Transition Completion
        ↓
Evaluate Successor Eligibility
```

## Required work

### M2.1 Input resolution

Unify:

* semantic product lookup;
* artifact lookup;
* SQLite/file authority;
* hashes and causal identity;
* required versus optional inputs;
* freshness checks.

### M2.2 Prompt rendering

Replace transition-specific verbose prompt assembly with a small, explicit rendering path driven by:

* prompt identity;
* ordered named inputs;
* projection or project-context products;
* workflow-specific prompt content;
* execution posture.

Prompt rendering must not also own:

* persistence;
* output validation;
* lifecycle;
* state advancement;
* recovery.

### M2.3 Prompt execution

Provide one transition execution boundary supporting:

* one-shot execution;
* persistent session execution where required;
* permissions;
* cancellation;
* raw output capture;
* execution diagnostics.

The architecture may support multiple execution postures without creating multiple transition models.

### M2.4 Output interpretation

Standardize:

* parser invocation;
* structured decision extraction;
* malformed output classification;
* raw-output preservation;
* evidence generation.

### M2.5 Output gates

Ensure required products have been:

* emitted;
* located;
* parsed where required;
* validated;
* associated with the current transition;
* marked usable for downstream consumers.

### M2.6 Effects

Separate effect application from prompt execution.

Effect application must be:

* ordered;
* explicit;
* observable;
* failure-aware;
* recoverable where current behavior requires it.

### M2.7 Transition persistence

Persist enough state to distinguish:

* transition not started;
* transition started;
* prompt completed;
* outputs validated;
* effects partially applied;
* transition completed;
* transition blocked;
* transition failed;
* transition cancelled.

### M2.8 Transition evidence

Provide durable evidence sufficient to explain:

* why the transition ran;
* what inputs it consumed;
* what prompt identity it used;
* what outputs it produced;
* which validation passed or failed;
* which effects were applied;
* why the next transition became eligible or remained blocked.

## Exit gate

M2 is complete when:

* a representative Roadmap transition can execute entirely through the canonical runtime;
* prompt rendering is no longer entangled with transition persistence or lifecycle logic;
* output validation is required before completion;
* partial effect failure is observable;
* the runtime remains workflow-agnostic.

---

# M3 — Workflow and Stage Resolution

## Objective

Establish deterministic resolution of workflow identity, current stage, and next eligible transition.

## Scope

Repository observation and orchestration decisions.

No cross-workflow auto-chaining yet.

## Required work

### M3.1 Automatic storage verification

Run storage verification before workflow and stage resolution.

Verification must produce a structured result covering:

* authoritative store;
* usable store;
* stale exports;
* conflicts;
* corruption;
* unsupported schema;
* unresolved references;
* partial workflow transactions.

Unsafe verification results must block mutation without silently repairing state.

### M3.2 Invocation mode resolution

Resolve:

* default auto-chain;
* forced Eval chain;
* forced Traditional chain;
* bounded Eval workflow;
* bounded Traditional workflow;
* bounded Plan workflow;
* bounded Execute workflow.

### M3.3 Default roadmap selection

Implement the declared default rule:

```text
Exists(.agents/evals/*.md)
    ? EvalRoadmap
    : TraditionalRoadmap
```

This choice determines workflow identity only.

It does not bypass stage detection or resume state.

### M3.4 Workflow state resolution

For each explicit workflow, determine:

* absent;
* eligible to start;
* active;
* resumable;
* completed;
* blocked;
* waiting for human input;
* invalid;
* ambiguous.

### M3.5 Stage resolution

Resolve current stage from durable state and validated products.

Stage resolution must not rely only on output existence.

### M3.6 Successor resolution

Determine the eligible transition set from:

* completed dependencies;
* input gate satisfaction;
* blockers;
* product freshness;
* workflow state.

The result should support one successor today and multiple successors in future dependency graphs.

### M3.7 Explainability

Every resolution result must identify:

* selected workflow;
* selected stage;
* selected transition or eligible set;
* authoritative evidence;
* ignored lower-authority evidence;
* blockers or ambiguity.

## Exit gate

M3 is complete when:

* the runtime can determine the correct workflow and current stage for representative Roadmap, Plan, and Execute repository states;
* default Eval versus Traditional detection is implemented;
* storage verification always precedes mutating orchestration;
* ambiguous state is reported rather than silently guessed;
* no workflow-specific CLI runner is required to perform stage detection.

---

# M4 — Workflow Chaining and Unified Gates

## Objective

Make workflow-to-workflow progression use the same contract machinery as transition-to-transition progression.

## Scope

Chain orchestration without yet migrating every workflow implementation.

## Required work

### M4.1 Workflow entry gate

A workflow entry gate must verify all required semantic products.

It must answer:

* which upstream products are required;
* whether they exist;
* whether they are valid;
* whether they are fresh;
* whether they belong to the current repository and workflow chain;
* whether blockers or HITL decisions prevent entry.

### M4.2 Workflow exit gate

A workflow exit gate must verify:

* all required stages are complete;
* all required transition products are valid;
* required workflow products are available;
* no blocking condition remains;
* the downstream workflow contract can be satisfied.

### M4.3 Uniform product handoff

The runtime must hand products from one workflow into the next without bespoke CLI glue.

The handoff may materialize compatibility files when required, but compatibility serialization must be an effect of satisfying the contract, not the architectural boundary itself.

### M4.4 Chain controller

Implement forward progression for:

```text
TraditionalRoadmap → Plan → Execute
EvalRoadmap → Plan → Execute
```

The controller must:

* resume an active workflow;
* stop on blocker, failure, cancellation, ambiguity, or bounded invocation;
* advance only after the workflow exit gate passes;
* evaluate the next workflow entry gate;
* preserve workflow-specific outcomes.

### M4.5 No closure loop

Execution completion must terminate the current chain.

No automatic return to Roadmap is allowed in this scope.

### M4.6 Bounded invocation

Single-workflow subcommands must reuse the same workflow runtime and gates while disabling downstream chain advancement.

## Exit gate

M4 is complete when:

* a synthetic or minimally migrated workflow chain can advance using uniform gates;
* no direct CLI-to-CLI process invocation is needed;
* transition boundaries and workflow boundaries share the same product/gate model;
* bounded workflows stop correctly;
* the future execution-to-roadmap loop is absent.

---

# M5 — Traditional Roadmap Migration

## Objective

Migrate the current Roadmap behavior onto the canonical runtime without copying its incidental structure.

## Scope

Traditional roadmap formation through milestone-spec readiness.

## Required stages

A refined stage model should preserve the existing domain capabilities, including:

1. Roadmap context readiness.
2. Strategic initiative selection.
3. Existing/new/split initiative preparation.
4. Active epic readiness.
5. Milestone deep-dive generation.
6. Roadmap workflow completion.

Exact stage names may be refined during detailed design, but implementation mechanics must not become stages.

## Required work

### M5.1 Transition migration

Migrate prompt transitions for:

* completion-context bootstrap or update;
* strategic initiative selection;
* epic preparation audit;
* create epic;
* split epic;
* realign epic;
* reimagine epic;
* milestone deep dives.

### M5.2 Eliminate orchestration verbosity

Transition definitions should retain only workflow-specific declarations and behavior.

The canonical runtime should own:

* input resolution;
* prompt assembly;
* execution;
* parsing dispatch;
* validation sequence;
* persistence boundaries;
* evidence;
* blockers;
* state advancement.

### M5.3 Preserve Roadmap rigor

Preserve or improve:

* prompt contracts;
* projection freshness;
* input snapshots;
* artifact promotion validation;
* lifecycle;
* decision ledger;
* blockers;
* recovery intent;
* transition evidence.

### M5.4 Remove legacy execution ownership

Do not migrate Roadmap states whose only purpose was the retired Roadmap-to-execution bridge unless required solely for compatibility reading.

Legacy persisted states should be:

* recognized;
* reported;
* migrated or mapped safely;
* prevented from dictating the new active model.

### M5.5 Traditional Roadmap exit contract

The workflow must produce the validated product set required by Plan.

This must resolve the current mismatch between:

* active epic and milestone-spec outputs;
* Plan's required epic/spec input representation.

The repository may retain existing file forms, but the semantic product contract must be singular and explicit.

## Exit gate

M5 is complete when:

* the Traditional Roadmap workflow runs through the canonical runtime;
* it reaches a canonical workflow-complete state suitable for Plan;
* current behavioral coverage passes;
* transition definitions are materially smaller and less repetitive;
* legacy execution-preparation states no longer shape active orchestration.

---

# M6 — Plan Workflow Migration

## Objective

Replace the fixed `PlanPipeline` orchestration with explicit stages and prompt transitions under the canonical runtime.

## Required stages

The Plan workflow should preserve the domain sequence represented by:

1. Plan authoring.
2. Adversarial review.
3. Plan revision.
4. Operational-detail and milestone materialization.
5. Plan workflow completion.

The exact grouping should prioritize coherent gates and recovery over one-to-one preservation of current method boundaries.

## Required work

### M6.1 Explicit Plan state

Introduce durable workflow and stage state sufficient to distinguish:

* not started;
* authoring complete;
* review complete;
* revision complete;
* operational products partially materialized;
* completed;
* blocked;
* failed;
* cancelled.

### M6.2 Warm-session support

Preserve the required relationship between initial plan authoring and revision using the canonical prompt execution model.

Warm-session behavior must be an execution posture, not a separate workflow architecture.

### M6.3 Scoped artifact operations

Migrate:

* details collection;
* milestone extraction;
* details extraction.

Preserve:

* allowed reads and writes;
* no-delete constraints;
* required-output gates;
* changed-output gates;
* rollback where currently supported;
* HITL evidence.

### M6.4 Durable partial-state semantics

Replace the current ambiguity where existing Plan outputs merely block a fresh run.

The migrated workflow must know which validated stage produced each required product and whether the workflow can resume safely.

### M6.5 Plan exit contract

Plan completes only when it has produced an execution-ready product set including:

* executable plan;
* operational context;
* execution details where required;
* milestone set with machine-trackable completion items;
* provenance tying the products to the current upstream roadmap product.

## Exit gate

M6 is complete when:

* Plan runs through the canonical transition runtime;
* partial Plan output is durably distinguishable from completed Plan;
* Plan can resume safely at the correct stage;
* Plan workflow completion automatically satisfies or fails the Execute entry gate;
* the old pipeline is no longer the active orchestration authority.

---

# M7 — Execute Workflow Migration

## Objective

Model Main execution as explicit stages and transitions while preserving its iterative operational behavior.

## Scope

Execution from entry readiness through certified closure.

## Required stages

At minimum, preserve domain phases for:

1. Execution readiness.
2. Decision preparation.
3. Implementation slice.
4. Handoff and review.
5. Progress or stall evaluation.
6. Completion review and certification.
7. Execution workflow closure.

Iteration remains valid inside the workflow.

## Required work

### M7.1 Explicit execution state

Persist enough state to distinguish:

* first execution;
* awaiting decision proposal;
* live decisions ready;
* implementation turn in progress;
* handoff required;
* review required;
* publication required;
* commit evaluation required;
* stalled;
* completion claim;
* certification blocked;
* certified closed.

### M7.2 Decision session integration

Preserve:

* warm or resumable decision sessions;
* projection freshness checks;
* Continue versus Transfer routing;
* operational context evolution;
* decision history;
* live decision consumption.

Express these as canonical transitions or transition execution postures rather than a parallel mini-runtime.

### M7.3 Execution slice

Preserve:

* execution prompt;
* handoff-generation prompt;
* required handoff output;
* post-execution review;
* `.agents` publication;
* real Git commit and push behavior;
* no-progress detection.

### M7.4 Completion authority

Make one canonical execution-closure path authoritative.

It must include:

* milestone completion claim;
* non-implementation completion review;
* completion evaluation;
* policy validation;
* route interpretation;
* archive effects;
* roadmap completion-context update;
* durable closed-state evidence.

Compatibility callers may delegate to this path, but must not duplicate closure ownership.

### M7.5 Durable closed-state marker

Establish repository-owned evidence that remains discoverable after live Plan and milestone artifacts are archived.

A subsequent invocation must recognize completed execution without falling back into implementation.

### M7.6 Partial completion recovery

Ensure the canonical runtime can distinguish and resume safely after failures during:

* completion review;
* evaluation;
* archive materialization;
* archive synthesis;
* roadmap context update;
* final state persistence.

### M7.7 Stall semantics

Preserve existing no-progress behavior, but make the relevant state and evidence durable enough for orchestrator resolution.

## Exit gate

M7 is complete when:

* Execute runs through the canonical runtime;
* execution stage can be resolved after process restart;
* decision, implementation, handoff, publication, commit, and completion paths are explicit;
* only one completion authority exists;
* already-closed execution is durable and idempotently discoverable;
* current Main CLI behavior is preserved or deliberately superseded by documented canonical behavior.

---

# M8 — Eval Roadmap Implementation

## Objective

Implement the explicit EvalRoadmap workflow using the canonical runtime.

## Workflow sequence

The workflow should support the intended progression:

```text
next-eval.md
    ↓
eval-dependency-inventory.md
    ↓
eval-hypothesis-inventory.md
    ↓
eval-architectural-catalog.md
    ↓
eval-dag.md
    ↓
next-epic-roadmap.md
    ↓
epic.md
    ↓
GenerateMilestoneDeepDivesForEpic
    ↓
Plan entry gate
```

## Required work

### M8.1 Eval workflow entry gate

Require the eval input set selected for the current repository.

Default workflow selection is triggered by the existence of:

```text
.agents/evals/*.md
```

The workflow itself must still validate its exact input requirements.

### M8.2 Eval transitions

Create explicit prompt transitions for:

* next evaluation selection or interpretation;
* dependency inventory;
* falsifiable hypothesis inventory;
* architectural catalog;
* evaluation DAG;
* next epic roadmap;
* epic generation;
* milestone deep dives.

### M8.3 Output gates

Each transition must verify the specific output required by the next transition.

No next stage may run merely because the prompt returned successfully.

### M8.4 Dependency topology

Represent dependencies between Eval transitions declaratively.

Execution remains serial in this epic.

The model must not prevent future independent branches from executing concurrently.

### M8.5 Eval-to-Plan convergence

EvalRoadmap must produce the same semantic downstream product contract as TraditionalRoadmap.

Plan must not need separate orchestration logic depending on which roadmap workflow produced the prepared epic.

### M8.6 Recovery and evidence

Provide the same baseline rigor as TraditionalRoadmap:

* durable stage state;
* transition evidence;
* input provenance;
* output validation;
* blocker semantics;
* safe resume;
* unambiguous workflow completion.

## Exit gate

M8 is complete when:

* `EvalRoadmap` is a first-class workflow identity;
* it can run and resume through the canonical runtime;
* it converges on the same Plan entry contract as `TraditionalRoadmap`;
* workflow definitions express dependency topology without implementing concurrency;
* no Plan or Execute code branches on roadmap producer identity.

---

# M9 — Unified CLI, Compatibility Retirement, and Certification

## Objective

Make the unified CLI the authoritative user entry point and retire redundant orchestration paths.

## Required work

### M9.1 Unified command implementation

Deliver:

```text
looprelay
looprelay --eval
looprelay --traditional
looprelay eval
looprelay traditional
looprelay plan
looprelay execute
```

### M9.2 Automatic chain behavior

Verify:

* default invocation selects Eval when `.agents/evals/*.md` exists;
* default invocation selects Traditional otherwise;
* `--eval` and `--traditional` override automatic selection;
* chained modes continue through Plan and Execute;
* bounded subcommands stop after one workflow;
* execution closure ends the chain.

### M9.3 Automatic storage verification

Ensure every mutating orchestration invocation performs verification first.

Define user-visible outcomes for:

* verified;
* stale export;
* conflict;
* corruption;
* unsupported schema;
* ambiguous authority;
* partial workflow transaction.

Do not silently repair.

### M9.4 Status and explainability

The unified CLI should be able to explain:

* selected invocation mode;
* selected workflow chain;
* current workflow;
* current stage;
* next eligible transition;
* satisfied gates;
* unsatisfied gates;
* blockers;
* storage authority;
* whether user action is required.

### M9.5 Recovery surface

Unify recovery routing sufficiently that the CLI can direct a blocked repository to the applicable workflow recovery behavior without pretending every blocker is automatically repairable.

### M9.6 Legacy compatibility

Provide safe handling for repositories containing:

* old Roadmap persisted states;
* partial Plan artifacts;
* old decision-session resume state;
* legacy filesystem state;
* SQLite imported/canonical state;
* legacy CLI-produced evidence.

Compatibility may involve adapters or migrations, but old orchestration implementations must not remain competing authorities indefinitely.

### M9.7 Retire redundant entry points

After parity and migration are certified:

* retire or delegate old CLI orchestration roots;
* remove duplicated workflow sequencing;
* remove duplicated completion ownership;
* remove legacy Roadmap execution-preparation progression;
* retain only compatibility surfaces justified by supported repository migrations.

### M9.8 Architecture certification

Certify the new architecture against:

* behavioral parity tests;
* chain progression tests;
* stage resume tests;
* blocker tests;
* cancellation tests;
* storage-authority tests;
* output-gate failure tests;
* partial effect recovery tests;
* archive idempotency tests;
* bounded subcommand tests;
* default workflow detection tests;
* Eval/Traditional convergence tests.

## Exit gate

M9 is complete when:

* the unified CLI is the primary intended usage;
* all four workflows use the canonical runtime;
* both roadmap workflows converge into the same Plan and Execute chain;
* workflow and stage detection are repository-driven and explainable;
* storage verification is automatic;
* no hidden CLI-to-CLI chaining remains;
* duplicate orchestration authorities are retired;
* the architecture is certified against failure, recovery, and compatibility cases.

---

# 8. Cross-Milestone Dependency Order

```text
M0
 ↓
M1
 ↓
M2
 ↓
M3
 ↓
M4
 ├──────────────┐
 ↓              │
M5              │
 ↓              │
M6              │
 ↓              │
M7              │
 ↓              │
M8 ◄─────────────┘
 ↓
M9
```

Interpretation:

* M0–M4 form the shared foundation.
* Traditional Roadmap should migrate before Plan because it is the strongest existing reference and upstream producer.
* Plan should migrate before Execute because it establishes the downstream entry contract.
* Execute should migrate before final CLI cutover because chained execution depends on durable completion behavior.
* EvalRoadmap should be implemented after the common Roadmap-to-Plan contract is proven, but its detailed transition development may begin in parallel with later migration work once M4 and the relevant M5 contracts are stable.
* M9 depends on all workflow migrations.

---

# 9. Required Architectural Tests

## 9.1 Transition contract tests

For every transition:

* missing required input blocks;
* stale required input blocks when freshness is required;
* prompt failure does not mark transition complete;
* malformed output does not satisfy the output gate;
* missing output does not satisfy the output gate;
* invalid output remains evidence but not a usable product;
* partial effect failure is observable;
* cancellation remains cancellation.

## 9.2 Stage resolution tests

For every workflow:

* clean start resolves the first stage;
* completed transition resolves the next stage;
* partial transition resolves recovery or blocked state;
* stale product does not produce false advancement;
* completed workflow does not restart from stage one;
* conflicting evidence produces ambiguity or block.

## 9.3 Workflow boundary tests

* TraditionalRoadmap output satisfies Plan entry.
* EvalRoadmap output satisfies the same Plan entry.
* Invalid Roadmap output does not start Plan.
* Partial Plan does not start Execute.
* Valid completed Plan starts Execute.
* Execute completion ends the current chain.
* Single-workflow commands do not auto-chain.

## 9.4 Storage verification tests

* missing DB;
* valid empty DB;
* imported DB;
* canonical DB;
* stale filesystem export;
* conflicting filesystem and DB state;
* corrupt DB;
* unsupported schema;
* partial workflow transaction;
* verification remains non-mutating.

## 9.5 CLI mode tests

| Invocation                     | Expected chain               |
| ------------------------------ | ---------------------------- |
| `looprelay` with eval files    | Eval → Plan → Execute        |
| `looprelay` without eval files | Traditional → Plan → Execute |
| `looprelay --eval`             | Eval → Plan → Execute        |
| `looprelay --traditional`      | Traditional → Plan → Execute |
| `looprelay eval`               | Eval only                    |
| `looprelay traditional`        | Traditional only             |
| `looprelay plan`               | Plan only                    |
| `looprelay execute`            | Execute only                 |

## 9.6 Recovery tests

* interruption before prompt;
* interruption after prompt but before output validation;
* interruption after output validation but before effects;
* interruption during effects;
* interruption after effects but before transition completion persistence;
* Plan partial-stage resume;
* Execute interrupted handoff;
* Execute interrupted publish;
* completion interrupted during archive;
* completion interrupted during context update;
* already-closed invocation.

---

# 10. Explicit Non-Goals

The current roadmap does not include:

* looping from Execute closure back into either Roadmap workflow;
* concurrent transition execution;
* parallel Eval branches;
* telemetry-driven workflow decisions;
* confidence scoring;
* telemetry-derived confidence;
* multi-repository orchestration;
* distributed orchestration;
* remote workflow coordination;
* speculative generalized workflow UI;
* automatic storage repair;
* automatic conflict resolution;
* redesign of every prompt's substantive content;
* unrelated cleanup of all `.agents` artifacts.

The architecture should remain compatible with future concurrency and confidence capabilities without implementing them now.

---

# 11. Completion Criteria

The Canonical Orchestration Architecture program is complete when all of the following are true:

1. A single CLI is the primary intended surface.
2. Workflow identities are explicit.
3. Default roadmap mode detection follows `.agents/evals/*.md`.
4. Default, `--eval`, and `--traditional` invocations auto-chain.
5. Single-workflow subcommands remain bounded.
6. Storage verification is automatic and non-repairing.
7. Every workflow is defined through the canonical workflow, stage, transition, gate, and product contracts.
8. Every transition runs through the canonical prompt-transition lifecycle.
9. Prompt rendering is concise and separated from persistence, validation, and effects.
10. Workflow boundaries use the same output/input gate model as internal transition boundaries.
11. TraditionalRoadmap and EvalRoadmap satisfy the same Plan entry contract.
12. Plan satisfies one explicit Execute entry contract.
13. Execute has durable stage and completion state.
14. Certified closure is idempotently discoverable after archival.
15. Workflow and stage resolution are explainable from repository-owned evidence.
16. Partial progress, blockers, failures, and cancellation remain distinguishable.
17. Current serial execution does not hard-code an inherently linear-only workflow topology.
18. Duplicate orchestration and completion authorities have been retired.
19. Existing supported repository states can be migrated or interpreted safely.
20. The full behavior and failure certification suite passes.

---

# 12. Final Architectural Direction

The architecture should make individual workflow definitions small, explicit, and domain-focused.

A workflow definition should primarily communicate:

```text
What stages exist?

What transitions exist?

What does each transition require?

What prompt does it run?

What must it produce?

How is that output validated?

What becomes eligible next?
```

The shared orchestration runtime should own:

```text
How inputs are resolved

How storage authority is verified

How prompts are rendered and executed

How outputs are interpreted and gated

How effects are applied

How evidence is persisted

How stages and workflows are resolved

How blockers and recovery are represented

How workflow chains advance
```

The result should not be Plan and Main rewritten to resemble the current Roadmap CLI.

It should be all four workflows expressed through a more elegant, rigorous, concise, and robust architecture that preserves the strongest Roadmap behaviors while removing the accidental complexity of its present implementation.
