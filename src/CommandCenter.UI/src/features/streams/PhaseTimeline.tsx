import { CheckGlyph } from './CheckGlyph'
import './StreamPrimitives.css'

// The ordered phase rail shared by the execution and decision surfaces: a vertical timeline of
// steps, each marked pending / active / done, with the active step exposed via aria-current.
// Presentational only — each source keeps its own resolveSteps mapping and feeds the result here.
export type PhaseStepState = 'pending' | 'active' | 'done'

export type PhaseStep = {
  key: string
  label: string
  state: PhaseStepState
  note?: string | null
}

type PhaseTimelineProps = {
  ariaLabel: string
  steps: PhaseStep[]
}

export function PhaseTimeline({ ariaLabel, steps }: PhaseTimelineProps) {
  return (
    <ol className="cc-stream-timeline" aria-label={ariaLabel}>
      {steps.map((step) => (
        <li
          key={step.key}
          className={`cc-stream-step cc-stream-step-${step.state}`}
          aria-current={step.state === 'active' ? 'step' : undefined}
        >
          <span className="cc-stream-step-marker" aria-hidden="true">
            {step.state === 'done' ? <CheckGlyph /> : <span className="cc-stream-step-dot" />}
          </span>
          <span className="cc-stream-step-body">
            <span className="cc-stream-step-label">{step.label}</span>
            {step.note ? <span className="cc-stream-step-note">{step.note}</span> : null}
          </span>
        </li>
      ))}
    </ol>
  )
}
