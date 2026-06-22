# Decisions

## Newly Authorized

- M0 is accepted as complete and certified ready based on the completed M0A-M0D foundation work.
- M1 may begin; no remaining M0 foundation work should block decision context resolution.
- The M0 authority model remains binding: structured JSON is canonical, markdown is generated projection, UI is presentation state only, execution has no decision authority, operational context assimilation remains separate, and human approval controls resolution.
- Decision projection recovery is accepted because it regenerates missing markdown from structured JSON without reconstructing authority from markdown.
- Existing generated markdown should not be overwritten during recovery reads; missing-projection regeneration should remain restorative and low-churn.
- M1 implementation order is: `DecisionContext`, `DecisionContextSnapshot`, validation model, diagnostics model, source attribution model, fingerprinting, context persistence, context endpoints, and deterministic snapshot tests.
- Fingerprints are first-class M1 work, not a later enhancement; establish `DecisionContextFingerprint` from the start because later discovery, proposal generation, refinement, resolution, governance, execution projection, and certification depend on it.
