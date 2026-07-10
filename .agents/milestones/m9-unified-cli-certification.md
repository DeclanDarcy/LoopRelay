# Milestone 9: Unified CLI, Compatibility Retirement, and Certification

Objective: make the unified CLI the authoritative orchestration surface.

## Work

- [ ] Replace `src/LoopRelay.Cli/Services/Cli/CliArguments.cs` with unified parsing that supports:
  - [ ] Current-directory default repository.
  - [ ] `--repo <path>`.
  - [ ] `--eval`.
  - [ ] `--traditional`.
  - [ ] Bounded workflow subcommands.
  - [ ] Status, unblock, and storage subcommands.
  - [ ] Legacy positional repository compatibility.
- [ ] Update `src/LoopRelay.Cli/Program.cs` to:
  - [ ] Set UTF-8 output.
  - [ ] Parse unified invocation.
  - [ ] Run storage verification before mutating orchestration.
  - [ ] Create unified composition.
  - [ ] Execute `WorkflowChainRunner`.
  - [ ] Map canonical outcomes to exit codes.
- [ ] Add `UnifiedCliComposition` that wires:
  - [ ] Repository observer.
  - [ ] Storage verifier.
  - [ ] Workflow resolver.
  - [ ] Transition runtime.
  - [ ] Workflow controller.
  - [ ] Workflow definitions.
  - [ ] Adapters for prompts, agents, permissions, projections, artifacts, completion, Git, and publication.
- [ ] Convert old CLI projects into thin compatibility surfaces:
  - [ ] `LoopRelay.Roadmap.Cli` translates legacy args and delegates to unified orchestration or storage compatibility.
  - [ ] `LoopRelay.Plan.Cli` delegates to bounded `Plan`.
  - [ ] Existing Execute invocation delegates to bounded `Execute`.
- [ ] Update publish scripts:
  - [ ] `publish-cli.bat` publishes the unified executable.
  - [ ] `publish-plan-cli.bat` and `publish-roadmap-cli.bat` either publish compatibility adapters or are retired after a documented compatibility decision.
- [ ] Add unified status output that explains:
  - [ ] Invocation mode.
  - [ ] Selected chain.
  - [ ] Selected workflow.
  - [ ] Current stage.
  - [ ] Next eligible transition.
  - [ ] Satisfied gates.
  - [ ] Unsatisfied gates.
  - [ ] Blockers.
  - [ ] Storage authority.
  - [ ] User action required, if any.
- [ ] Retire duplicate active authorities:
  - [ ] Roadmap state-machine orchestration.
  - [ ] Plan pipeline sequencing.
  - [ ] Execution loop orchestration.
  - [ ] Duplicate completion ownership.
  - [ ] CLI-to-CLI chaining.
- [ ] Keep compatibility readers for:
  - [ ] Old roadmap state.
  - [ ] Partial Plan artifacts.
  - [ ] Old decision-session resume state.
  - [ ] Filesystem exports.
  - [ ] Imported/canonical SQLite states.
  - [ ] Legacy transition journals.
  - [ ] Legacy lifecycle rows.
  - [ ] Completion archives.

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

There should be no duplicated discovery, storage validation, or stage selection in compatibility entry points.

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

### Compatibility Layer Responsibilities

Existing Roadmap, Plan, and Execute entry points should become thin adapters. Their responsibilities are argument translation, legacy compatibility, exit-code compatibility, and migration assistance.

Compatibility layers must not contain orchestration logic and must not compete as authorities.

### Legacy Retirement Scope

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
- legacy compatibility across filesystem repositories, SQLite repositories, mixed repositories, legacy exports, legacy state, legacy journals, legacy lifecycle, legacy execution state, legacy roadmap state, decision resume, and completion archives
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

For each, validate storage verification, workflow resolution, stage resolution, transition execution, workflow chaining, repository progression, completion, cancellation, recovery, legacy compatibility, and explainability.

### Legacy Retirement Matrix

Every retired responsibility should have exactly one new owner:

| Legacy Responsibility | New Owner |
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
- Compatibility Certification
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

- [ ] Required invocations work:
  - [ ] `looprelay`
  - [ ] `looprelay --eval`
  - [ ] `looprelay --traditional`
  - [ ] `looprelay eval`
  - [ ] `looprelay traditional`
  - [ ] `looprelay plan`
  - [ ] `looprelay execute`
- [ ] Default invocation selects EvalRoadmap when `.agents/evals/*.md` exists and TraditionalRoadmap otherwise.
- [ ] Chained modes continue through Plan and Execute.
- [ ] Bounded commands stop after one workflow.
- [ ] Execute certified closure ends the chain.
- [ ] Automatic storage verification is present and non-repairing.
- [ ] Legacy entry points no longer own orchestration.
- [ ] `dotnet test LoopRelay.slnx` passes.
