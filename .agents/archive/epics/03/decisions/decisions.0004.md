# Decisions

## Newly Authorized Decisions

- M4 must preserve the M3 architectural boundary: review acceptance is not promotion, and accepted proposals remain non-authoritative until an explicit promotion operation succeeds.
- Only accepted operational-context proposals may be promoted into `.agents/operational_context.md`.
- M4 promotion must behave atomically from the user's perspective: validation, archive, promote, and finalize must not leave the repository in a partially promoted or partially archived state.
- Promotion must preserve review provenance sufficient to answer which proposal produced the promoted understanding, whether `edited.md` or `proposed.md` was promoted, and when the proposal was accepted.
- M4 should keep accepted and promoted states distinct so lifecycle state remains understandable across proposal review, promotion, certification, and later audit work.

## Next-Slice Constraints

- Do not let acceptance mutate current operational context.
- Do not promote superseded, rejected, pending, edited-but-unaccepted, or stale proposals.
- Do not overwrite current operational context without preserving the prior current context as a numbered historical artifact.
