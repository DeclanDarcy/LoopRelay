# Handoff: Phase 0 Prompt Architecture Course Correction

Current milestone state: Phase 0 Runtime Foundation remains active. The active plan and all active milestone files were revised to correct a significant drift from the intended prompt architecture.

## New State Introduced

- `.agents/plan.md` now treats `CommandCenter.Core.Prompts` as the canonical prompt API.
- `.agents/milestones/m0-runtime-foundation.md` now makes generated prompt infrastructure foundational rather than incidental.
- `.agents/milestones/m1` through `m12` now carry prompt selection, prompt provenance, prompt drift, prompt certification, and no-literal-prompt constraints through the full roadmap.
- `src/CommandCenter.Core/Prompts/*.prompt` is the authored prompt source of truth.
- `Lib.Prompts` is the intended generator. It consumes `.prompt` files as build-time `AdditionalFiles` and emits static prompt classes with `Template`, `SourceHash`, and `Render(...)`.
- Prompt generation is analyzer-only infrastructure. It must not become a runtime parser, embedded-resource lookup, runtime service, or text-reconstruction mechanism.
- Runtime layers receive rendered prompt text and prompt provenance. They must not choose templates, edit prompt text, infer prompt semantics, or rebuild prompts from literals.

## Canonical Prompt Catalog

Planning:

- `WritePlanAgainstCodebase`
- `WritePlanForNewCodebase`
- `RevisePlan`
- `ExtractMilestones`

Operational execution:

- `StartExecution`
- `ContinueExecution`

Decision sessions:

- `StartDecisionSession`
- `StartDecisionSessionFromTransfer`
- `GetNextDecisions`

Continuity:

- `ProduceOperationalDelta`
- `UpdateOperationalContext`

## Corrected Architecture

- `CommandCenter.Core.Prompts` owns canonical prompt text through generated code.
- Domain services own typed source information and prompt input shaping.
- Planning, Execution, DecisionSessions, and Continuity should expose selection/rendering adapters over generated prompt classes.
- Execution owns operational semantics, Git, handoffs, execution evidence, and operational prompt input shaping. It does not own canonical prompt text.
- Agent Runtime is prompt-neutral. It carries rendered prompt text plus provenance only.
- Repository Runtime coordinates commands and may route to prompt-owning services, but it does not construct prompt text.
- Prompt templates can instruct agents, but they do not become semantic authority for plans, executions, decisions, continuity, reasoning, workflow, Git, contracts, or UI behavior.

## Required Prompt Provenance

Every generated-prompt turn must be able to record:

- prompt name
- generated prompt type
- `SourceHash`
- session role
- workflow phase
- input artifact identities
- output artifact identities

Historical prompt provenance must remain readable even when prompt source hashes change later.

## Superseded Direction

The prior active handoff and decisions understated the prompt architecture by describing generic prompt builders and "generated prompt documents." That framing is incomplete and should not guide implementation.

Do not introduce a new prompt-builder abstraction that owns or recreates canonical prompt text. Any builder/adapter abstraction must delegate to generated `CommandCenter.Core.Prompts` renderers and exist only to select prompts, format authority-owned inputs, and capture provenance.

Do not force decision runtime output into an ad hoc JSON contract unless a new canonical `.prompt` file is authored, generated, and certified for that behavior.

The old "stage, commit, push, stop" sequence in `.agents/decisions/decisions.md` is obsolete.

## Existing Still-Valid Context

The previous Phase 0 agent event primitive slice remains accepted:

- `AgentProcessEvent`, `AgentProcessEventKind`, `AgentProcessOutputStream`, and `AgentProcessEventStream` were added under `CommandCenter.Agents`.
- The event layer is observational only.
- `AgentProcessStateMachine` remains lifecycle authority.
- Process-local in-memory event streams are the current scope.
- Durable replay, stream contracts, repository timelines, and UI consumers remain future work.

## Current Working Tree Notes

- The user-added `src/CommandCenter.Core/Prompts/` files and `src/CommandCenter.Core/CommandCenter.Core.csproj` changes are intentional and must not be reverted.
- The active planning docs have been revised to align with those prompt files.
- `dotnet build src\CommandCenter.Core\CommandCenter.Core.csproj` passed after the prompt files and generator wiring were present.
- `git diff --check -- .agents\plan.md .agents\milestones` passed with only CRLF normalization warnings.

## Next Suggested Slice

Continue Phase 0 by implementing the generated prompt infrastructure as documented:

1. Build against `Lib.Prompts` generated classes under `CommandCenter.Core.Prompts`.
2. Add prompt selection/rendering adapters for Planning, Execution, DecisionSessions, and Continuity.
3. Replace existing literal prompt composition with generated renderers.
4. Add prompt provenance models before persistent Agent Runtime depends on prompt turns.
5. Add governance tests preventing canonical prompt text outside authored `.prompt` files and generated prompt output.
6. Keep existing public behavior unchanged while compatibility paths are migrated.
