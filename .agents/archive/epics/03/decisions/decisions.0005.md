# Decisions

## Newly Authorized Decisions

- M5 must focus on compression analysis before compression action.
- M5 should answer what is bloated, historical, redundant, unsafe to remove, and required to preserve.
- M5 must not implement model-assisted rewriting or narrative summarization.
- Compression must preserve the operational model rather than create a smaller narrative.
- No stable decision, active constraint, open question, or active risk may disappear without an explicit warning.
- Preservation warnings should be surfaced in proposal review.
- M5 must not mutate authoritative `.agents/operational_context.md`.

## Recommended M5 Test Shape

- Detect long historical narrative.
- Preserve stable decisions.
- Preserve open questions.
- Preserve active risks.
- Preserve unknown sections.
- Warn on redundant repeated decisions.
- Show compression warning in proposal review.
- Verify no authoritative context mutation.
