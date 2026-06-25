import { Badge, EmptyState } from '../design'
import type { ExplanationUncertainty } from '../../types'
import { EvidenceList } from './EvidenceList'

type UncertaintyViewProps = {
  uncertainty: ExplanationUncertainty[]
  emptyLabel?: string
  title?: string
}

export function UncertaintyView({
  uncertainty,
  emptyLabel = 'No uncertainty projected.',
  title = 'Uncertainty',
}: UncertaintyViewProps) {
  return (
    <div className="explainability-list explainability-uncertainty-list">
      <h5>{title}</h5>
      {uncertainty.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {uncertainty.map((item, index) => (
            <li key={`${item.label}-${index}`}>
              <div className="explainability-row-header">
                <strong>{item.label}</strong>
                {item.severity ? <Badge tone={item.severity}>{item.severity}</Badge> : null}
              </div>
              <span>{item.detail}</span>
              {item.missingEvidence?.length ? (
                <EvidenceList evidence={item.missingEvidence} title="Missing Evidence" />
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
