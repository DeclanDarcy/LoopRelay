# Decisions

## Newly Authorized

- Treat `GOV-002` history preservation as required certification evidence, not optional auditability.
- Require generation certification to preserve concrete authority lineage:
  - decision history
  - proposal snapshot history
  - package authority lineage
  - revision lineage
- Treat missing generated-decision history as a governance certification failure.
- Keep certification focused on resolved authority snapshots rather than only current repository state.
- Continue keeping recommendation divergence separate from governance certification:
  - recommendation divergence remains a quality signal
  - recommendation divergence is not an automatic certification failure
- Proceed through remaining M10 scenario fixtures in this order:
  - architectural fork
  - workflow priority decision
  - contradiction with withheld recommendation
  - refinement after changed assumptions
  - end-to-end repository lifecycle
- Use the end-to-end repository lifecycle fixture as the canonical certification proof before evaluating whether M10 can be formally closed.

## Not Authorized

- Do not treat history preservation as a non-blocking quality improvement.
- Do not make recommendation divergence an automatic certification failure.
