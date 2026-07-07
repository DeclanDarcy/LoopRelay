# Pre-Refactor Plan

## Purpose

This plan applies the maturity audit through the Planning -> Relay sequencing guidance. It identifies the work that should happen before the relay refactor because it improves safety, reproducibility, infrastructure reuse, and architectural hygiene without investing further in the classical plan/roadmap/epic runtime.

Source inputs:

- `.agents/maturity-audit.md`
- `.agents/planning-to-relay-audit.md`
- Planning -> Relay sequencing guidance captured in the attached review note

The operating rule is simple: complete only work that survives the relay architecture. Pause work whose main effect is making the existing planning pipeline, roadmap state machine, milestone flow, prompt grammar, or public CLI surface more elegant.

This plan also makes one explicit promise: no additional planning architecture will be designed until the first executable relay cycle exists. Previous abstractions were created before executable reality could test them; this plan exists partly to prevent that failure mode from repeating.

## Architectural Philosophy Alignment Gate

Run this gate before starting any item from this plan:

- Does the work reduce risk without making classical planning more central?
- Does this work make the repository more dependent on classical planning concepts?
- Does it preserve human directional authority rather than transferring authority to plans, epics, or roadmap artifacts?
- Does it increase executable evidence, observed reality, reproducibility, or permanent context quality?
- Does it avoid treating milestones, epic closure, or roadmap progress as the runtime's source of truth?
- Does it avoid adding semantic ceremony that the relay refactor will have to delete?
- Does it leave planning artifacts as projections or legacy evidence rather than runtime authority?
- If the relay runtime replaced plans tomorrow, would this work still provide value?

If an item fails the gate, move it to `post-refactor-plan.md` or discard it.

## Pre-Refactor Scope

### 0. Relay Foundations Freeze

Audit coverage:

- Planning -> Relay audit migration path
- Architectural philosophy alignment guidance

Work:

- Freeze planning architecture evolution.
- Freeze roadmap architecture evolution.
- Freeze prompt proliferation.
- Freeze permanent-context growth.
- Freeze semantic ceremony.
- Treat plan, roadmap, epic, milestone, and certification language as legacy vocabulary unless the work explicitly demotes it to projection or compatibility status.

Validation:

- Every item in this plan passes the Architectural Philosophy Alignment Gate before implementation.
- Anti-regression review is performed after each stage that introduces new shared abstractions or persistent artifacts.

Explicitly excluded:

- New planning model design.
- New roadmap state-machine concepts.
- New permanent context unless it is required for infrastructure safety and passes the gate.
- New prompt families before the first executable relay cycle exists.

### 1. Baseline and Characterization Tests

Audit coverage:

- Testing additions for shared infrastructure
- Testing and validation gaps for shared console rendering, git publishing, artifact namespace compatibility, trust policy, and clean-clone build

Work:

- Add characterization tests for current console rendering, turn rendering, exit mapping, git publisher behavior, artifact path ownership, evidence numbering, sandbox mapping, and trust policy defaults.
- Keep tests focused on observable compatibility, not the future command grammar.
- Preserve current behavior before extraction so relay migration can distinguish intentional architectural change from accidental regression.

Validation:

- `dotnet test LoopRelay.slnx --no-restore --nologo`

Explicitly excluded:

- New help grammar, public command examples, shell completions, or version-output design.
- New roadmap/epic/milestone workflow tests beyond what is needed to protect infrastructure extraction.

### 2. Self-Contained Build and Dependency Governance

Audit coverage:

- OSR-2: Build is not self-contained or SDK-pinned
- OSR-7: Dependency and version governance is ad hoc

Work:

- Remove or package the sibling `dotnet-libraries/Lib.Prompts` build dependency so a clean clone can restore and build.
- Add `global.json` for the intended .NET SDK.
- Introduce central package management with `Directory.Packages.props`.
- Decide whether package lock files are required now; if not, document why.
- Add a clean-clone build/test check once the build no longer depends on machine-local sibling paths.

Validation:

- Restore and test from a clean checkout without relying on sibling repositories or pre-restored artifacts.

Explicitly excluded:

- Public release packaging.
- NuGet/global-tool publishing.
- Public README/install polish beyond minimal build notes needed to validate this step.

### 3. Shared Console Rendering and Diagnostic Primitives

Audit coverage:

- REF-2: Console rendering and turn diagnostics are duplicated across apps
- The infrastructure-safe portion of UX-3: shared result/exit-code primitives

Work:

