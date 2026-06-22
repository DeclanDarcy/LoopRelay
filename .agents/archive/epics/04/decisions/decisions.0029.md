# Decisions

## Newly Authorized

- Treat `ExecutionContextArtifactDiagnosticsList` as a defensible M0.5 extraction only because it preserves a direct `backend projection -> verbatim display` boundary.
- Keep artifact diagnostics rendering limited to projected ordering, byte counts, warning labels, and hard-limit labels.
- Do not add severity ranking, risk scoring, recommendation generation, readiness evaluation, importance, relevance, priority, or recommended action derivation to artifact diagnostics rendering.
- Treat artifact content previews as the final likely execution-context presentation extraction candidate.
- Artifact content previews may be extracted only if they preserve artifact ordering, existing markdown rendering, `OperationalContext` default-open behavior, and the `Empty artifact.` fallback.
- Do not derive importance, relevance, priority, or recommended action from artifact content previews.
- If artifact preview extraction remains clean, strongly consider declaring execution-context decomposition complete.
- After artifact preview extraction, use a stricter final inventory filter: a candidate must render entirely from props and remain fully functional with all workflow knowledge removed.
- Treat `Retain in App.tsx` as the likely successful outcome for later execution-context audits when remaining surfaces coordinate workflow, readiness, or authority boundaries.

## Next Authorized Slice

Extract artifact content previews only if the implementation remains pure `artifact -> render`; otherwise record why the surface should remain in `App.tsx`.
