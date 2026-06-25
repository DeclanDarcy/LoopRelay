import {
  DiagnosticList,
  EvidenceList,
} from '../../components/explainability'
import {
  continuityDecisionContradictionToDiagnostics,
  continuityDecisionContradictionToEvidence,
} from '../../lib/explainability'
import type { DecisionAssimilationProjection } from '../../types'

type OperationalContextContradictionPanelProps = {
  decisionAssimilation: DecisionAssimilationProjection
}

export function OperationalContextContradictionPanel({
  decisionAssimilation,
}: OperationalContextContradictionPanelProps) {
  if (decisionAssimilation.contradictions.length === 0) {
    return null
  }

  return (
    <div className="assimilation-panel">
      <h5>Decision Contradictions</h5>
      <ul>
        {decisionAssimilation.contradictions.map((contradiction) => (
          <li key={contradiction.contradictionId}>
            <div className="assimilation-header">
              <strong>{contradiction.severity}</strong>
              <span>{contradiction.conflictType}</span>
              <span>{contradiction.contradictionId}</span>
            </div>
            <dl className="assimilation-metadata">
              <div>
                <dt>First decision</dt>
                <dd>
                  {contradiction.firstDecision.decisionId}: {contradiction.firstDecision.statement}
                </dd>
              </div>
              <div>
                <dt>First source</dt>
                <dd>{contradiction.firstDecision.sourceRelativePath}</dd>
              </div>
              <div>
                <dt>Second decision</dt>
                <dd>
                  {contradiction.secondDecision.decisionId}: {contradiction.secondDecision.statement}
                </dd>
              </div>
              <div>
                <dt>Second source</dt>
                <dd>{contradiction.secondDecision.sourceRelativePath}</dd>
              </div>
              <div>
                <dt>Resolution guidance</dt>
                <dd>{contradiction.resolutionGuidance}</dd>
              </div>
            </dl>
            <EvidenceList
              title="Contradiction Evidence"
              evidence={continuityDecisionContradictionToEvidence(contradiction)}
            />
            <DiagnosticList
              title="Contradiction Diagnostics"
              diagnostics={continuityDecisionContradictionToDiagnostics(contradiction)}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}
