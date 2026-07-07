# CLI Boundary Insights

## Roadmap, Plan, and Main CLI separation

- The three CLIs should remain disconnected for now. The system is not mature enough to connect `Roadmap -> Plan -> Main` as one automated chain.
- Roadmap CLI should stop at roadmap-owned planning artifacts, currently active epic selection/promotion and milestone specs.
- Plan CLI should own `AdversarialPlanReview` and its projection usage.
- Main CLI should own `DecisionSession` and its projection usage.

## Main CLI ownership confirmed

- Execution remains part of the main CLI:
  - `src/LoopRelay.Cli/LoopRunner.cs` calls `execution.RunAsync(...)` inside the main loop.
  - `src/LoopRelay.Cli/ExecutionStep.cs` owns `StartExecution`, `ContinueExecution`, `GenerateHandoff`, and `GenerateNoChangesHandoff`.
  - `src/LoopRelay.Cli/AgentSpecs.cs` gives the execution session `danger-full-access`, preserving the main execution posture.

- Operational context generation/update remains part of the main CLI:
  - `src/LoopRelay.Cli/LoopArtifacts.cs` has `EnsureOperationalContextAsync()`, which seeds `.agents/operational_context.md` from `.agents/plan.md` when missing.
  - `src/LoopRelay.Cli/DecisionSession.cs` reads and injects operational context into decision proposals.
  - `src/LoopRelay.Cli/DecisionSession.cs` handles transfer-time operational context evolution through `UpdateOperationalContext.Text` in `EvolveOperationalContextAsync(...)`, then writes the evolved `.agents/operational_context.md` back to the repo.

## Practical implication

- Roadmap CLI must not generate operational context, execution prompts, compatibility execution artifacts, or execution turns.
- Roadmap CLI may record or validate milestone-spec provenance, but execution preparation beyond specs belongs outside Roadmap CLI.
