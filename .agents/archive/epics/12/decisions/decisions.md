# Decisions: 2026-06-27 Prompt Architecture Course Correction

These decisions supersede the previous Phase 0 prompt infrastructure direction where it conflicts with the intended `Lib.Prompts` architecture.

## Evidence

- `.agents/plan.md`
- `.agents/milestones/m0-runtime-foundation.md`
- `.agents/milestones/m1-persistent-agent-runtime.md` through `.agents/milestones/m12-adaptive-engineering-intelligence.md`
- `.agents/specs/s0.md`
- `src/CommandCenter.Core/Prompts/*.prompt`
- `src/CommandCenter.Core/CommandCenter.Core.csproj`
- `C:\kernritsu\dotnet-libraries\Lib.Prompts\README.md`

## Authorized Decisions

1. Treat the prior prompt direction as materially incomplete.
   - The previous "common prompt generation abstraction before specialized role builders" direction is superseded where it implies runtime-owned prompt documents or hand-authored prompt text inside services.
   - Generic prompt builders must not become a second prompt source of truth.
   - Compatibility layers may remain temporarily, but they must delegate to generated prompt renderers.

2. Make authored `.prompt` files the canonical prompt source.
   - Prompt text lives in `src/CommandCenter.Core/Prompts/*.prompt`.
   - The initial catalog is `WritePlanAgainstCodebase`, `WritePlanForNewCodebase`, `RevisePlan`, `ExtractMilestones`, `StartExecution`, `ContinueExecution`, `StartDecisionSession`, `StartDecisionSessionFromTransfer`, `GetNextDecisions`, `ProduceOperationalDelta`, and `UpdateOperationalContext`.
   - Canonical prompt text must not be duplicated in Execution, DecisionSessions, Planning, Continuity, Workflow, Backend, UI, tests, runtime services, or compatibility adapters.

3. Use `Lib.Prompts` as compile-time prompt infrastructure.
   - `Lib.Prompts` is analyzer-only prompt generation.
   - It consumes `.prompt` files as MSBuild `AdditionalFiles`.
   - It emits static prompt classes under `CommandCenter.Core.Prompts`.
   - Generated classes expose `Template`, `SourceHash`, and `Render(...)`.
   - Malformed prompt placeholders are build failures through `PROMPT001`-`PROMPT004`.
   - Missing prompt discovery must remain visible through `PROMPT100` or equivalent certification.

4. Separate prompt text authority from prompt input authority.
   - `CommandCenter.Core.Prompts` owns canonical prompt text.
   - Domain services own typed source information and input shaping.
   - Planning selects and renders planning prompts from intent, specs, roadmap inputs, plans, and feedback.
   - Execution selects and renders operational prompts from plans, operational context, handoffs, decisions, and execution evidence.
   - DecisionSessions selects and renders decision prompts from operational context, transfers, handoffs, and session reports.
   - Continuity selects and renders delta/update prompts from current operational context and extracted durable session state.

5. Keep Agent Runtime prompt-neutral.
   - Agent Runtime receives rendered prompt text and provenance.
   - Agent Runtime must not select templates, compose canonical prompt text, inspect prompt semantics, repair prompt output, or gather domain inputs.
   - Repository Runtime may route commands to prompt-owning services but must not build prompt text.

6. Require prompt provenance as durable communication metadata.
   - Every generated-prompt turn records prompt name, generated type, `SourceHash`, session role, workflow phase, input artifact identities, and output artifact identities.
   - Runtime diagnostics, run journals, conversation history, Repository Knowledge lineage, and recovery projections should preserve prompt provenance where a prompt shaped an agent-produced artifact.
   - Historical artifacts remain readable when a later prompt source hash changes.

7. Treat prompt changes as architecture-affecting communication changes.
   - A prompt change requires generated prompt compilation, selection coverage, provenance impact review, and no-literal-prompt governance.
   - Prompt drift is an architectural drift class.
   - Prompt improvements may be recommended by intelligence services only as human-reviewed proposals.
   - No intelligence, runtime, UI, or compatibility layer may mutate prompt templates autonomously.

8. Preserve the authored decision prompt contract.
   - Decision runtime must use `StartDecisionSession`, `StartDecisionSessionFromTransfer`, and `GetNextDecisions` for canonical decision conversation turns.
   - Do not force decision runtime output into an ad hoc structured JSON contract unless a new canonical `.prompt` file is authored, generated, and certified for that behavior.
   - Decisions domain remains responsible for validation, lifecycle, evidence, fallback, governance, quality, and persistence.

9. Keep the previous event primitive decision intact.
   - The Phase 0 agent event primitive slice remains accepted.
   - Event primitives are observational only.
   - `AgentProcessStateMachine` remains lifecycle authority.
   - Durable replay, stream contracts, repository timelines, and UI consumers remain future work.

10. Replace the old next sequence.
    - The old "stage, commit, push, stop" sequence is no longer the active instruction.
    - The next implementation slice is generated prompt infrastructure and governance under Phase 0.
    - The user-added `src/CommandCenter.Core/Prompts/` files and `src/CommandCenter.Core/CommandCenter.Core.csproj` changes are intentional and must be preserved.

## Next Authorized Sequence

1. Implement or finish `CommandCenter.Core.Prompts` generation through `Lib.Prompts`.
2. Add prompt selection/rendering adapters that call generated `Render(...)` methods.
3. Replace literal prompt composition with generated prompt renderers.
4. Add prompt provenance models and tests before persistent runtime turn history depends on them.
5. Add architecture/governance tests for prompt authority, prompt selection, source hashes, and no duplicated canonical prompt text.
6. Preserve existing product behavior while this foundation is introduced.
