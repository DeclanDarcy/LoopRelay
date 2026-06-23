# Decisions

## Newly Authorized

- Treat the M10 false-positive hardening slice as correct, especially the `GEN-006` safeguard against order-based or hardcoded recommendations.
- Continue M10 with an explicit positive pass fixture proving the full certification path:
  - discovery
  - options
  - tradeoffs
  - recommendation
  - human resolution
  - execution projection
  - influence trace
  - certification pass
- Add an execution-projection-absent failure fixture that is distinct from the missing influence trace failure.
- Treat repeated ignored recommendations initially as a quality warning or quality signal, not an automatic generation-certification failure.
- Use a high recommendation-divergence signal to surface recommendation concerns unless later evidence shows recommendations are consistently wrong enough to justify hard certification failure.

## Not Authorized

- Do not make repeated ignored recommendations an automatic certification failure yet.
- Do not close M10 until the explicit pass fixture and execution-projection-absent failure fixture are implemented.
