import type { DecisionAssimilationProjection } from '../../types'

type OperationalContextAssimilationPanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextAssimilationPanel({
  decisionAssimilation,
}: OperationalContextAssimilationPanelProps) {
  if (decisionAssimilation.decisions.length === 0) {
    return null
  }

  return (
    <div className="assimilation-panel">
      <h5>Decision Assimilation</h5>
      <ul>
        {decisionAssimilation.decisions.map((decision) => (
          <li key={`${decision.decisionId}-${decision.sourceRelativePath}`}>
            <div className="assimilation-header">
              <strong>{decision.status}</strong>
              <span>{decision.taxonomy}</span>
              <span>{decision.isDurable ? 'Durable' : 'Not durable'}</span>
            </div>
            <p>{decision.statement}</p>
            <dl className="assimilation-metadata">
              <div>
                <dt>Decision</dt>
                <dd>{decision.decisionId}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{decision.sourceRelativePath}</dd>
              </div>
              <div>
                <dt>Qualifies</dt>
                <dd>{decision.qualifiesForAssimilation ? 'Yes' : 'No'}</dd>
              </div>
              <div>
                <dt>Operational statement</dt>
                <dd>{decision.operationalStatement ?? 'None'}</dd>
              </div>
              <div>
                <dt>Exclusion reason</dt>
                <dd>{decision.exclusionReason ?? 'None'}</dd>
              </div>
              <div>
                <dt>Omission reason</dt>
                <dd>{decision.omissionReason ?? 'None'}</dd>
              </div>
              <div>
                <dt>Rationale</dt>
                <dd>{decision.rationale ?? 'None'}</dd>
              </div>
            </dl>
            <AssimilationList title="Evidence" items={decision.sourceEvidence} />
            <AssimilationList title="Constraints" items={decision.constraintsIntroduced} />
            <AssimilationList title="Consequences" items={decision.consequencesIntroduced} />
            <AssimilationList title="Open questions" items={decision.openQuestions} />
          </li>
        ))}
      </ul>
    </div>
  )
}

type AssimilationListProps = {
  title: string
  items: string[]
}

function AssimilationList({ title, items }: AssimilationListProps) {
  if (items.length === 0) {
    return null
  }

  return (
    <>
      <h6>{title}</h6>
      <ul className="assimilation-detail-list" aria-label={`Assimilation ${title.toLowerCase()}`}>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </>
  )
}
