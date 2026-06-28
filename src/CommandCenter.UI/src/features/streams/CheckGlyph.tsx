// Shared done-marker glyph for the phase timeline. Extracted so PlanStreamView,
// ExecutionStreamView, and DecisionRuntimeView render an identical checkmark.
export function CheckGlyph() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
      <path
        d="M2.5 6.5L5 9L9.5 3.5"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}
