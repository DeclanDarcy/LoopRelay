# Decisions

## Newly Authorized Decisions

- M3 must preserve the distinction between review and lifecycle: edit, accept, and reject proposal review state without promoting or mutating `.agents/operational_context.md`.
- M3 review state should use explicit vocabulary for `Pending`, `Edited`, `Accepted`, `Rejected`, and `Superseded` so stale proposal handling remains unambiguous.
- Proposal identity must be stable enough to support generate, review, edit, accept, reject, stale detection, and later promotion without deriving authority indirectly from filesystem state.
- Add a reproducibility certification expectation: same proposal inputs should produce equivalent generated understanding output, apart from proposal identity and timestamps.
- Unknown operational-context Markdown preservation remains a high-risk invariant and must be tested through current context, proposal generation, and proposal persistence.

## Next-Slice Constraints

- Do not merge M3 review with M4 lifecycle.
- Do not make acceptance update authoritative operational context.
- Do not introduce lifecycle automation while implementing review state.
