# Decisions

## Newly Authorized

- Treat `ExecutionRepositorySnapshotPanel` as the last execution-context extraction that is unambiguously justified by the M0.5 charter unless a later audit proves another remaining surface is pure `props -> render`.
- Keep repository snapshot rendering projection-only: branch, staged paths, modified paths, untracked paths, related path buckets, and backend-provided state labels may render; repository health, readiness, blocked state, risk, or action guidance must not be inferred in UI code.
- Treat the existing execution-context presentation components as meaningful structural progress while keeping workflow authority, execution authority, readiness authority, draft authority, and composition in `App.tsx`.
- Proceed next with an Artifact Diagnostics Audit before considering extraction.
- Raise the burden of proof for artifact diagnostics because byte thresholds and diagnostic labels are close to execution readiness, blocking, severity, and action semantics.
- Artifact diagnostics may be extracted only if the component renders backend-provided labels, byte counts, paths, threshold labels, and ordering exactly as projected.
- Stop artifact diagnostics extraction immediately if it introduces severity ranking, impact sorting, impact grouping, recommendation generation, readiness determination, blocking determination, action suggestions, or meaning-bearing labels such as `Attention Required`, `Large Artifact`, `Recommended Fix`, or `Execution Risk`.
- Accept that a successful artifact diagnostics audit may conclude `Remain in App.tsx`; this is a valid M0.5 outcome when remaining code reflects deliberate authority placement rather than technical debt.
- Treat M0.5 as transitioning from finding obvious render-only islands to proving that authority-adjacent surfaces should remain where they are.

## Next Authorized Slice

Perform an Artifact Diagnostics Audit. Do not assume extraction is desired; extract only if the audited surface satisfies `props -> render` without interpretation.