- Extract `LoopConsole` and `ConsoleTurnRenderer` into shared infrastructure or an application-neutral support project.
- Preserve current output behavior with golden or equivalent characterization tests.
- Introduce internal result/exit-code primitives only as needed to remove duplicated host code.

Validation:

- Existing console and renderer tests ported to the shared implementation.
- Process smoke tests for current executable behavior, limited to compatibility checks.

Explicitly excluded:

- Final public exit-code contract.
- New command grammar or help text.

### 4. Shared Git and `.agents` Publishing Infrastructure

Audit coverage:

- REF-3: Git and `.agents` submodule publishing logic is duplicated and app-local
- The infrastructure-safe portion of OSR-4: `.agents` submodule behavior is undocumented and brittle

Work:

- Extract shared git porcelain parsing.
- Extract shared `.agents` publisher behavior with workflow-specific policies for cadence, commit messages, and failure translation.
- Preserve parent gitlink recording semantics and strict push recovery behavior.
- Document the current `.agents` assumption enough for maintainers to understand it during migration.
- Keep the abstraction repository-state and Git oriented. Whether artifact publishing remains a first-class relay responsibility should be determined by the relay architecture.

Validation:

- Port both existing publisher suites to the shared implementation.
- Add cases for detached HEAD, missing upstream, stranded push recovery, parent staging scope, and cadence differences.

Explicitly excluded:

- Final public artifact-storage strategy.
- Committing to `.agents` as the long-term relay state container before relay artifact ownership is decided.

### 5. Shared Artifact Access Primitives

Audit coverage:

- REF-4: Artifact access and path catalogs are fragmented

Work:

- Extract common repository-relative artifact read/write/list/rotate primitives.
- Keep workflow-specific path catalogs explicit.
- Create an artifact ownership matrix for existing `.agents` paths.
- All new artifact APIs must distinguish canonical state from projections, even where the current implementation still stores Markdown.
- Treat Markdown artifacts as renderings, compatibility outputs, or historical evidence rather than unchallenged runtime authority.

Validation:

- Tests for repository-boundary validation, rotation sequence, evidence numbering, sandbox copy-back, and ownership conflicts.

Explicitly excluded:

- State schema unification.
- Renaming plan/roadmap/epic artifacts into relay vocabulary before the relay runtime exists.

### 6. Thin Composition Roots

Audit coverage:

- REF-6: Composition roots construct application behavior directly in `Program.cs`

Work:

- Move service construction into registration/composition helpers.
- Keep each `Program.cs` focused on console setup, cancellation, command parse handoff, and result-to-exit-code mapping.
- Move behavioral ownership out of entry points, not merely wiring. The target is `Program -> Relay Runtime -> Planning as projection`, not `Program -> Application -> Planning`.
- Make this a behavior-preserving host cleanup that prepares the codebase for relay services later.

Validation:

- Composition smoke tests.
- Full test suite after each executable host is thinned.

Explicitly excluded:

- Moving roadmap workflow semantics into a new planning-oriented application model.
- Introducing the future unified command host.

### 7. Trust Policy Modeling and Runtime Diagnostics

Audit coverage:

- REF-7: Execution trust posture is hardcoded instead of modeled
- OSR-5: Required external executables and environment variables are undocumented prerequisites
- Infrastructure-safe parts of UX-5 and UX-6

Work:

- Introduce explicit trust policy records for sandbox, network, approval, and execution authority.
- Preserve current defaults where needed for compatibility, but make the selected posture visible in execution evidence.
- Add runtime prerequisite diagnostics for `CODEX_EXECUTABLE` and `LoopRelay_DECISION_RESUME`.
- Add a minimal internal doctor/preflight service that future relay commands can reuse.
- Shape trust policy as the seed of a constitutional subsystem, not just execution settings. It should be able to grow toward execution authority, permanent-context authority, document admission, pollution review, and relay authority.

Validation:

- Trust-mode tests for current and elevated execution profiles.
- Evidence tests proving selected trust posture is recorded.
- Diagnostics tests for missing or misconfigured executables.

Explicitly excluded:

- Final CLI flags for trust modes.
- Public help text for command families.
- Rewriting execution prompts into relay prompts.

### 8. Clean Up `Orchestration.Primitives`

Audit coverage:

- REF-8: `Orchestration.Primitives` is not a clean architectural layer

Work:

