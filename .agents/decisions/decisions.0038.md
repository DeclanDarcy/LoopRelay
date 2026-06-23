# Decisions

## Newly Authorized

- Treat repeated recommendation overrides as quality evidence, not automatic certification failure.
- Preserve the `QLT-002` shape:
  - repeated recommendation override
  - quality signal
  - certification finding
- Keep recommendation adoption distinct from workflow replacement success.
- Treat workflow replacement success as humans no longer authoring the decision content, not humans always agreeing with the generated recommendation.
- Consider a generated decision successful when the human selects a generated alternative with `ReviewOnly` or `MinorEdit` burden.
- Continue M10 by closing the `history preserved` certification requirement next.
- After history preservation, finish the remaining certification scenario fixtures:
  - architectural fork
  - workflow priority
  - withheld recommendation
  - refinement after changed assumptions
  - end-to-end repository lifecycle

## Not Authorized

- Do not make repeated recommendation overrides an automatic certification failure.
- Do not equate recommendation adoption with workflow success.
