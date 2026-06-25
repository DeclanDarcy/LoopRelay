import { DecisionSourceList } from './DecisionEvidenceFragments'
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
      <p>{explanation.selectionRule}</p>
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
            <DecisionSourceList sources={explanation.winningSignal.sources} />
          </article>
        </div>
      ) : null}
      {explanation.diagnostics.length > 0 ? (
        <div className="decision-warning-list" aria-label={`Burden explanation diagnostics for ${explanation.decisionId}`}>
          {explanation.diagnostics.map((diagnostic) => (
            <span key={`${explanation.decisionId}-${diagnostic}`}>{diagnostic}</span>
          ))}
        </div>
      ) : null}
    </article>
  )
}
