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
