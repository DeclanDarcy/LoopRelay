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
            <dl className="compression-outcome-metadata">
              <div>
                <dt>Rule</dt>
                <dd>{outcome.rule}</dd>
              </div>
              <div>
                <dt>Threshold</dt>
                <dd>{outcome.threshold}</dd>
              </div>
              <div>
                <dt>Rationale</dt>
                <dd>{outcome.rationale}</dd>
              </div>
            </dl>
            {outcome.evidence.length > 0 ? (
              <ul className="compression-outcome-evidence" aria-label={`${outcome.outcome} evidence`}>
                {outcome.evidence.map((evidence) => (
                  <li key={evidence}>{evidence}</li>
                ))}
              </ul>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  )
}
