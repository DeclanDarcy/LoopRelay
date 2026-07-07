**Audit Verdict**

LoopRelay is already halfway out of classical planning, but the executable product is still organized around `plan -> epic -> milestones -> completion`. The evidence-driven pieces exist mostly as safeguards around that model, not as the primary relay loop.

The migration should go further than replacing roadmap planning with adaptive planning. The target should be continuous epistemic convergence:

> The purpose of the relay is not to maximize implementation progress. It is to maximize the rate at which the human and the repository converge toward demonstrable truth while preserving the human's ability to redirect the system at any moment.

That principle should govern the product. The relay should answer one question every cycle:

> What is the highest-leverage thing the repository can do next?

Everything else exists to support that answer.

```text
Observed reality
North star
Constraints
Unknowns
Human direction
Architectural constitution
-----------------------------
Highest-leverage next action
```

Below that line are implementation details. Above it is reasoning.

The relay should not contain a runtime that performs relays. The relay is the runtime:

```text
Observe
Reason
Challenge
Act
Observe
```

Plans, roadmaps, epics, probes, unknown graphs, certifications, and reports should become projections that explain or summarize why the next action was chosen. They should not be the mechanism that chooses the action.

**Must Change**

- `LoopRelay.Plan.Cli` should be demoted or retired from the primary workflow. [PlanPipeline.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Plan.Cli/PlanPipeline.cs:7) explicitly runs `preflight -> write plan -> adversarial review -> revise plan -> collect details -> extract milestones -> extract details -> commit/push`. That is the classical planning model the migration is leaving.

- `.agents/plan.md` is active permanent context and should be quarantined as historical implementation evidence. It describes a "LoopRelay.Plan.CLI Implementation Plan" and a pipeline that authors plans, extracts checkbox milestones, and verifies them. That artifact should not be part of future relay context.

- The legacy execution loop treats milestone checkboxes as progress and completion. [LoopRunner.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Cli/LoopRunner.cs:28), [MilestoneGate.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Cli/MilestoneGate.cs:97), and [CommitGate.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Cli/CommitGate.cs:8) make epic completion and "progress" depend on checked milestone items or code commits. That must move to observed-reality evaluation.

- Execution prompts still tell agents to follow the plan and tick milestone checkboxes. See [StartExecution.prompt](C:/kernritsu/LoopRelay/src/LoopRelay.Core/Prompts/StartExecution.prompt:1), [ContinueExecution.prompt](C:/kernritsu/LoopRelay/src/LoopRelay.Core/Prompts/ContinueExecution.prompt:1), and [GenerateNoChangesHandoff.prompt](C:/kernritsu/LoopRelay/src/LoopRelay.Core/Prompts/GenerateNoChangesHandoff.prompt:2). These should become relay prompts: observe reality, reason from evidence, challenge assumptions, act through bounded probes or capabilities, and update observed reality.

- `Roadmap.Cli` is closer to the target, but still names and routes the world through roadmaps, epics, milestone specs, and completion certification. [RoadmapState.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapState.cs:5), [PromptContractRegistry.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/PromptContractRegistry.cs:11), and [RoadmapArtifactPaths.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapArtifactPaths.cs:5) need a relay vocabulary.

- Completion certification should be redesigned. [CompletionCertificationRouter.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/CompletionCertificationRouter.cs:5) routes `Close Epic`, `Continue Epic`, `Reopen Epic`, and `Gather More Evidence`. The useful part is evidence-based routing; the problematic part is treating epic closure as the central unit.

- The relay should not assume the next step is always the highest-value uncertainty. Uncertainty reduction is one optimization dimension, not the governing primitive. The runtime should choose the highest-leverage next action given current reality, constraints, human direction, and constitutional boundaries. That action might reduce uncertainty, increase capability, simplify architecture, remove pollution, falsify an expensive assumption, or produce the first executable observation.

- Audit and purge workflows should distinguish mutation, propagation, and artifacts. The system should optimize for finding the mutation that introduced divergence or pollution; artifacts are downstream cleanup targets.

**Keep And Build On**

- The repo already has strong evidence-boundary work. [roadmap-execution-interpretation.md](C:/kernritsu/LoopRelay/docs/roadmap-execution-interpretation.md:3) separates execution transport from certification, and [RoadmapExecutionOutcomeInterpreter.cs](C:/kernritsu/LoopRelay/src/LoopRelay.Roadmap.Cli/RoadmapExecutionOutcomeInterpreter.cs:130) writes durable execution evidence.

- Derived artifact provenance is valuable. [roadmap-execution-preparation-provenance.md](C:/kernritsu/LoopRelay/docs/roadmap-execution-preparation-provenance.md:5) already defines artifact dependency and freshness expectations. That should become the basis for observed-reality freshness and propagation tracing.

- Structured persistence is the right direction. [roadmap-structured-persistence.md](C:/kernritsu/LoopRelay/docs/roadmap-structured-persistence.md:20) says JSON should become canonical and Markdown rendered/projection-only. The relay runtime should follow that rule.

- Existing roadmap prompts contain useful anti-roadmap language. [SelectNextEpic.prompt](C:/kernritsu/LoopRelay/src/LoopRelay.Core/Prompts/Planning/SelectNextEpic.prompt:25) says roadmap entries are strategic hypotheses, not the complete search space, and later supports strategic investigation when uncertainty is too high. That concept should be promoted above "select next epic."

**Runtime Shape**

