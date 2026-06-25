# Decisions

## Newly Authorized

- Accept the Milestone 4 rejected/deduplicated option payload implementation as architecturally correct.
- Treat rejected and deduplicated generated options as durable backend-owned transparency artifacts, not transient generation metadata or counts only.
- Keep `DecisionGenerationDiagnostics` as the canonical record of option generation behavior, including rejected and deduplicated payloads.
- Preserve rejected/deduplicated option diagnostics through proposal persistence, package-version persistence, package regeneration, serialization, and markdown projection.
- Keep markdown projections as consumers of authoritative diagnostics rather than separate explanation paths.
- Continue the next Milestone 4 backend slice in this order:
  - complete remaining `DecisionProposal` transparency fields, especially recommendation evidence, option evaluations, supporting factors, concerns, assumptions, alternative explanations, analyzed options, tradeoff comparisons, and tradeoff diagnostics
  - then expose influence and governance reasoning for included, excluded, superseded, conflicting, and ignored decisions with governing reasons and execution influence diagnostics
- Defer significant UI composition until the backend transparency model is coherent and complete.
