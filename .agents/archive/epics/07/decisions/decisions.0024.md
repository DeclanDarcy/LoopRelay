# Decisions

## Newly Authorized

- Accept the Milestone 8 backend quality endpoint slice as correctly preserving the generation, resolution, quality-assessment, persistence, and endpoint boundary.
- Treat unresolved proposals returning `409 Conflict` from proposal-scoped quality assessment as the intended endpoint behavior.
- Keep resolution snapshots as the quality assessment boundary; assess the human-resolved decision outcome rather than mutable current proposal state.
- Keep markdown projection mandatory for persisted quality assessment, report, and trend artifacts.
- Proceed next with M8 quality UI consumption in this order: Tauri commands, UI types, API hooks, then a narrow quality surface.
- Build the first UI quality surface around assessment, report, and trend retrieval before adding a full dashboard.
- Preserve `DecisionQualitySignal` as the primary UI abstraction; keep overall score secondary.
- Prioritize visible signal categories in the narrow UI surface: human authoring burden, recommendation stability, tradeoff quality, context quality, and constraint quality.

## Not Authorized

- Do not build the full dashboard before proving backend-to-Tauri-to-UI quality retrieval.
- Do not present the overall score as the primary quality UI abstraction.
- Do not assess unresolved generated proposals before human resolution authority exists.
