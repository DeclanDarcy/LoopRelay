# Decisions

## Newly Authorized

- The current M3 option-generation architecture is accepted as the right direction because `IOptionGenerationService` now returns a richer `DecisionOptionGenerationResult`.
- Persist generation diagnostics on the proposal for Tier 0 instead of creating a standalone `diagnostics.json` artifact now.
- Preserve option relationships and generation diagnostics through `DecisionResolvedProposalSnapshot`.
- Keep option validation deterministic so future provider-backed generation remains reproducible after validation.
- Complete one final M3 hardening slice before starting M4.
- The final M3 hardening slice must add explicit tests proving rejection of duplicate, non-actionable, and evidence-unrelated options, with corresponding diagnostics persisted.
- After those invalid-option validation tests exist, M3 can close and M4 structured tradeoff analysis can begin.

## Not Authorized

- Do not create a standalone proposal diagnostics artifact before Tier 0 validation requires it.
- Do not expand into package infrastructure, quality reporting, certification, or diagnostics-history work before M4.
- Do not begin M4 until duplicate, non-actionable, and evidence-unrelated option rejection is directly covered.
