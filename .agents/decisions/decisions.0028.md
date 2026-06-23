# Decisions

## Newly Authorized

- Treat M8 backend as complete and proceed to the M8 UI slice.
- Preserve the certification authority boundary: certification reports are evidence artifacts only.
- Certification reports must not become repository authority, reasoning authority, narrative authority, graph authority, or reconstruction source data.
- The UI may display certification outcome, evidence, failures, diagnostics, and report history.
- The UI must remain a presentation layer over certification artifacts and must not introduce a second reasoning authority.
- The source of truth for reasoning reconstruction remains:

```text
Repository
  -> Graph
  -> Trace
  -> Reconstruction
```

- Certification continues to validate answerability using the same reconstruction path, not a specialized certification model or specialized reasoning engine.
- The M8 UI slice should continue with `ReasoningCertificationPanel`, bridge/API integration, and characterization tests without additional backend architecture unless implementation reveals a concrete gap.
