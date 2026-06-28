import { StreamOutputPanel } from '../streams'
import type { PlanTurnPhase } from '../../types'

type PlanStreamViewProps = {
  text: string
  turnPhase: PlanTurnPhase | null
  // The browser is retrying a dropped stream; the pill reads "Reconnecting" until a frame arrives.
  isReconnecting?: boolean
}

const phaseLabel: Record<PlanTurnPhase, string> = {
  WritePlan: 'Writing plan',
  RevisePlan: 'Revising plan',
}

export function PlanStreamView({ text, turnPhase, isReconnecting = false }: PlanStreamViewProps) {
  const label = isReconnecting ? 'Reconnecting' : turnPhase ? phaseLabel[turnPhase] : 'Working'

  return (
    <StreamOutputPanel
      ariaLabel="Planning stream"
      eyebrow="Plan"
      streamedText={text}
      live
      phaseLabel={label}
    />
  )
}
