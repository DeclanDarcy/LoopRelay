# Decisions

## Newly Authorized

- Formally close Milestone 3 as complete.
- Begin Milestone 4 structured tradeoff analysis next.
- Keep `IOptionValidationService` as the ownership boundary for filtering generated options before tradeoff analysis.
- M4 tradeoff analysis must consume validated options, not raw generated options.
- Keep proposal/API compatibility intact during M4 unless additive fields are explicitly required.
- M4 should be additive and may introduce structured analysis concepts such as:
  - `AnalyzedDecisionOption`
  - `DecisionBenefit`
  - `DecisionCost`
  - `DecisionRisk`
  - `DecisionDependency`
  - `DecisionConsequence`
- Do not break existing `DecisionProposal` or `DecisionTradeoff` while the structured analysis path is stabilizing.
- Continue deferring a standalone `diagnostics.json` artifact; proposal-level diagnostics remain sufficient for Tier 0.
- M4 tradeoffs must be candidate-aware and option-aware rather than generic.
- M4 should represent missing evidence as explicit unknown risks or consequences instead of silently omitting risk.
- M4 must not choose winners or leak recommendation logic into tradeoff analysis; M4 describes tradeoffs, M5 recommends.

## Not Authorized

- Do not add standalone diagnostics artifacts during M4 unless Tier 0 validation proves proposal-level diagnostics insufficient.
- Do not introduce package infrastructure, quality systems, certification machinery, or governance complexity as part of closing M3.
- Do not let M4 structured tradeoff analysis resolve, rank, or recommend decisions.
