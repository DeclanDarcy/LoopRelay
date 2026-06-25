import { Badge, EmptyState } from '../design'
import type { ExplanationAction } from '../../types'
import { ConstraintViewer } from './ConstraintViewer'

type ActionEligibilityViewProps = {
  actions: ExplanationAction[]
  emptyLabel?: string
  title?: string
}

export function ActionEligibilityView({
  actions,
  emptyLabel = 'No actions projected.',
  title = 'Next Action',
}: ActionEligibilityViewProps) {
  return (
    <div className="explainability-list explainability-action-list">
      <h5>{title}</h5>
      {actions.length === 0 ? (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      ) : (
        <ul>
          {actions.map((action) => (
            <li key={action.label}>
              <div className="explainability-row-header">
                <strong>{action.label}</strong>
                <Badge tone={action.eligible ? 'success' : 'warning'}>
                  {action.eligible ? 'Eligible' : 'Blocked'}
                </Badge>
              </div>
              <span>{action.detail}</span>
              {action.reason ? <small>Reason: {action.reason}</small> : null}
              {action.command ? <small>Command: {action.command}</small> : null}
              {action.constraints?.length ? <ConstraintViewer constraints={action.constraints} /> : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
