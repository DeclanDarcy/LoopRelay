import { useId } from 'react'
import type { ReactNode } from 'react'

type TooltipProps = {
  // The accessible name for the trigger — required because the visible trigger is usually a glyph.
  triggerLabel: string
  // The detail content revealed on hover/focus. Always in the DOM (and announced via
  // aria-describedby); only its visibility is toggled, so screen readers and tests can reach it.
  children: ReactNode
  // Optional visible trigger content. Defaults to an unobtrusive info glyph.
  trigger?: ReactNode
  className?: string
}

// A hover/focus disclosure for secondary detail. The trigger stays compact so the primary
// content reads cleanly; the bubble is CSS-toggled (no timers, no portal) which keeps it
// deterministic to test and announces through aria-describedby for assistive tech.
export function Tooltip({ triggerLabel, children, trigger, className }: TooltipProps) {
  const tooltipId = useId()

  return (
    <span className={['cc-tooltip', className].filter(Boolean).join(' ')}>
      <button
        type="button"
        className="cc-tooltip-trigger"
        aria-label={triggerLabel}
        aria-describedby={tooltipId}
      >
        {trigger ?? (
          <span className="cc-tooltip-glyph" aria-hidden="true">
            ⓘ
          </span>
        )}
      </button>
      <span role="tooltip" id={tooltipId} className="cc-tooltip-bubble">
        {children}
      </span>
    </span>
  )
}
