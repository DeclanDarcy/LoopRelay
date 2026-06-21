# Decisions

## Newly Authorized

- Treat M0.6 as frontend authority certification, not merely UI regression coverage.
- Treat the removed automatic `prepare_commit` on repository selection as a genuine authority leak and architectural correction.
- Preserve the boundary that repository selection is navigation authority only and must not initiate workflow preparation.
- Use the emerging authority matrix in future audits:
  - Navigation authority must never invoke workflow actions.
  - Draft authority must never trigger workflow execution.
  - Workflow authority must require explicit workflow actions.
  - Projection authority may retrieve projections but must not initiate workflow.
- Continue prioritizing workflow authority characterization over presentation extraction while M0.6 is finding real boundary violations.
- Make operational-context proposal workflow gating the next highest-leverage M0.6 target.

## Next Characterization Target

Characterize operational-context proposal workflow authority:

- Generate must not be triggered by repository selection, proposal selection, tab changes, or navigation changes.
- Loading or viewing proposal metadata/content must not generate a proposal.
- Editing proposal content must not implicitly accept or promote a proposal.
- Accept and reject must only run through explicit actions.
- Promotion is highest risk and must only run through explicit promotion; proposal selection, viewing, and editing must not invoke promotion.
