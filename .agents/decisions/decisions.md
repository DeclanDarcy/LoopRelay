# Decisions

## Newly Authorized

- Treat the M10 Tauri/UI generation-certification exposure slice as correct.
- Preserve the boundary that generation certification is visible and end-to-end, but remains observational and non-authoritative.
- Keep generation certification separate from lifecycle certification:
  - lifecycle certification answers whether the decision lifecycle is structurally valid
  - generation certification answers whether automated generation produced useful, governed, execution-influencing decisions
- Keep the generation certification UI advisory-only. It may present findings, failures, evidence, human authoring burden, and quality evidence, but it must not approve decisions, resolve decisions, or mutate lifecycle authority.
- Prioritize false-positive prevention before closing M10.
- Implement remaining negative certification fixtures in this order:
  1. order-based or hardcoded recommendation failure
  2. missing options
  3. missing quality evidence
  4. full rewrite dominance
  5. generation bypass dominance
  6. governance resolution bypass
  7. missing execution influence
- Give highest attention to the order-based recommendation fixture because it guards against regression to the original `options[0]` failure mode.

## Not Authorized

- Do not treat generation certification as governance.
- Do not let certification approve, reject, defer, supersede, archive, or otherwise resolve decisions.
- Do not close Milestone 10 before negative fixtures reduce false-positive certification risk.
