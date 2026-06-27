import type { PlanTurnPhase } from '../../types'

type PlanStreamViewProps = {
  text: string
  turnPhase: PlanTurnPhase | null
}

const phaseLabel: Record<PlanTurnPhase, string> = {
  WritePlan: 'Writing plan',
  RevisePlan: 'Revising plan',
}

export function PlanStreamView({ text, turnPhase }: PlanStreamViewProps) {
  const label = turnPhase ? phaseLabel[turnPhase] : 'Working'

  return (
    <section className="cc-plan-document cc-plan-document-live" aria-label="Planning stream">
      <header className="cc-plan-document-head">
        <span className="cc-plan-eyebrow">Plan</span>
        <span className="cc-plan-phase-pill" aria-live="polite">
          <span className="cc-plan-phase-dot" aria-hidden="true" />
          {label}…
        </span>
      </header>
      <pre className="cc-plan-stream" aria-live="polite" aria-atomic="false">
        {text}
        <span className="cc-plan-caret" aria-hidden="true" />
      </pre>
    </section>
  )
}
