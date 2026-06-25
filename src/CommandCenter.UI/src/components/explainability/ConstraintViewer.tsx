import { Badge, EmptyState } from '../design'
import type { ExplanationConstraint } from '../../types'
import { EvidenceList } from './EvidenceList'

type ConstraintViewerProps = {
  constraints: ExplanationConstraint[]
  emptyLabel?: string
  title?: string
}

export function ConstraintViewer({
  constraints,
  emptyLabel = 'No constraints projected.',
  title = 'Constraints',
}: ConstraintViewerProps) {
  return (
    <div className="explainability-list explainability-constraint-list">
      <h5>{title}</h5>
      {constraints.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {constraints.map((constraint, index) => (
            <li key={`${constraint.label}-${index}`}>
              <div className="explainability-row-header">
                <strong>{constraint.label}</strong>
                {constraint.satisfied === null || constraint.satisfied === undefined ? null : (
                  <Badge tone={constraint.satisfied ? 'success' : 'warning'}>
                    {constraint.satisfied ? 'Satisfied' : 'Open'}
                  </Badge>
                )}
              </div>
              <span>{constraint.detail}</span>
              {constraint.evidence?.length ? <EvidenceList evidence={constraint.evidence} /> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
