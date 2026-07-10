# Milestone 9: Unified CLI, Old CLI Retirement, and Certification

Objective: make the unified CLI the authoritative orchestration surface.

## Work

- [x] Replace `src/LoopRelay.Cli/Services/Cli/CliArguments.cs` with unified parsing that supports:
  - [x] Current-directory default repository.
  - [x] `--repo <path>`.
  - [x] `--eval`.
  - [x] `--traditional`.
  - [x] Bounded workflow subcommands.
  - [x] Status, unblock, and storage subcommands.
- [x] Update `src/LoopRelay.Cli/Program.cs` to:
  - [x] Set UTF-8 output.
  - [x] Parse unified invocation.
  - [x] Run storage verification before mutating orchestration.
  - [x] Create unified composition.
  - [x] Execute `WorkflowChainRunner`.
  - [x] Map canonical outcomes to exit codes.
- [x] Add `UnifiedCliComposition` that wires:
  - [x] Repository observer.
  - [x] Storage verifier.
  - [x] Workflow resolver.
  - [x] Transition runtime.
  - [x] Workflow controller.
  - [x] Workflow definitions.
  - [x] Local verification effects materialize durable evidence files under `.LoopRelay/evidence/local-verification`.
  - [x] Generated prompt templates render through the unified runtime prompt renderer with source-hash evidence for registered canonical prompt assets.
  - [x] Integrations for prompts, agents, permissions, projections, artifacts, completion, Git, and publication.
- [x] Retire old CLI entry points:
  - [x] `LoopRelay.Roadmap.Cli` no longer ships as a public executable.
  - [x] `LoopRelay.Plan.Cli` no longer ships as a public executable.
  - [x] The old Execute invocation no longer ships as a separate public executable.
  - [x] Reusable domain code may remain only as internal services invoked by `src/LoopRelay.Cli`.
- [x] Update publish scripts:
  - [x] `publish-cli.bat` publishes the unified executable.
  - [x] `publish-plan-cli.bat` and `publish-roadmap-cli.bat` are retired or changed to fail with migration guidance to `publish-cli.bat`.
- [x] Add unified status output that explains:
  - [x] Invocation mode.
  - [x] Selected chain.
  - [x] Selected workflow.
  - [x] Current stage.
  - [x] Next eligible transition.
  - [x] Satisfied gates.
  - [x] Unsatisfied gates.
  - [x] Blockers.
  - [x] Storage authority.
  - [x] User action required, if any.
- [x] Add unified `unblock` execution that resolves recoverable canonical blockers and restores blocked canonical workflow state without invoking old CLI entry points.
- [x] Wire unified `storage import`, `storage export`, and `storage sync` to the shared workspace database contract behind the unified CLI surface.
- [x] Retire duplicate active authorities:
  - [x] Roadmap state-machine orchestration.
  - [x] Plan pipeline sequencing.
  - [x] Execution loop orchestration.
  - [x] Duplicate completion ownership.
  - [x] CLI-to-CLI chaining.
- [x] Keep universal-CLI migration readers for:
  - [x] Old roadmap state.
  - [x] Partial Plan artifacts.
  - [x] Old decision-session resume state.
  - [x] Filesystem exports.
  - [x] Imported/canonical SQLite states.
  - [x] Pre-unification transition journals.
  - [x] Pre-unification lifecycle rows.
  - [x] Completion archives.

## Detail Requirements

### Unified CLI Invocation Behavior

Primary invocation:

```text
looprelay
```

Performs storage verification, repository observation, workflow resolution, stage resolution, current workflow execution, and auto-chaining through downstream workflows.

Explicit chained modes:

```text
looprelay --traditional
looprelay --eval
```

Bounded modes:

```text
looprelay traditional
looprelay eval
looprelay plan
looprelay execute
```

### Repository Resolution Stack

Every invocation should perform the same stack:

```text
Storage verification
  -> Repository observation
  -> Workflow identity resolution
  -> Workflow eligibility
  -> Stage resolution
  -> Transition eligibility
```

There should be no duplicated discovery, storage validation, or stage selection in retired entry points.

### Unified User Experience Fields

The unified CLI should be able to report:

- current workflow
- current stage
- current transition
- current repository classification
- workflow chain
- blockers
- human interaction
- storage authority
- current products
- next eligible transition
- next eligible workflow

Every decision should identify evidence, authority, ignored evidence, conflicts, and uncertainty.

### Old CLI Retirement Responsibilities

