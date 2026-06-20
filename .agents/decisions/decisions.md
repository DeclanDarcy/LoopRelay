# Decisions

## Newly Authorized Decisions

- M7 Understanding Workspace is complete; its purpose was observability of existing continuity, not a richer continuity workflow.
- Current Understanding and Execution Participation must remain independently visible because understanding existence and understanding influence on execution are separate observable states.
- The read-only workspace boundary remains authoritative: operational context flows through backend projection into UI, and the UI must not become an operational-context authority.
- The expanded understanding surface should include architecture, authority boundaries, constraints, and rationale because those are high-value orientation and preservation-failure signals.
- Operational-context last-updated visibility is required continuity context because current understanding is temporal and should show how recently it was validated.
- M8 should be proof-focused rather than capability-focused; the primary remaining risk is continuity drift over repeated cycles.
- M8 certification should evaluate long sequences of generate, review, promote, reload, repeated multiple times, rather than only isolated operation success.
- Fresh participant certification should prove that plan, selected milestone, and current operational context are sufficient to recover architecture, constraints, stable decisions, open questions, and active risks without historical handoffs, proposal archives, or decision archives.
- Restart and recovery certification should cover promotion, review, and pending proposal states across process restart because continuity must not depend on process lifetime.
- Archive independence should be explicit in M8: historical operational-context archives may exist, but reconstruction/orientation must not require consulting them.

## Recommended Next Slice

- Start M8 by formalizing the fresh-participant certification statement and implementing backend fixtures/tests for repeated continuity cycles, restart recovery, and archive-independent orientation from plan, milestone, and current operational context.
