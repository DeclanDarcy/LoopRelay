# Decisions

## Newly Authorized

- Accept the completed Milestone 5 prompt-manifest vertical slice as consistent with the roadmap and operational-context architecture.
- Treat the prompt-manifest pipeline as complete end-to-end across backend authority, Tauri transport, TypeScript contracts, client API, React hook, execution session UI, characterization coverage, and development mock support.
- Preserve prompt manifests as an opt-in resource rather than expanding `ExecutionSessionSummary`.
- Preserve requested vs delivered context as an explicit distinction instead of implying provider fidelity.
- Preserve `NoProviderDivergenceSignal` as a first-class semantic state that means no provider divergence signal exists, not that exact delivery was confirmed.
- Continue enforcing `Authority -> Projection -> Transport -> Typed Contract -> Render-only UI`.
- Prioritize remaining Milestone 5 work in this order:
  1. Recovery and monitoring transparency.
  2. Push retry transparency.
  3. Git eligibility projection.
  4. Structured governed conflicts.
  5. Handoff transparency.
  6. Semantic execution event grouping.
- Stop milestone execution after rotating decisions, staging, committing, and pushing this slice.
