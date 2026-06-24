# Decisions

## Newly Authorized

- Treat Milestone 4A projection/history models and service as complete.
- Begin the next slice with Milestone 4 projection/history backend endpoints before influence work.
- Implement the endpoint work in this order:
  1. Projection endpoints.
  2. History endpoints.
  3. Influence trace.
  4. Health.
- Projection and history endpoints must remain simple read-only composition from existing lifecycle evidence.
- Do not add projection/history persistence, background generation, or observability-owned artifacts for the endpoint slice.
- Preserve the authority direction: lifecycle core feeds observability; observability must not feed lifecycle core.
- Preserve history as a projection reconstructed from durable evidence, not as a second source of truth.
- Corrupt derived snapshots should surface as diagnostics and must not block registry visibility.
- Influence traces are explainability only: they explain decisions and must not create decisions.
- Influence traces must not become policy inputs or transfer inputs.
- Influence traces should reuse existing decision evidence and policy contributing factors rather than inventing a parallel explanation model.
- Include existing contributing factors such as reuse score, transfer score, transfer pressure, cache risk, continuity benefit, and coherence score when implementing influence traces.
