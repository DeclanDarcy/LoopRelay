import { EmptyState } from '../design'
import type { ExplanationEvidence } from '../../types'

type EvidenceListProps = {
  evidence: ExplanationEvidence[]
  emptyLabel?: string
  title?: string
}

export function EvidenceList({ evidence, emptyLabel = 'No evidence projected.', title = 'Evidence' }: EvidenceListProps) {
  return (
    <div className="explainability-list explainability-evidence-list">
      <h5>{title}</h5>
      {evidence.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {evidence.map((item, index) => (
            <li key={`${item.id ?? item.label}-${index}`}>
              <strong>{item.label}</strong>
              {item.detail ? <span>{item.detail}</span> : null}
              {item.source ? <small>Source: <span>{item.source}</span></small> : null}
              {item.fingerprint ? <small>Fingerprint: <span>{item.fingerprint}</span></small> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
