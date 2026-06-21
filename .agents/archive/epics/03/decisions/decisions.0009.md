# Decisions

## Newly Authorized Decisions

- M6 is accepted as effectively closed because decision continuity now preserves durable decision consequences and rationale without becoming a second decision archive or an automated governance engine.
- The combination of decision-specific semantic changes and decision archive creep certification is the strongest M6 boundary because it makes the operational-context versus decision-history separation testable.
- Decision review should reason over stable decision additions/removals, open decision additions/removals, rationale changes, and decision warnings rather than treating the result as generic Markdown change.
- Tactical classifier hardening is a high-leverage continuity guardrail because phrases such as `next slice should...` can otherwise turn current understanding into recent planning activity.
- Decision contradiction and missing-rationale handling must remain advisory warnings; Command Center should not infer resolutions or become a governance authority.
- M7 should primarily be a projection milestone focused on how humans observe existing understanding, not on creating more understanding.
- M7 must preserve the boundary that projection is not authority: workspace surfaces may show stable decisions, unresolved questions, active risks, constraints, recent changes, and continuity warnings, but they must not become an operational-context editor, decision-resolution surface, or governance workspace.
- M7 should define a formal backend projection contract, conceptually `UnderstandingWorkspaceProjection`, before multiple UI surfaces consume continuity information.
- Epic 3 has transitioned from foundational continuity mechanics toward visibility, certification, and instrumentation.

## Recommended Next Slice

- Start M7 by adding a canonical backend understanding workspace projection for operational-context sections, decision-derived understanding, and continuity warnings.
- Surface that projection in the repository workspace before broad UI refactoring.
