import { Badge, EmptyState } from '../design'
import type { ExplanationDiagnostic } from '../../types'
import { EvidenceList } from './EvidenceList'

type DiagnosticListProps = {
  diagnostics: ExplanationDiagnostic[]
  emptyLabel?: string
  title?: string
}

export function DiagnosticList({
  diagnostics,
  emptyLabel = 'No diagnostics projected.',
  title = 'Diagnostics',
}: DiagnosticListProps) {
  return (
    <div className="explainability-list explainability-diagnostic-list">
      <h5>{title}</h5>
      {diagnostics.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {diagnostics.map((diagnostic, index) => (
            <li key={`${diagnostic.label}-${index}`}>
              <div className="explainability-row-header">
                <strong>{diagnostic.label}</strong>
                {diagnostic.tone ? <Badge tone={diagnostic.tone}>{diagnostic.tone}</Badge> : null}
              </div>
              <span>{diagnostic.detail}</span>
              {diagnostic.evidence?.length ? (
                <EvidenceList evidence={diagnostic.evidence} title="Diagnostic Evidence" />
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