Existing Roadmap, Plan, and Execute entry points should be retired as public executable surfaces. They should not translate arguments, delegate to the universal CLI, or preserve separate exit-code behavior.

Any reusable code from old CLI projects must be treated as internal/domain services owned by the universal composition. It must not be exposed as a second orchestration surface and must not compete as an authority.

### Old Orchestration Retirement Scope

Retire duplicate ownership of:

- Roadmap orchestration
- Plan pipeline orchestration
- Execution loop orchestration
- workflow progression
- prompt orchestration
- completion orchestration
- workflow discovery
- stage discovery
- transition sequencing

Preserve domain prompts, products, recovery, evidence, and behavior.

### Storage Integration

Automatic behavior:

- storage verification
- repository authority determination

Manual behavior:

- import
- export
- sync
- repair
- conflict resolution

Verification must never silently mutate repository state, and storage authority must always be visible.

### Certification Breakdown

M9 should separately certify:

- behavioral equivalence across TraditionalRoadmap, EvalRoadmap, Plan, Execute, Completion, Storage, Recovery, and CLI
- pre-unification repository migration across filesystem repositories, SQLite repositories, mixed repositories, exports, old state files, journals, lifecycle rows, execution state, roadmap state, decision resume, and completion archives
- orchestration architecture consistency

### Orchestration Certification Questions

M9 should answer:

- Is there exactly one orchestration authority?
- Is there exactly one workflow controller?
- Is there exactly one repository resolution engine?
- Is there exactly one transition runtime?
- Is there exactly one completion authority?
- Is there exactly one workflow chaining model?
- Does every workflow execute through identical orchestration?
- Can every orchestration decision be explained?
- Can every workflow resume correctly?
- Can every workflow chain automatically?

### Public Surface Freeze

M9 should freeze:

- CLI behavior
- workflow identities
- workflow chains
- workflow entry contracts
- workflow exit contracts
- repository resolution
- storage verification
- workflow progression
- workflow outcomes

Future evolution should extend the architecture, not redefine the public orchestration contract.

### Final Validation Matrix Additions

End-to-end validation should explicitly cover:

- `looprelay` with a Traditional repository
- `looprelay` with an Eval repository
- `looprelay --traditional`
- `looprelay --eval`
- `looprelay traditional`
- `looprelay eval`
- `looprelay plan`
- `looprelay execute`

For each, validate storage verification, workflow resolution, stage resolution, transition execution, workflow chaining, repository progression, completion, cancellation, recovery, pre-unification state migration, and explainability.

### Old Responsibility Retirement Matrix

Every retired responsibility should have exactly one new owner:

| Old Responsibility | New Owner |
|---|---|
| Roadmap state-machine orchestration | Workflow Controller + Transition Runtime |
| Plan pipeline sequencing | Workflow Controller + Transition Runtime |
| Execution loop orchestration | Workflow Controller + Transition Runtime |
| Prompt execution ownership | Transition Runtime |
| Stage progression | Workflow Controller |
| Workflow progression | Workflow Controller |
| Repository discovery | Repository Resolver |
| Workflow discovery | Repository Resolver |
| Stage discovery | Repository Resolver |
| Storage authority | Repository Resolver |
| Completion orchestration | Execute Workflow + Transition Runtime |
| CLI orchestration | Unified CLI |

### Architecture Closure Deliverables

The final closure package should contain:

- Canonical Runtime
- Repository Resolver
- Workflow Controller
- Workflow Contracts
- Workflow Definitions
- Unified CLI
- Behavioral Certification
- Migration Certification
- Architecture Certification

Final state:

- one Runtime
- one Resolver
- one Controller
- four Workflows
- two Workflow Chains
- one CLI
- one Orchestration Authority

## Acceptance

- [x] Required invocations work:
  - [x] `looprelay`
  - [x] `looprelay --eval`
  - [x] `looprelay --traditional`
  - [x] `looprelay eval`
  - [x] `looprelay traditional`
  - [x] `looprelay plan`
  - [x] `looprelay execute`
- [x] Default invocation selects EvalRoadmap when `.agents/evals/*.md` exists and TraditionalRoadmap otherwise.
- [x] Chained modes continue through Plan and Execute.
- [x] Bounded commands stop after one workflow.
- [x] Execute certified closure ends the chain.
- [x] Automatic storage verification is present and non-repairing.
- [x] Old CLI entry points are retired and no longer ship as public executable surfaces.
- [x] `dotnet test LoopRelay.slnx` passes.
