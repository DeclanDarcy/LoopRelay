# Milestone 9: Unified CLI, Old CLI Retirement, and Certification

Objective: make the unified CLI the authoritative orchestration surface.

## Work

- [ ] Replace `src/LoopRelay.Cli/Services/Cli/CliArguments.cs` with unified parsing that supports:
  - [ ] Current-directory default repository.
  - [ ] `--repo <path>`.
  - [ ] `--eval`.
  - [ ] `--traditional`.
  - [ ] Bounded workflow subcommands.
  - [ ] Status, unblock, and storage subcommands.
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
  - [ ] Integrations for prompts, agents, permissions, projections, artifacts, completion, Git, and publication.
- [ ] Retire old CLI entry points:
  - [ ] `LoopRelay.Roadmap.Cli` no longer ships as a public executable.
  - [ ] `LoopRelay.Plan.Cli` no longer ships as a public executable.
  - [ ] The old Execute invocation no longer ships as a separate public executable.
  - [ ] Reusable domain code may remain only as internal services invoked by `src/LoopRelay.Cli`.
- [ ] Update publish scripts:
  - [ ] `publish-cli.bat` publishes the unified executable.
  - [ ] `publish-plan-cli.bat` and `publish-roadmap-cli.bat` are retired or changed to fail with migration guidance to `publish-cli.bat`.
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
- [ ] Keep universal-CLI migration readers for:
  - [ ] Old roadmap state.
  - [ ] Partial Plan artifacts.
  - [ ] Old decision-session resume state.
  - [ ] Filesystem exports.
  - [ ] Imported/canonical SQLite states.
  - [ ] Pre-unification transition journals.
  - [ ] Pre-unification lifecycle rows.
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
- [ ] Old CLI entry points are retired and no longer ship as public executable surfaces.
- [ ] `dotnet test LoopRelay.slnx` passes.
