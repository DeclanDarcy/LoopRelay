# Decisions

## Newly Authorized

- Split the next proposed M0.3 work into smaller slices instead of extracting `useExecutionContextPreview`, `useExecutionSession`, and `useExecutionEvents` together.
- M0.3A is authorized to extract `useExecutionContextPreview` only.
- `useExecutionContextPreview` should remain a boring projection hook with data, loading, error, load, and refresh behavior.
- M0.3B should handle `useExecutionSession` separately because session lifecycle, refresh, reattachment, and recovery deserve isolated characterization.
- M0.3C should handle `useExecutionEvents` separately because streaming, ordering, cleanup, and repository/session switching are the highest-risk remaining M0.3 extraction.
- `useExecutionEvents` is authorized as transport-adjacent, not workflow-adjacent.
- `useExecutionEvents` may subscribe, unsubscribe, reconnect, expose ordered events, expose the latest event, and expose the event stream.
- `useExecutionEvents` must not calculate execution phase, derive milestones, derive completion, derive readiness, or infer workflow status.
- Event workflow meaning must remain backend projection authority.
- Before extracting SSE behavior, add characterization for event ordering, cleanup on unmount, and repository/session switching without stale listeners.

## Validation Expected For Next Slice

- Add characterization before moving `useExecutionContextPreview`.
- Keep `App.tsx` as owner of navigation state, draft state, view composition, and workflow actions.
- `npm run lint`
- `npm run build`
- `npm run test`
- `npm run test:e2e`
