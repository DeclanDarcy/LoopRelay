import { Badge, EmptyState } from '../design'
import type { ExplanationHealthDimension } from '../../types'
import { DiagnosticList } from './DiagnosticList'
import { EvidenceList } from './EvidenceList'

type HealthViewProps = {
  dimensions: ExplanationHealthDimension[]
  emptyLabel?: string
  title?: string
}

export function HealthView({
  dimensions,
  emptyLabel = 'No health dimensions projected.',
  title = 'Health Dimensions',
}: HealthViewProps) {
  return (
    <div className="explainability-list explainability-health-view">
      <h5>{title}</h5>
      {dimensions.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {dimensions.map((dimension) => (
            <li key={dimension.name}>
              <div className="explainability-row-header">
                <strong>{dimension.name}</strong>
                <Badge tone={dimension.tone ?? 'neutral'}>{dimension.status}</Badge>
              </div>
              <span>{dimension.reason}</span>
              <EvidenceList evidence={dimension.evidence} />
              <DiagnosticList diagnostics={dimension.diagnostics} />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
