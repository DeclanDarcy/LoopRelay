import { ConstraintViewer, EvidenceList } from '../../components/explainability'
import {
  operationalContextCompressionOutcomeToConstraints,
  operationalContextCompressionOutcomeToEvidence,
} from '../../lib/explainability'
import type { OperationalContextCompressionOutcome } from '../../types'

type OperationalContextCompressionExplanationProps = {
  itemOutcomes: OperationalContextCompressionOutcome[]
}

export function OperationalContextCompressionExplanation({
  itemOutcomes,
}: OperationalContextCompressionExplanationProps) {
  if (itemOutcomes.length === 0) {
    return null
  }

  return (
    <div className="compression-outcome-list">
      <h5>Compression Explanation</h5>
      <ul>
        {itemOutcomes.map((outcome, index) => (
          <li key={`${outcome.outcome}-${outcome.itemKind}-${outcome.itemText}-${index}`}>
            <div className="compression-outcome-header">
              <strong>{outcome.outcome}</strong>
              <span>{outcome.itemKind}</span>
            </div>
            <p>{outcome.itemText}</p>
            <ConstraintViewer
              constraints={operationalContextCompressionOutcomeToConstraints(outcome)}
              title="Compression Constraints"
            />
            <EvidenceList
              evidence={operationalContextCompressionOutcomeToEvidence(outcome)}
              title={`${outcome.outcome} evidence`}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}