- Split or rename responsibilities so contracts, policy services, filesystem persistence, and path constants are not collapsed under a misleading "primitives" name.
- Do not preserve the existing project simply because it already exists. Split according to enduring architectural responsibilities rather than historical convenience.
- Keep dependency movement mechanical and covered by existing tests.
- Avoid adding new planning concepts to the shared layer.

Validation:

- Build graph remains acyclic and intentional.
- Existing decision-session router and resume-store tests continue to pass.

Explicitly excluded:

- Designing final relay runtime projects before one executable relay cycle exists.

### 9. Bridge Milestone: First Executable Relay Cycle

Audit coverage:

- Planning -> Relay audit migration path
- Architectural philosophy alignment guidance

Work:

- After infrastructure maturity, make the highest priority the smallest possible relay executing against a single North Star evaluation.
- Implement one narrow cycle: observe, interpret, choose, execute, observe.
- Use existing evidence and provenance machinery before introducing broad new schemas.
- Keep the implementation intentionally small: no complete runtime, no complete model set, no complete relay schema universe.

Validation:

- One executable relay cycle can run against the current repository and produce an observed-reality update or equivalent evidence-backed result.
- The cycle preserves HITL directional authority by surfacing what was chosen, why it was chosen, what assumption it bets on, and where human direction can override or redirect it.

Explicitly excluded:

- Designing broader relay architecture before this one cycle exists.
- Rebranding planning, roadmap, or milestone selection as relay selection without changing the authority model.

## Anti-Regression Review

Run this review after each stage and before declaring the pre-refactor plan complete:

- Did any new permanent context get introduced?
- Did any new roadmap machinery appear?
- Did any new semantic ceremony appear?
- Did any infrastructure accidentally hardcode planning assumptions?
- Did any abstraction increase coupling to epics or milestones?
- Did the work make future relay execution more dependent on artifacts whose authority comes from planning narrative rather than observed reality?

If the answer to any question is yes, either remove the change, quarantine it as legacy compatibility, or move it to `post-refactor-plan.md`.

## Pre-Refactor Work Item Matrix

| Audit ID | Pre-refactor disposition | Reason |
| --- | --- | --- |
| REF-2 | Do now | Pure shared console infrastructure. |
| REF-3 | Do now | Relay still needs repository-state and Git abstractions; artifact publishing may remain or may be demoted by the relay architecture. |
| REF-4 | Do now, limited to primitives and ownership | Relay needs artifact abstractions; defer vocabulary and state unification. |
| REF-6 | Do now | Thin hosts make future relay architecture easier. |
| REF-7 | Do now | Trust policy is the seed of the constitutional/runtime authority model. |
| REF-8 | Do now | Clarifies enduring shared responsibility boundaries before migration. |
| OSR-2 | Do now | Clean build is independent of planning philosophy. |
| OSR-5 | Do now, limited to diagnostics services | Prerequisite discovery survives the relay design. |
| OSR-7 | Do now | Dependency governance is pure engineering maturity. |
| UX-3 | Partial now | Internal result/exit primitives only; public contract waits. |
| UX-5 | Partial now | Shared diagnostics service now; public UX waits. |
| UX-6 | Partial now | Model and record trust posture now; final CLI options wait. |

## Deferred From This Plan

Move these to `post-refactor-plan.md`:

- REF-1 roadmap state-machine decomposition, except for narrow characterization tests.
- REF-5 unified command model.
- REF-9 prompt catalogs and prompt contract migration.
- REF-10 durable repository identity and cross-command configuration, except for small interfaces needed by diagnostics.
- Stage 6 unified CLI.
- Stage 7 open-source hardening.
- OSR-1, OSR-3, OSR-6 public documentation, packaging, stale-doc cleanup, and release polish.
- UX-1, UX-2, final UX-3, UX-4, final UX-5, and final UX-6 public command behavior.
- Any work whose main purpose is making `plan -> epic -> milestones -> completion` cleaner.

## Exit Criteria

This plan is complete when:

- A clean clone can restore, build, and test without private sibling paths.
- Shared console, git publishing, artifact access, diagnostics, and trust policy primitives exist and are tested.
- Console apps have thin composition roots.
- `Orchestration.Primitives` no longer hides mixed policy and infrastructure responsibilities under a misleading layer name.
- No major new public command grammar, prompt grammar, roadmap semantics, or planning-pipeline polish has been introduced.
- The extracted infrastructure does not increase dependence on roadmap, milestone, or planning authority.
- The repository is better positioned for continuous evidence-driven relay execution while preserving HITL directional authority.
- The next highest-priority step is the smallest executable relay cycle, not broader planning-architecture design.
