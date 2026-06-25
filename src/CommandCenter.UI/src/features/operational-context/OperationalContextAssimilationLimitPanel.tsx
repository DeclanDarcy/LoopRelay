import {
  ConstraintViewer,
  EvidenceList,
} from '../../components/explainability'
import {
  decisionAssimilationLimitToConstraints,
  decisionAssimilationLimitToEvidence,
} from '../../lib/explainability'
import type { DecisionAssimilationProjection } from '../../types'

type OperationalContextAssimilationLimitPanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextAssimilationLimitPanel({
  decisionAssimilation,
}: OperationalContextAssimilationLimitPanelProps) {
  const omittedItems = decisionAssimilation.decisions.filter((decision) => decision.isOmittedByLimit)

  return (
    <div className="proposal-warning-list">
      <h5>Assimilation Limit</h5>
      <div className="context-summary-grid">
        <span>Analyzed: {decisionAssimilation.limit.totalAnalyzedItemCount}</span>
        <span>Qualifying: {decisionAssimilation.limit.totalQualifyingItemCount}</span>
        <span>Assimilated: {decisionAssimilation.limit.assimilatedItemCount}</span>
        <span>Omitted: {decisionAssimilation.limit.omittedItemCount}</span>
        <span>Limit: {decisionAssimilation.limit.limit}</span>
      </div>
      <ConstraintViewer
        title="Assimilation Limit Constraints"
        constraints={decisionAssimilationLimitToConstraints(decisionAssimilation.limit)}
      />
      <EvidenceList
        title="Assimilation Limit Evidence"
        evidence={decisionAssimilationLimitToEvidence(decisionAssimilation.limit)}
      />
      {omittedItems.length > 0 ? (
        <>
          <h6>Omitted Items</h6>
          <ul>
            {omittedItems.map((decision) => (
              <li key={`${decision.decisionId}-${decision.statement}`}>
                <strong>{decision.status}</strong>: {decision.statement}
                {decision.omissionReason ? ` (${decision.omissionReason})` : ''}
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </div>
  )
}
