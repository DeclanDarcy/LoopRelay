# Milestone 9 Obsolete UI Cleanup: Continuity and Reasoning Diagnostics

## Scope

- Removed local fallback diagnostic rendering from `ReasoningGraphPanel`.
- Routed graph and trace diagnostics through shared `DiagnosticList` using the reasoning explainability adapter.
- Removed duplicate operational-context continuity warning text lists from current context, compression summary, and decision-continuity review panels.
- Routed continuity, compression, retention, and decision-continuity warnings through shared `DiagnosticList`.
- Preserved domain navigation as separate contextual buttons that open the primary continuity diagnostics or decision-retention surfaces.

## Retained

- Reasoning graph node, relationship, and trace tables remain domain-specific navigation and graph context.
- Operational-context item lists, semantic changes, compression counters, revision evidence, and decision-continuity grouping remain domain-specific composition.
- Continuity and reasoning grouped diagnostic adapters remain the single translation point from backend diagnostics to shared explainability diagnostics.

## Verification

- `npm test -- reasoningTrajectory.test.tsx continuityDiagnosticsPanel.test.tsx`
- `npm run build`

## Notes

- `npm run build` still reports the existing Vite chunk-size warning for the main bundle.
