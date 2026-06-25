import { DiagnosticList, EvidenceList } from '../../components/explainability'
import {
  humanAuthoringBurdenExplanationToDiagnostics,
  humanAuthoringBurdenSignalToEvidence,
} from '../../lib/explainability'
import type { HumanAuthoringBurdenExplanation } from '../../types'

export function DecisionBurdenExplanation({
  explanation,
}: {
  explanation: HumanAuthoringBurdenExplanation
}) {
  return (
    <article className="decision-inspection-card" aria-label={`Burden explanation for ${explanation.decisionId}`}>
      <div>
        <span>{explanation.decisionId}</span>
        <strong>Effective burden: {explanation.effectiveBurden}</strong>
      </div>
      <div className="decision-badge-row" aria-label="Burden selection facts">
        <span>{explanation.isUnknown ? 'Unknown burden' : 'Known burden'}</span>
        <span>{explanation.isInferred ? 'Inferred' : 'Signal-backed'}</span>
        {explanation.winningSignal ? <span>Winning signal: {explanation.winningSignal.id}</span> : null}
      </div>
      {explanation.winningSignal ? (
        <div className="decision-inspection-list" aria-label={`Winning burden signal for ${explanation.decisionId}`}>
          <h6>Winning Signal</h6>
          <article className="decision-quality-signal">
            <div>
              <span>
                {explanation.winningSignal.burden} / {explanation.winningSignal.sourceKind}
              </span>
              <strong>{explanation.winningSignal.summary}</strong>
            </div>
            <EvidenceList
              title="Winning Signal Evidence"
              evidence={humanAuthoringBurdenSignalToEvidence(explanation.winningSignal)}
            />
          </article>
        </div>
      ) : null}
      <DiagnosticList
        title="Burden Explanation Diagnostics"
        diagnostics={humanAuthoringBurdenExplanationToDiagnostics(explanation)}
      />
    </article>
  )
}
