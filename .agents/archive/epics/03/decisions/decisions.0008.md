# Decisions

## Newly Authorized Decisions

- The first half of M6 is accepted as correctly aligned because it implements deterministic decision analysis and durable consequence assimilation without turning operational context into a second decision archive.
- M6 should continue preserving the boundary between decision history and current understanding: decision artifacts remain the record, while operational context receives only durable consequences.
- Tactical decisions should continue to surface as reviewer warnings rather than being silently discarded or promoted into operational context.
- Deterministic, auditable, reviewable classification remains preferred over early decision-intelligence or automatic semantic authority.
- Rationale preservation is a core M6 value because durable rationale often survives architecture evolution better than literal decision wording.
- Contradictory durable decisions must remain warnings only; the system must identify apparent conflicts but never decide which decision wins.
- The next M6 slice should make stable decisions, open decisions, decision rationale, decision warnings, and decision contradictions first-class review concepts.
- Reviewers should be able to evaluate decision-derived understanding without opening `decisions.md` or inspecting raw proposal metadata.
- Before closing M6, add an explicit certification rule that decision assimilation must preserve separation between decision history and current understanding.
- Add repeated-revision certification coverage proving a large decision archive does not cause operational context to accumulate historical decisions over repeated proposal cycles.

## Recommended Next Slice

- Extend decision-specific semantic changes and the review/workspace UI so decision-derived understanding is visible as reviewable concepts.
- Add decision archive creep certification before marking M6 complete.
