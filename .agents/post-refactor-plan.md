# Post-Refactor Plan

## Purpose

This plan holds the maturity-audit work that should wait until after the Planning -> Relay refactor. These items are either rooted in the existing planning worldview or expose public contracts that should describe the relay runtime, not the transitional plan/roadmap/epic machinery.

Source inputs:

- `.agents/maturity-audit.md`
- `.agents/planning-to-relay-audit.md`
- Planning -> Relay sequencing guidance captured in the attached review note

The post-refactor plan begins only after the codebase has an executable relay path where plans, roadmaps, epics, certifications, and reports are projections over the runtime rather than the runtime itself.

## Preconditions

Do not begin this plan until:

- The pre-refactor infrastructure plan is complete.
- At least one relay cycle is executable.
- The runtime can choose a highest-leverage next action from observed reality, human direction, constraints, unknowns, and architectural constitution checks.
- Markdown planning artifacts are projections or historical evidence, not the source of runtime authority.
- The legacy plan/roadmap/epic flow is frozen behind compatibility boundaries or explicitly marked as transitional.

## Architectural Philosophy Alignment Gate

Every item in this plan must preserve the relay thesis:

```text
RelayInput -> HighestLeverageNextAction -> ObservedRealityUpdate
```

Before implementing a delayed maturity item, verify:

- Does the design make observed reality more authoritative than narrative plans?
- Does it keep human direction first-class and inspectable?
- Does it expose assumption bets before they accumulate descendants?
- Does it treat roadmaps, plans, and epics as projections when they still exist?
- Does it reduce semantic ceremony rather than renaming the old planning pipeline?
- Does it improve executable evidence or the interpretation of that evidence?

If an item would rebuild the old planning pipeline under new names, stop.

## Revised Sequencing

### 1. Stabilize the Relay Runtime

Audit coverage:

- Planning -> Relay audit migration path
- Post-refactor replacement for maturity-audit Stage 4

Work:

- Make the relay loop explicit: observe reality, reason from north star and constraints, expose assumption bets, challenge with directional authority, challenge with architectural constitution, select highest-leverage next action, act, interpret observation, and update observed reality.
- Keep the first runtime narrow. Prefer one reliable executable relay cycle over a large schema framework.
- Make canonical JSON the source of truth for observed reality and relay state; keep Markdown as rendered summaries.

Validation:

- Tests for one full relay cycle from input state to selected action to observed reality update.
- Evidence freshness and provenance checks reused from the pre-refactor artifact infrastructure.

### 2. Decompose Roadmap Workflow Around Relay Semantics

Audit coverage:

- REF-1: Roadmap workflow orchestration is concentrated in one console-project class

Work:

- Split `RoadmapStateMachine` only after the target relay semantics are available.
- Reframe useful pieces as relay services: projection rendering, evidence interpretation, transition journaling, observed-reality updates, and compatibility reporting.
- Remove epic closure as the central objective. Keep evidence-based routing, but interpret outcomes against observed reality, human direction, and constitutional checks.

Validation:

- Transition-level tests for state JSON, Markdown projections, journals, lifecycle records, and evidence paths.
- Compatibility tests for legacy roadmap commands if they remain available.

Explicitly excluded:

- Recreating the roadmap state machine as a cleaner epic/milestone state machine.

### 3. Migrate Prompt and Runtime Contracts

Audit coverage:

- REF-9: Prompt catalogs and contracts are string-switch registries in a CLI project
- Planning -> Relay audit prompt migration findings

Work:

- Replace plan, roadmap, milestone, and certification prompts with relay prompts: observe reality, reason from evidence, challenge assumptions, act through bounded probes or capabilities, and update observed reality.
- Move prompt contract metadata out of the CLI project into relay or application-level contracts.
- Preserve generated prompt resources in `LoopRelay.Core` or an equivalent resource layer.
- Treat stale prompt/projection entries as quarantine or removal candidates, especially prompts already identified in `prune-candidates.md`.

Validation:

- Prompt contract tests for input/output schema, source hashing, projection freshness, and allowed decisions.
- Golden tests for relay prompt rendering that prove artifacts are embedded as data, not as unchallenged authority.

### 4. Define Repository Identity and Command Model

Audit coverage:

- REF-5: No shared command model exists yet
- REF-10: Repository identity and configuration are ephemeral and app-local
- UX-1 through UX-3, as public contract work

Work:

- Define stable repository context: filesystem root, display name, process session identity, durable local config identity, and compatibility state location.
- Define the public relay command grammar after the runtime is stable.
- Prefer relay-first command names. Keep `plan` and `roadmap` as compatibility or projection commands only if they remain useful.
- Model help, version, exit codes, diagnostics, and trust flags as part of the same command contract.

Validation:

- Process-level CLI tests for valid commands, bad args, `--help`, `--version`, missing repo, exit codes, and compatibility wrappers.
- Migration tests for existing `.LoopRelay` state if durable identity changes.

Explicitly excluded:

- Finalizing command names before relay runtime behavior is clear.

### 5. Introduce the Unified CLI

Audit coverage:

- Maturity-audit Stage 6: Introduce Unified CLI With Subcommands
- Consolidation readiness assessment
- UX-2 command contract differences

Work:

