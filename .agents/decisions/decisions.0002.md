# Decisions: 2026-06-27 Phase 0 Agents Boundary Acceptance

These decisions capture only newly authorized direction from the user response accepting the first Phase 0 Runtime Foundation slice.

## Authorized Decisions

1. Accept the first Phase 0 Runtime Foundation slice.
   - `CommandCenter.Agents` owning process-launch infrastructure is accepted as aligned with the roadmap.
   - Execution retaining operational semantics is accepted as the correct boundary.
   - Boundary tests preventing dependency inversion are accepted as required Phase 0 protection.

2. Keep `CodexExecutableResolver` unresolved until `SessionRole` exists.
   - Move it to Agents only if it is limited to executable discovery, filesystem lookup, and PATH probing.
   - Keep it outside Agents if it knows Codex CLI conventions, operational configuration, execution policies, provider defaults, or repository semantics.

3. Prevent `CommandCenter.Agents` from becoming `Execution v2`.
   - Agents may contain session, turn, process, stream, cancellation, sandbox, effort, and lifecycle primitives.
   - Agents must not become aware of Repository, Execution, Decisions, Workflow, Git, Planning, or Continuity semantics.

4. Introduce next runtime primitives as immutable information objects first.
   - `AgentSessionSpec` should describe role, sandbox, effort, and startup options without accumulating mutable runtime state.
   - `AgentProcessState` and `AgentTurnState` should be lifecycle values, not behavior-owning runtime objects.

5. Consider small internal structure in `CommandCenter.Agents` while the project is still small.
   - Candidate grouping may separate contracts, runtime, process, and models if it helps avoid Phase 1 mixing session registry, process supervision, stream plumbing, runtime orchestration, and lifecycle models in a flat project.

6. Use the next-slice sequence authorized by the acceptance response.
   - Introduce immutable runtime primitives.
   - Add architecture tests protecting those primitives.
   - Introduce `IAgentProcess` only after primitives are protected.
   - Build `IAgentRuntime` after process abstraction.
   - Migrate Execution through a compatibility adapter last.

## Evidence Targets

- `.agents/decisions/decisions.0001.md`
- `.agents/decisions/decisions.md`
- `.agents/milestones/m0.4-referential-governance-validation-slice-0054.md`
- `.agents/handoffs/handoff.md`
- `.agents/milestones/m0-runtime-foundation.md`
- `src/CommandCenter.Agents/`
- `tests/CommandCenter.Backend.Tests/Architecture/AgentRuntimeBoundaryTests.cs`

## Next Authorized Sequence

1. Stage the completed Phase 0 Agents boundary slice and this decision rotation.
2. Commit on `dev`.
3. Push to `origin/dev`.
4. Stop executing after the push.
