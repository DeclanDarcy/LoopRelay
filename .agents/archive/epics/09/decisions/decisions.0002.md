# Decisions

## Newly Authorized

- Treat Decision Session Lifecycle Stage 1 as effectively complete; no remaining Stage 1 architectural risk was identified in the review.
- Split the Decision Session foundation test suite before beginning Stage 2 so future certification failures map cleanly to domain, repository, registry, endpoint, analysis, policy, eligibility, transfer, and recovery concerns.
- Begin Stage 2A after the test-suite split, starting with metrics, statistics, TTL, cache-miss-risk snapshots, and diagnostics.
- Preserve the Stage 2 analysis boundary: analysis services answer what is true, not what should be done.
- Keep metrics/statistics/cache, economics, coherence, and policy as separate services with independently rebuildable snapshots and independently certifiable behavior.
- Do not adjust the roadmap at this point; implementation remains aligned with the intended architecture.
