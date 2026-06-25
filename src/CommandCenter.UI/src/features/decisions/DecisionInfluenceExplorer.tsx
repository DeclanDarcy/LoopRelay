import { EmptyState } from '../../components/design'
import type { DecisionProjectionDecisionDiagnostic } from '../../types'

type DecisionInfluenceExplorerProps = {
  includedDecisions: DecisionProjectionDecisionDiagnostic[]
  excludedDecisions: DecisionProjectionDecisionDiagnostic[]
  supersededDecisions: DecisionProjectionDecisionDiagnostic[]
  conflictingDecisions: DecisionProjectionDecisionDiagnostic[]
  ignoredDecisions: DecisionProjectionDecisionDiagnostic[]
  blockedDecisions: DecisionProjectionDecisionDiagnostic[]
}

const influenceGroups = [
  {
    key: 'includedDecisions',
    label: 'Included Decisions',
    emptyLabel: 'No included decisions were projected.',
  },
  {
    key: 'excludedDecisions',
    label: 'Excluded Decisions',
    emptyLabel: 'No excluded decisions were projected.',
  },
  {
    key: 'supersededDecisions',
    label: 'Superseded Decisions',
    emptyLabel: 'No superseded decisions were projected.',
  },
  {
    key: 'conflictingDecisions',
    label: 'Conflicting Decisions',
    emptyLabel: 'No conflicting decisions were projected.',
  },
  {
    key: 'ignoredDecisions',
    label: 'Ignored Decisions',
    emptyLabel: 'No ignored decisions were projected.',
  },
  {
    key: 'blockedDecisions',
    label: 'Blocked Decisions',
    emptyLabel: 'No blocked decisions were projected.',
  },
] satisfies Array<{
  key: keyof DecisionInfluenceExplorerProps
  label: string
  emptyLabel: string
}>

export function DecisionInfluenceExplorer(props: DecisionInfluenceExplorerProps) {
  return (
    <div className="decision-influence-explorer" aria-label="Decision influence reason categories">
      {influenceGroups.map((group) => (
        <DecisionInfluenceGroup
          key={group.key}
          title={group.label}
          emptyLabel={group.emptyLabel}
          decisions={props[group.key]}
        />
      ))}
    </div>
  )
}

function DecisionInfluenceGroup({
  title,
  emptyLabel,
  decisions,
}: {
  title: string
  emptyLabel: string
  decisions: DecisionProjectionDecisionDiagnostic[]
}) {
  return (
    <div className="execution-influence-section">
      <h5>{title}</h5>
      {decisions.length > 0 ? (
        <div className="decision-influence-diagnostic-list">
          {decisions.map((decision) => (
            <article className="decision-influence-diagnostic" key={`${title}.${decision.decisionId}`}>
              <div>
                <strong>{decision.title}</strong>
                <span>{decision.decisionId}</span>
              </div>
              <p>{decision.reason}</p>
              <div className="execution-influence-meta">
                <span>{decision.state}</span>
                <span>{decision.outcome ?? 'No outcome'}</span>
                <span>{decision.classification}</span>
                <span>{decision.projectedStatementIds.length} projected statements</span>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <EmptyState className="empty-state">{emptyLabel}</EmptyState>
      )}
    </div>
  )
}
