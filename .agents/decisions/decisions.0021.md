# Decisions

## Newly Authorized

- Accept the Milestone 4 quality/burden transparency implementation as the correct backend-owned pattern.
- Keep `DecisionQualityExplanation` and `HumanAuthoringBurdenExplanation` as authoritative sources for score contribution, threshold and override explanation, burden selection rule, winning signal, and unknown/default status.
- Treat explanation durability across assessment/report, JSON persistence, endpoint response, and markdown projection as required for Milestone 4 transparency work.
- Keep React presentation-only for these transparency fields once they are surfaced through typed API clients.
- Continue the next Milestone 4 backend slice in this order:
  - preserve rejected and deduplicated option payloads, not just counts
  - expose those payloads through proposal serialization and projection
  - add tests proving rejected and deduplicated options survive generation and reload
  - then expose influence and execution diagnostics through decision-owned API/type surfaces
- Defer UI composition until the backend projection gaps are closed.
