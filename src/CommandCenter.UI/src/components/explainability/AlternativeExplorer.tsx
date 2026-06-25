import { Badge, EmptyState } from '../design'
import type { ExplanationAlternative } from '../../types'
import { EvidenceList } from './EvidenceList'

type AlternativeExplorerProps = {
  alternatives: ExplanationAlternative[]
  emptyLabel?: string
  title?: string
}

export function AlternativeExplorer({
  alternatives,
  emptyLabel = 'No alternatives projected.',
  title = 'Alternatives',
}: AlternativeExplorerProps) {
  return (
    <div className="explainability-list explainability-alternative-list">
      <h5>{title}</h5>
      {alternatives.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {alternatives.map((alternative, index) => (
            <li key={`${alternative.label}-${index}`}>
              <div className="explainability-row-header">
                <strong>{alternative.label}</strong>
                {alternative.selected ? <Badge tone="success">Selected</Badge> : null}
              </div>
              <span>{alternative.detail}</span>
              {alternative.reason ? <small>Reason: {alternative.reason}</small> : null}
              {alternative.evidence?.length ? <EvidenceList evidence={alternative.evidence} /> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
