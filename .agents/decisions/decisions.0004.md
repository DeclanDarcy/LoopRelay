# Decisions

## Newly Authorized

- Accept the first Milestone 4 structured tradeoff analysis slice as the correct initial M4 direction.
- Keep Milestone 4 open.
- Continue preserving legacy `DecisionTradeoff` compatibility by deriving legacy tradeoff projection from structured analysis during migration.
- Preserve the sequencing boundary:
  - M4 describes tradeoffs.
  - M5 decides recommendations.
- Keep benefits, costs, risks, dependencies, consequences, unknowns, and disqualifiers inside M4.
- Keep preferred option, preferred-plus-alternative, and no-recommendation modes inside M5.
- Preserve structured tradeoff analysis through human resolution snapshots because it is governance evidence.
- Treat the current analysis layer as an acceptable first structural slice if it is primarily derived from option metadata.
- Do not attempt to finish context integration and tradeoff generation in one jump.
- Next M4 slice should add or reuse a `DecisionGenerationContext` projection boundary before enriching tradeoff generation.
- `TradeoffAnalysisService` should consume `DecisionGenerationContext`, not read repository, milestone, handoff, operational-context, decision, or repository-state files directly.
- The context service should remain the sole owner of extracting goals, constraints, risks, questions, decision history, repository state, and handoff state.
- Continue M4 by making analysis context-aware against:
  - goals
  - constraints
  - risks
  - prior decisions
  - repository state
  - dependencies
- Model absence of information explicitly as unknown risk, unknown dependency, or unknown consequence where applicable.
- Surface constraint-violating options as tradeoff analysis output, not recommendation output.
- Improve comparison quality so comparisons identify concrete differences between options rather than generic stronger/weaker boilerplate.

## Not Authorized

- Do not start M5 recommendation generation yet.
- Do not add projections, dashboards, package infrastructure, certification machinery, or recommendation logic before M4 analysis becomes context-aware.
- Do not create a second context-construction system inside tradeoff analysis.