- Add one command host that delegates to relay and compatibility services.
- Keep old executables as thin wrappers or documented aliases for one migration window.
- Route diagnostics, repository context construction, trust policy selection, cancellation, output rendering, and exit-code mapping through shared infrastructure.
- Avoid deep state unification unless runtime evidence shows that loop, plan, and roadmap state can safely share more than primitives.

Validation:

- Process-level tests for the unified CLI and legacy wrappers.
- Compatibility tests for existing scripts that call current executables with positional repository paths.

### 6. Finish CLI UX and Documentation

Audit coverage:

- UX-1: Help and usage are parse-error-only
- UX-2: User-facing command contract differs between apps
- UX-3: Exit codes are implemented but not centralized as a public contract
- UX-4: Main loop success blocks automation with an interactive prompt
- Final UX-5 and UX-6 public surfaces
- OSR-6: Internal docs and issue notes are stale or broader than the checked-in CLI repo

Work:

- Add discoverable help, examples, command descriptions, environment variables, trust-mode descriptions, and exit-code documentation for the relay CLI.
- Remove or gate interactive success prompts so automation is script-safe by default.
- Update docs to describe relay behavior and compatibility boundaries.
- Mark stale issue notes and broad architecture docs as resolved, historical, or archived.
- Ensure documentation makes clear that plans and roadmaps are projections, not runtime authority.

Validation:

- Documentation quickstart smoke test.
- CLI help snapshot tests where useful.
- Stale-doc metadata or linting for docs that claim to describe current behavior.

### 7. Complete Open-Source Hardening

Audit coverage:

- Maturity-audit Stage 7: Open-Source Release Hardening
- OSR-1: Missing public onboarding, license, contribution, and CI baseline
- OSR-3: Packaging is Windows-local rather than distributable
- Final OSR-4 public `.agents` strategy

Work:

- Add root `README.md`, `LICENSE`, `CONTRIBUTING.md`, code of conduct if the project accepts external contributors, and issue/PR templates when appropriate.
- Add CI for clean-clone restore/build/test and package smoke tests.
- Add package metadata, version stamping, and release automation for the chosen distribution mode.
- Replace Windows-local publish scripts with cross-platform scripts or keep them as local convenience wrappers over the official release path.
- Decide and document the long-term `.agents` strategy: submodule metadata, artifact directory, external store, or relay state projection.

Validation:

- Clean-clone CI passes.
- Published artifact or tool package installs and runs smoke tests on supported platforms.
- Public quickstart works without undocumented local machine state.

### 8. Consider Deeper State Unification

Audit coverage:

- Consolidation readiness "Should Happen After Merge"
- Artifact/state findings from REF-4 and REF-10

Work:

- Evaluate whether relay, legacy loop, legacy plan, and roadmap projections share enough semantics to justify deeper state unification.
- Unify only around observed reality, evidence, provenance, trust posture, repository context, and projection metadata.
- Keep workflow-specific histories isolated where unification would hide important differences.

Validation:

- Round-trip tests for old and new state.
- Migration tests for any persisted user data.
- Evidence that the unified model reduces complexity instead of only creating a larger abstraction.

## Post-Refactor Work Item Matrix

| Audit ID | Post-refactor disposition | Reason |
| --- | --- | --- |
| REF-1 | Do after relay semantics exist | The roadmap state machine should be redesigned around relay services, not polished as planning machinery. |
| REF-5 | Do after relay command shape is known | Final command grammar must describe the relay, not the old plan/roadmap flow. |
| REF-9 | Do after prompt migration begins | Prompt contracts will change from planning/milestone prompts to relay prompts. |
| REF-10 | Do with command/config model | Durable repository identity should support cross-command relay behavior. |
| Stage 6 | Do after relay runtime stabilization | The unified CLI should expose the architecture the project believes in. |
| Stage 7 | Do after stable command surface | Public packaging and docs should not codify transitional architecture. |
| OSR-1 | Do after stable public behavior | README, contribution, and onboarding should describe relay-first usage. |
| OSR-3 | Do after command names stabilize | Distribution artifacts should publish the final CLI surface. |
| OSR-4 | Finish after relay artifact ownership is known | `.agents` strategy depends on relay state and projection ownership. |
| OSR-6 | Do with docs rewrite | Stale docs should be resolved against the relay architecture. |
| UX-1 | Do after command model | Help text should teach the relay CLI. |
| UX-2 | Do after command model | Users need one relay-first grammar, not three old executable shapes. |
| UX-3 | Finish after command model | Public exit codes belong to the unified command contract. |
| UX-4 | Do with CLI UX | Script-safe behavior should be part of final public CLI behavior. |
| UX-5 | Finish after command model | Diagnostics should be exposed through the final CLI surface. |
| UX-6 | Finish after trust flags are public | Trust posture should be selectable and visible to users. |

## Exit Criteria

This plan is complete when:

- Relay, not planning, is the primary runtime abstraction.
- Roadmaps, plans, epics, certifications, and reports are projections or compatibility surfaces.
- The unified CLI exposes relay-first commands with documented help, version, diagnostics, trust policy, and exit-code behavior.
- Prompt contracts operate on observed reality, assumption exposure, executable probes, and evidence interpretation.
- Public onboarding, packaging, CI, release, and contribution materials describe the stable relay architecture.
- Legacy plan/roadmap behavior is either removed, quarantined, or explicitly maintained as compatibility tooling.
