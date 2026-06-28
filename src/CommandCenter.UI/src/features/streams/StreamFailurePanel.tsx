import { Button } from '../../components/design'
import './StreamPrimitives.css'

// The recoverable-failure surface shared by the plan, execution, and decision streams: an alert
// eyebrow naming what failed, the reason, an optional detail block, and an optional dismiss action
// back to the prior surface. Presentational only — each source owns its eyebrow text and handler.
type StreamFailurePanelProps = {
  eyebrow: string
  reason: string
  detail?: string | null
  onDismiss?: () => void
  dismissLabel?: string
  // The alert's accessible name, so each source's failure stays addressable by role+name.
  ariaLabel?: string
}

export function StreamFailurePanel({
  eyebrow,
  reason,
  detail = null,
  onDismiss,
  dismissLabel = 'Dismiss',
  ariaLabel,
}: StreamFailurePanelProps) {
  return (
    <section className="cc-stream-failure" role="alert" aria-label={ariaLabel}>
      <div className="cc-stream-failure-body">
        <span className="cc-stream-eyebrow cc-stream-failure-eyebrow">{eyebrow}</span>
        <p className="cc-stream-failure-reason">{reason}</p>
        {detail ? <pre className="cc-stream-failure-detail">{detail}</pre> : null}
      </div>
      {onDismiss ? (
        <Button type="button" variant="secondary" onClick={onDismiss}>
          {dismissLabel}
        </Button>
      ) : null}
    </section>
  )
}