The runtime should avoid becoming a collection of equally important data models. `NorthStarRuntime`, `ObservedReality`, `EpistemicRuntime`, `DirectionalAuthority`, mutation tracing, and propagation tracing are useful only insofar as they help the relay select, challenge, and interpret the next action.

The primary runtime contract should be:

```text
RelayInput -> HighestLeverageNextAction -> ObservedRealityUpdate
```

Where `RelayInput` is the current observed reality, north star, constraints, unknowns, human direction, architectural constitution, and assumption exposure.

The next-action selector should score candidate actions by:

- expected value to the north star;
- risk and reversibility;
- evidence cost and time to observe;
- capability gain;
- simplification or pollution-removal potential;
- human-direction relevance;
- assumption exposure;
- cost if the exposed assumption is false;
- time to falsify the exposed assumption.

The last three are not the same as uncertainty. The relay should continually ask:

> What assumption am I currently betting the most engineering effort on?

That is the assumption most likely to waste months if it is wrong. A high-leverage next action is often the cheapest falsification path for that assumption, even when other work looks more productive.

**Directional Authority**

`DirectionalAuthority` should be first-class, not because it approves work, but because it prevents authority transfer from the human to the AI.

The subsystem should actively surface:

- assumptions the AI is relying on;
- assumptions that have never been experimentally challenged;
- assumptions accumulating descendants;
- assumptions consuming increasing engineering effort;
- confidence shifts caused by new evidence;
- decisions requiring explicit human direction;
- points where the AI is drifting beyond delegated authority.

This protects the human from the "maybe the AI knows something I don't" trap. The relay should make its bets inspectable before those bets become sunk-cost direction.

**Architectural Constitution Runtime**

LoopRelay needs a continuous constitutional evaluator, not just one-time architecture audits.

An `ArchitecturalConstitutionRuntime` should continuously evaluate:

- responsibility boundaries;
- permanent context admission;
- semantic ceremony;
- planning pollution;
- authority inflation;
- hypothesis becoming constraint;
- narrative becoming evidence;
- stale projections masquerading as facts;
- governance language that hides who has authority.

This is not another product layer above the relay. It is the relay's constitutional court. It challenges candidate actions and persisted context before they harden into direction.

**Supporting Capabilities**

- Observed reality should replace a passive current-evidence ledger. Evidence supports claims; observed reality is the grounding substrate future reasoning must account for. It should track claims, source files, commands run, test results, execution evidence, confidence, stale/unknown markers, timestamps, and explicit contradictions.

- Unknowns should become active reasoning inputs, not a passive unknown-dependency graph. Each unknown should track why it matters, what evidence would resolve it, estimated payoff, alternative probes, dependencies, confidence, and whether it blocks the north star.

- Executable probe and capability contracts should define command/action, expected observations, safety/approval requirements, evidence output, interpretation rules, and how the result updates observed reality.

- Mutation and propagation tracing should record `Mutation -> Propagation -> Artifacts` so cleanup work does not confuse symptoms with causes.

- Planning should become a projection. A plan can explain why the next action was selected, communicate likely follow-on work, or summarize a route through known constraints. It should not be the chooser.

**Relay State Machine**

The replacement state machine should be named around the loop, not around planning:

```text
ObserveReality
ReasonFromNorthStarAndConstraints
ExposeAssumptionBets
ChallengeWithDirectionalAuthority
ChallengeWithArchitecturalConstitution
SelectHighestLeverageNextAction
DesignProbeCapabilityOrCleanup
Act
InterpretObservation
UpdateObservedReality
PrepareNextRelay
```

The sequence is intentionally adversarial. The relay should not merely select useful work; it should challenge why that work is being treated as useful.

**Remove, Demote, Or Quarantine**

- Demote `LoopRelay.Plan.Cli` to legacy compatibility or migration tooling.
- Quarantine `.agents/plan.md` and root planning documents as historical evidence, not operating context.
- Remove milestone checkbox completion as a success signal.
- Redesign "certification," "readiness," "freeze," and "governance" language into observed reality, confidence, constraints, constitutional checks, and human decision records.
- Stop treating epic closure as the central loop objective; it can remain a reporting projection when needed.
- Quarantine stale architecture docs that reference absent projects like Backend/UI/Shell until revalidated against the current solution.
- Resolve or remove unused projection prompts noted in [prune-candidates.md](C:/kernritsu/LoopRelay/prune-candidates.md:27), especially `ProjectionForAdversarialPlanReview` and `ProjectionForDecisionSession`.

**Migration Path**

1. Define the relay's foundation around continuous epistemic convergence and the highest-leverage next action.
2. Get one executable north-star evaluation running against the current repository.
3. Feed that evaluation from existing execution evidence and provenance machinery before designing a complete new framework.
4. Add the minimal canonical JSON needed for observed reality, assumption exposure, executable probes, directional authority, and architectural constitution checks.
5. Replace milestone selection with highest-leverage next-action selection.
6. Replace epic completion certification with interpretation of observed reality plus directional-authority and constitutional review.
7. Add mutation and propagation tracing so purge/cleanup work finds root causes before cleaning artifacts.
8. Freeze the old plan/roadmap/epic flow as legacy compatibility once the executable relay path exists.
9. Keep Markdown as rendered summaries only; make JSON the source of truth.
10. Redirect future CLI entry points from `plan` and `roadmap` toward `relay`.

The important ordering is intentional: do not start by designing the whole new schema universe. Start by making one relay cycle executable, then let the runtime boundaries harden around actual observations.

I did not run tests; this was a static repository audit grounded in the current files.
