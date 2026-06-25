import { EvidenceList } from '../../components/explainability'
import { continuityDecisionConsequenceToEvidence } from '../../lib/explainability'
import type { DecisionAssimilationProjection } from '../../types'

type OperationalContextConsequencePanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextConsequencePanel({
  decisionAssimilation,
}: OperationalContextConsequencePanelProps) {
  if (decisionAssimilation.consequences.length === 0) {
    return null
  }

  return (
    <div className="assimilation-panel">
      <h5>Decision Consequences</h5>
      <ul>
        {decisionAssimilation.consequences.map((consequence) => (
          <li key={consequence.consequenceId}>
            <div className="assimilation-header">
              <strong>{consequence.affectedArea}</strong>
              <span>{consequence.originatingDecision.decisionId}</span>
              <span>{consequence.originatingDecision.taxonomy}</span>
            </div>
            <p>{consequence.operationalStatement}</p>
            <dl className="assimilation-metadata">
              <div>
                <dt>Originating decision</dt>
                <dd>{consequence.originatingDecision.statement}</dd>
              </div>
              <div>
                <dt>Source</dt>
                <dd>{consequence.originatingDecision.sourceRelativePath}</dd>
              </div>
              <div>
                <dt>Operational impact</dt>
                <dd>{consequence.operationalImpact}</dd>
              </div>
            </dl>
            <EvidenceList
              title="Consequence Evidence"
              evidence={continuityDecisionConsequenceToEvidence(consequence)}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}
