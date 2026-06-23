# Decisions

## Newly Authorized

- Treat the M10 validation slice as strong and correct.
- Preserve the `GEN-001` certification requirement that candidates must have a real `Discovered` lifecycle history event, so manually seeded candidates cannot prove automated generation.
- Treat the full-chain pass fixture as the canonical M10 workflow proof:
  - discovery
  - promotion
  - generation
  - resolution
  - quality
  - projection
  - influence
  - certification
- Keep execution projection absence and influence trace absence as distinct certification failures:
  - `CON-001`: execution projection absent
  - `CON-002`: influence trace absent
- Treat repeated ignored recommendations as quality signals or warnings, not automatic certification failures, unless later evidence shows generation is structurally ineffective.
- Continue M10 by finishing scenario and report coverage:
  - repeated ignored recommendation signal
  - architectural fork
  - workflow priority
  - withheld recommendation
  - refinement
  - end-to-end lifecycle

## Not Authorized

- Do not treat manually seeded candidate state as proof of automated discovery.
- Do not automatically fail certification solely because recommendations are repeatedly ignored without stronger evidence of structural generation ineffectiveness.
